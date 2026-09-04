using Microsoft.Extensions.Configuration;

namespace Vecerdi.Extensions.Configuration;

/// <summary>
/// The roots the editor's Configuration window can show. A host registers each root it builds (a
/// <c>ConfigurationManager</c> can do so through <c>ExposeToInspector</c>); the window lists them by name.
/// </summary>
public static class ConfigurationInspector {
    private static readonly List<Entry> s_Roots = [];

    public sealed record Entry(string Name, IConfigurationRoot Root);

    /// <summary>Registered roots, in registration order.</summary>
    public static IReadOnlyList<Entry> Roots {
        get {
            lock (s_Roots) {
                return s_Roots.ToArray();
            }
        }
    }

    /// <summary>Raised on registration and removal. Not guaranteed to be on the main thread.</summary>
    public static event Action? Changed;

    public static void Register(string name, IConfigurationRoot root) {
        lock (s_Roots) {
            s_Roots.RemoveAll(e => ReferenceEquals(e.Root, root));
            s_Roots.Add(new Entry(name, root));
        }

        Changed?.Invoke();
    }

    public static void Unregister(IConfigurationRoot root) {
        lock (s_Roots) {
            s_Roots.RemoveAll(e => ReferenceEquals(e.Root, root));
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// The provider whose value wins for <paramref name="path"/>, or <c>null</c> when no provider has it.
    /// Mirrors what <c>IConfigurationRoot.GetDebugView</c> reports, without rendering the whole tree.
    /// </summary>
    public static IConfigurationProvider? FindWinningProvider(IConfigurationRoot root, string path) {
        var providers = root.Providers as IList<IConfigurationProvider> ?? root.Providers.ToList();
        for (var i = providers.Count - 1; i >= 0; i--) {
            if (providers[i].TryGet(path, out _)) {
                return providers[i];
            }
        }

        return null;
    }
}
