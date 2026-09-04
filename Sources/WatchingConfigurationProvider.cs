using System.Threading;
using Microsoft.Extensions.Configuration;
using Vecerdi.Extensions.Configuration.MainThread;

namespace Vecerdi.Extensions.Configuration.Sources;

/// <summary>
/// Base for the file-backed providers: loads through <see cref="LoadCore"/>, and when
/// <c>reloadOnChange</c> is set watches the containing directory, debounces the burst of events a
/// save produces, and reloads on the Unity main thread so the reload token fires there.
/// </summary>
public abstract class WatchingConfigurationProvider : ConfigurationProvider, IDisposable {
    private static readonly TimeSpan s_Debounce = TimeSpan.FromMilliseconds(250);

    private readonly string m_Directory;
    private readonly string m_Filter;
    private readonly bool m_IncludeSubdirectories;
    private readonly bool m_ReloadOnChange;
    private FileSystemWatcher? m_Watcher;
    private Timer? m_DebounceTimer;
    private bool m_Disposed;

    protected WatchingConfigurationProvider(string directory, string filter, bool includeSubdirectories, bool reloadOnChange) {
        m_Directory = directory;
        m_Filter = filter;
        m_IncludeSubdirectories = includeSubdirectories;
        m_ReloadOnChange = reloadOnChange;
        UnityMainThread.Capture();
    }

    /// <summary>Produces the full key/value set for this provider. Throw to signal a hard failure.</summary>
    protected abstract Dictionary<string, string?> LoadCore();

    public override void Load() {
        Data = LoadCore();
        EnsureWatcher();
    }

    private void EnsureWatcher() {
        if (!m_ReloadOnChange || m_Watcher is not null || m_Disposed || !Directory.Exists(m_Directory)) {
            return;
        }

        m_Watcher = new FileSystemWatcher(m_Directory, m_Filter) {
            IncludeSubdirectories = m_IncludeSubdirectories,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size,
        };
        m_Watcher.Changed += OnFileEvent;
        m_Watcher.Created += OnFileEvent;
        m_Watcher.Deleted += OnFileEvent;
        m_Watcher.Renamed += OnFileEvent;
        m_Watcher.EnableRaisingEvents = true;
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e) {
        // Editors write in several steps (truncate, write, rename); collapse the burst into one reload.
        m_DebounceTimer ??= new Timer(_ => UnityMainThread.Post(ReloadOnMainThread), null, Timeout.Infinite, Timeout.Infinite);
        m_DebounceTimer.Change(s_Debounce, Timeout.InfiniteTimeSpan);
    }

    private void ReloadOnMainThread() {
        if (m_Disposed) {
            return;
        }

        try {
            Data = LoadCore();
        } catch (Exception ex) {
            // A half-written file is the common cause; keep the previous data and say so.
            UnityEngine.Debug.LogWarning($"[{GetType().Name}] Reload failed, keeping previous values: {ex.Message}");
            return;
        }

        OnReload();
    }

    public void Dispose() {
        m_Disposed = true;
        m_DebounceTimer?.Dispose();
        if (m_Watcher is not null) {
            m_Watcher.EnableRaisingEvents = false;
            m_Watcher.Dispose();
            m_Watcher = null;
        }
    }
}
