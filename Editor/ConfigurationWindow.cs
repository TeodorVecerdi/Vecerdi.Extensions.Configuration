using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Vecerdi.Extensions.Configuration.Editor;

/// <summary>
/// Shows the effective configuration of every root registered with <see cref="ConfigurationInspector"/>:
/// each key, its winning value, and which provider supplied it. Filters, reloads, and follows reload
/// tokens so a file save is visible immediately.
/// </summary>
public sealed class ConfigurationWindow : EditorWindow {
    private sealed record Row(string Key, string? Value, string Provider);

    private readonly List<Row> m_AllRows = [];
    private readonly List<Row> m_VisibleRows = [];
    private DropdownField m_RootDropdown = null!;
    private ToolbarSearchField m_Filter = null!;
    private Label m_Summary = null!;
    private ToolbarToggle m_ShowSecrets = null!;
    private MultiColumnListView m_List = null!;
    private IDisposable? m_ReloadSubscription;
    private IConfigurationRoot? m_Root;

    [MenuItem("Window/Configuration")]
    public static void ShowWindow() {
        var window = GetWindow<ConfigurationWindow>();
        window.titleContent = new GUIContent("Configuration");
        window.minSize = new Vector2(480, 240);
        window.Show();
    }

    private void CreateGUI() {
        var toolbar = new Toolbar();
        m_RootDropdown = new DropdownField { tooltip = "Registered configuration roots" };
        m_RootDropdown.style.minWidth = 140;
        m_RootDropdown.RegisterValueChangedCallback(_ => SelectRoot(m_RootDropdown.index));
        toolbar.Add(m_RootDropdown);

        m_Filter = new ToolbarSearchField();
        m_Filter.style.flexGrow = 1;
        m_Filter.RegisterValueChangedCallback(_ => ApplyFilter());
        toolbar.Add(m_Filter);

        m_ShowSecrets = new ToolbarToggle { text = "Show secrets", tooltip = "Keys that look like credentials (ApiKey, Secret, Token, Password) are masked unless this is on" };
        m_ShowSecrets.RegisterValueChangedCallback(_ => m_List.RefreshItems());
        toolbar.Add(m_ShowSecrets);

        var reload = new ToolbarButton(() => m_Root?.Reload()) { text = "Reload", tooltip = "Reload every provider of the selected root" };
        toolbar.Add(reload);
        rootVisualElement.Add(toolbar);

        m_Summary = new Label { style = { marginLeft = 6, marginTop = 2, marginBottom = 2, unityFontStyleAndWeight = FontStyle.Italic } };
        rootVisualElement.Add(m_Summary);

        m_List = new MultiColumnListView {
            itemsSource = m_VisibleRows,
            fixedItemHeight = 20,
            showBorder = true,
            selectionType = SelectionType.Single,
            style = { flexGrow = 1 },
        };
        m_List.columns.Add(Column("Key", 0.4f, row => row.Key));
        m_List.columns.Add(Column("Value", 0.35f, row => row.Value is null ? "<null>" : IsSecret(row.Key) && !m_ShowSecrets.value ? "••••••••" : row.Value, italicWhenNull: true));
        m_List.columns.Add(Column("Provider", 0.25f, row => row.Provider));
        rootVisualElement.Add(m_List);

        ConfigurationInspector.Changed += OnRootsChanged;
        RefreshRoots();
    }

    private void OnDisable() {
        ConfigurationInspector.Changed -= OnRootsChanged;
        m_ReloadSubscription?.Dispose();
        m_ReloadSubscription = null;
    }

    private void OnRootsChanged() => EditorApplication.delayCall += RefreshRoots;

    private void RefreshRoots() {
        var roots = ConfigurationInspector.Roots;
        m_RootDropdown.choices = roots.Select(r => r.Name).ToList();
        var index = m_Root is null ? 0 : Math.Max(0, roots.ToList().FindIndex(r => ReferenceEquals(r.Root, m_Root)));
        if (roots.Count == 0) {
            m_RootDropdown.SetValueWithoutNotify(string.Empty);
            SelectRoot(-1);
            return;
        }

        m_RootDropdown.SetValueWithoutNotify(roots[index].Name);
        SelectRoot(index);
    }

    private void SelectRoot(int index) {
        m_ReloadSubscription?.Dispose();
        m_ReloadSubscription = null;

        var roots = ConfigurationInspector.Roots;
        m_Root = index >= 0 && index < roots.Count ? roots[index].Root : null;
        if (m_Root is not null) {
            var root = m_Root;
            m_ReloadSubscription = ChangeToken.OnChange(root.GetReloadToken, () => EditorApplication.delayCall += Rebuild);
        }

        Rebuild();
    }

    private void Rebuild() {
        m_AllRows.Clear();
        if (m_Root is null) {
            m_Summary.text = "No configuration root registered. Call ExposeToInspector() on your ConfigurationManager, or ConfigurationInspector.Register(...).";
            ApplyFilter();
            return;
        }

        Collect(m_Root, m_Root);
        m_AllRows.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase));
        var providers = m_Root.Providers.Count();
        m_Summary.text = $"{m_AllRows.Count} keys from {providers} provider{(providers == 1 ? "" : "s")}  ·  environment {UnityHostEnvironment.Current}";
        ApplyFilter();
    }

    private void Collect(IConfigurationRoot root, IConfiguration node) {
        foreach (var child in node.GetChildren()) {
            var hasChildren = child.GetChildren().Any();
            if (child.Value is not null || !hasChildren) {
                var provider = ConfigurationInspector.FindWinningProvider(root, child.Path);
                m_AllRows.Add(new Row(child.Path, child.Value, provider?.ToString() ?? "-"));
            }

            if (hasChildren) {
                Collect(root, child);
            }
        }
    }

    private void ApplyFilter() {
        var filter = m_Filter.value;
        m_VisibleRows.Clear();
        m_VisibleRows.AddRange(string.IsNullOrWhiteSpace(filter)
            ? m_AllRows
            : m_AllRows.Where(r => r.Key.Contains(filter, StringComparison.OrdinalIgnoreCase) || (r.Value?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false)));
        m_List.RefreshItems();
    }

    private static readonly string[] s_SecretMarkers = ["apikey", "api_key", "secret", "token", "password", "credential"];

    private static bool IsSecret(string key) {
        var last = key.LastIndexOf(':') is var i && i >= 0 ? key[(i + 1)..] : key;
        return s_SecretMarkers.Any(marker => last.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private Column Column(string title, float widthFraction, Func<Row, string> text, bool italicWhenNull = false) {
        return new Column {
            title = title,
            width = new Length(widthFraction * 100, LengthUnit.Percent),
            stretchable = true,
            makeCell = () => new Label { style = { unityTextAlign = TextAnchor.MiddleLeft, overflow = Overflow.Hidden, textOverflow = TextOverflow.Ellipsis, marginLeft = 4 } },
            bindCell = (element, index) => {
                var row = m_VisibleRows[index];
                var label = (Label)element;
                label.text = text(row).Replace('\n', ' ');
                label.tooltip = label.text;
                label.style.unityFontStyleAndWeight = italicWhenNull && row.Value is null ? FontStyle.Italic : FontStyle.Normal;
            },
        };
    }
}
