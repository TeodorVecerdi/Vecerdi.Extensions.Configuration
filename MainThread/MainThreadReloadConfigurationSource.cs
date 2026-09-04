using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Vecerdi.Extensions.Configuration.MainThread;

/// <summary>
/// Wraps another source so its reload token fires on the Unity main thread. The wrapped provider
/// keeps doing the loading; only the notification is marshalled, so <c>IOptionsMonitor.OnChange</c>
/// callbacks and reload-token listeners can touch Unity APIs.
/// </summary>
public sealed class MainThreadReloadConfigurationSource(IConfigurationSource inner) : IConfigurationSource {
    public IConfigurationProvider Build(IConfigurationBuilder builder) => new Provider(inner.Build(builder));

    private sealed class Provider : IConfigurationProvider, IDisposable {
        private readonly IConfigurationProvider m_Inner;
        private readonly IDisposable m_Subscription;
        private ConfigurationReloadToken m_Token = new();

        public Provider(IConfigurationProvider inner) {
            m_Inner = inner;
            m_Subscription = ChangeToken.OnChange(inner.GetReloadToken, () => UnityMainThread.Post(RaiseReload));
        }

        public bool TryGet(string key, out string? value) => m_Inner.TryGet(key, out value);
        public void Set(string key, string? value) => m_Inner.Set(key, value);
        public IChangeToken GetReloadToken() => m_Token;
        public void Load() => m_Inner.Load();
        public IEnumerable<string> GetChildKeys(IEnumerable<string> earlierKeys, string? parentPath) => m_Inner.GetChildKeys(earlierKeys, parentPath);
        public override string ToString() => $"{m_Inner} (reload on main thread)";

        private void RaiseReload() {
            var previous = Interlocked.Exchange(ref m_Token, new ConfigurationReloadToken());
            previous.OnReload();
        }

        public void Dispose() {
            m_Subscription.Dispose();
            (m_Inner as IDisposable)?.Dispose();
        }
    }
}
