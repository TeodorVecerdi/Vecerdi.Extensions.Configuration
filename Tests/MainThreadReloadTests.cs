using System;
using System.Collections;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using NUnit.Framework;
using UnityEngine.TestTools;
using Vecerdi.Extensions.Configuration.MainThread;

namespace Vecerdi.Extensions.Configuration.Tests;

/// <summary>Reload notifications must arrive on the Unity main thread, whether from our watchers or from a wrapped foreign source.</summary>
[TestFixture]
public sealed class MainThreadReloadTests {
    private static readonly TimeSpan s_Timeout = TimeSpan.FromSeconds(10);

    private string m_Root = null!;

    [SetUp]
    public void SetUp() {
        m_Root = Path.Combine(Path.GetTempPath(), "vecerdi-configuration-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(m_Root);
    }

    [TearDown]
    public void TearDown() {
        try {
            Directory.Delete(m_Root, recursive: true);
        } catch (IOException) { }
    }

    [UnityTest]
    public IEnumerator JsonFile_ReloadsOnMainThread_AfterTheFileChanges() {
        var mainThreadId = Environment.CurrentManagedThreadId;
        var path = Path.Combine(m_Root, "app.json");
        File.WriteAllText(path, """{ "A": "1" }""");

        var config = new ConfigurationBuilder().AddUnityJsonFile(path, reloadOnChange: true).Build();
        var fired = 0;
        var firedOnThread = -1;
        using var subscription = ChangeToken.OnChange(config.GetReloadToken, () => {
            firedOnThread = Environment.CurrentManagedThreadId;
            Interlocked.Increment(ref fired);
        });

        try {
            File.WriteAllText(path, """{ "A": "2" }""");

            var deadline = DateTime.UtcNow + s_Timeout;
            while (Volatile.Read(ref fired) == 0 && DateTime.UtcNow < deadline) {
                yield return null; // pump the editor loop so the posted reload can run
            }

            Assert.That(fired, Is.GreaterThanOrEqualTo(1), "reload token never fired");
            Assert.That(firedOnThread, Is.EqualTo(mainThreadId), "reload must be raised on the main thread");
            Assert.That(config["A"], Is.EqualTo("2"));
        } finally {
            (config as IDisposable)?.Dispose();
        }
    }

    [UnityTest]
    public IEnumerator AddOnMainThread_MarshalsAForeignSourcesToken() {
        var mainThreadId = Environment.CurrentManagedThreadId;
        var source = new ManualSource();
        var config = new ConfigurationBuilder().AddOnMainThread(b => b.Add(source)).Build();
        var firedOnThread = -1;
        using var subscription = ChangeToken.OnChange(config.GetReloadToken, () => firedOnThread = Environment.CurrentManagedThreadId);

        var background = Task.Run(() => {
            Assert.That(Environment.CurrentManagedThreadId, Is.Not.EqualTo(mainThreadId));
            source.Provider!.Set("A", "changed");
            source.Provider.Fire();
        });

        var deadline = DateTime.UtcNow + s_Timeout;
        while (firedOnThread == -1 && DateTime.UtcNow < deadline) {
            yield return null;
        }

        Assert.That(background.IsFaulted, Is.False, background.Exception?.ToString());
        Assert.That(firedOnThread, Is.EqualTo(mainThreadId));
        Assert.That(config["A"], Is.EqualTo("changed"));
    }

    [Test]
    public void UnityMainThread_Post_RunsInlineOnTheMainThread() {
        var ran = false;
        UnityMainThread.Post(() => ran = true);

        Assert.That(ran, Is.True);
        Assert.That(UnityMainThread.IsMainThread, Is.True);
    }

    private sealed class ManualSource : IConfigurationSource {
        public ManualProvider? Provider { get; private set; }

        public IConfigurationProvider Build(IConfigurationBuilder builder) => Provider = new ManualProvider();
    }

    private sealed class ManualProvider : ConfigurationProvider {
        public void Fire() => OnReload();
    }
}
