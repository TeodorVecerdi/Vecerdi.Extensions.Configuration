using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace Vecerdi.Extensions.Configuration.Tests;

/// <summary>File-backed sources against a temporary directory: loading, optionality, merge order, environment layering.</summary>
[TestFixture]
public sealed class UnitySourcesTests {
    private static readonly UnityHostEnvironment s_TestEnvironment = new("Testing", "TestOS");

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
        } catch (IOException) {
            // A watcher may still hold the directory for a moment on Windows; the temp folder is disposable anyway.
        }
    }

    private string Write(string relativePath, string content) {
        var path = Path.Combine(m_Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private static IConfigurationRoot Build(Action<IConfigurationBuilder> configure) {
        var builder = new ConfigurationBuilder();
        configure(builder);
        return builder.Build();
    }

    [Test]
    public void JsonFile_LoadsKeysAsSpelled_NoFileNameSemantics() {
        var path = Write("Logging.json", """{ "Logging": { "LogLevel": { "Default": "Debug" } }, "Other": 1 }""");

        var config = Build(b => b.AddUnityJsonFile(path, reloadOnChange: false));

        Assert.That(config["Logging:LogLevel:Default"], Is.EqualTo("Debug"));
        Assert.That(config["Other"], Is.EqualTo("1"));
        Assert.That(config["Logging.json:Other"], Is.Null);
    }

    [Test]
    public void JsonFile_Missing_IsEmptyWhenOptional_ThrowsOtherwise() {
        var path = Path.Combine(m_Root, "missing.json");

        var config = Build(b => b.AddUnityJsonFile(path, optional: true, reloadOnChange: false));
        Assert.That(config.AsEnumerable(), Is.Empty);

        Assert.Throws<FileNotFoundException>(() => Build(b => b.AddUnityJsonFile(path, optional: false, reloadOnChange: false)));
    }

    [Test]
    public void JsonFile_EnvironmentVariants_LayerInOrder_BaseThenEnvironmentThenPlatform() {
        var path = Write("app.json", """{ "A": "base", "B": "base", "C": "base" }""");
        Write("app.Testing.json", """{ "B": "env", "C": "env" }""");
        Write("app.TestOS.json", """{ "C": "platform" }""");

        var config = Build(b => b.AddUnityJsonFile(path, s_TestEnvironment, reloadOnChange: false));

        Assert.That(config["A"], Is.EqualTo("base"));
        Assert.That(config["B"], Is.EqualTo("env"));
        Assert.That(config["C"], Is.EqualTo("platform"));
    }

    [Test]
    public void JsonFile_EnvironmentVariants_AreOptionalEvenWhenBaseIsRequired() {
        var path = Write("app.json", """{ "A": "base" }""");

        var config = Build(b => b.AddUnityJsonFile(path, s_TestEnvironment, optional: false, reloadOnChange: false));

        Assert.That(config["A"], Is.EqualTo("base"));
    }

    [Test]
    public void Directory_MergesFilesInOrdinalOrder_LaterOverrides() {
        Write("vault/10-first.json", """{ "Shared": "first", "OnlyFirst": "1" }""");
        Write("vault/20-second.json", """{ "Shared": "second", "OnlySecond": "2" }""");
        Write("vault/notes.txt", "ignored");

        var config = Build(b => b.AddDirectoryJson(Path.Combine(m_Root, "vault"), reloadOnChange: false));

        Assert.That(config["Shared"], Is.EqualTo("second"));
        Assert.That(config["OnlyFirst"], Is.EqualTo("1"));
        Assert.That(config["OnlySecond"], Is.EqualTo("2"));
    }

    [Test]
    public void Directory_Missing_IsEmptyWhenOptional_ThrowsOtherwise() {
        var dir = Path.Combine(m_Root, "nope");

        Assert.That(Build(b => b.AddDirectoryJson(dir, optional: true, reloadOnChange: false)).AsEnumerable(), Is.Empty);
        Assert.Throws<DirectoryNotFoundException>(() => Build(b => b.AddDirectoryJson(dir, optional: false, reloadOnChange: false)));
    }

    [Test]
    public void Directory_EnvironmentSubfolders_LayerOverBase() {
        Write("vault/a.json", """{ "X": "base", "Y": "base" }""");
        Write("vault/Testing/a.json", """{ "X": "env" }""");
        Write("vault/TestOS/a.json", """{ "Y": "platform" }""");
        Write("vault/Other/a.json", """{ "X": "should not load", "Y": "should not load" }""");

        var config = Build(b => b.AddDirectoryJson(Path.Combine(m_Root, "vault"), s_TestEnvironment, reloadOnChange: false));

        Assert.That(config["X"], Is.EqualTo("env"));
        Assert.That(config["Y"], Is.EqualTo("platform"));
    }

    [Test]
    public void Directory_MalformedFile_ThrowsNamingTheFile() {
        Write("vault/bad.json", "{ not json");

        var ex = Assert.Throws<FormatException>(() => Build(b => b.AddDirectoryJson(Path.Combine(m_Root, "vault"), reloadOnChange: false)));
        Assert.That(ex!.Message, Does.Contain("bad.json"));
    }

    [Test]
    public void TextFile_BecomesOneValue() {
        var path = Write("prompt.txt", "You are a helpful assistant.\nBe brief.");

        var config = Build(b => b.AddTextFile("SystemPrompt", path, reloadOnChange: false));

        Assert.That(config["SystemPrompt"], Is.EqualTo("You are a helpful assistant.\nBe brief."));
    }

    [Test]
    public void TextFile_Missing_IsEmptyWhenOptional_ThrowsOtherwise() {
        var path = Path.Combine(m_Root, "missing.txt");

        Assert.That(Build(b => b.AddTextFile("K", path, optional: true, reloadOnChange: false))["K"], Is.Null);
        Assert.Throws<FileNotFoundException>(() => Build(b => b.AddTextFile("K", path, optional: false, reloadOnChange: false)));
    }

    [Test]
    public void Providers_DescribeThemselves_ForTheInspector() {
        var path = Write("app.json", """{ "A": "1" }""");
        var config = Build(b => b.AddUnityJsonFile(path, reloadOnChange: false));

        var provider = ConfigurationInspector.FindWinningProvider(config, "A");

        Assert.That(provider, Is.Not.Null);
        Assert.That(provider!.ToString(), Does.Contain("app.json"));
        Assert.That(ConfigurationInspector.FindWinningProvider(config, "Missing"), Is.Null);
    }

    [Test]
    public void ExposeToInspector_RegistersAConfigurationManager() {
        var manager = new ConfigurationManager();
        manager.AddInMemoryCollection([new("A", "1")]);

        manager.ExposeToInspector("Test root");
        try {
            Assert.That(ConfigurationInspector.Roots, Has.Some.Matches<ConfigurationInspector.Entry>(e => e.Name == "Test root" && ReferenceEquals(e.Root, manager)));
        } finally {
            ConfigurationInspector.Unregister(manager);
        }

        Assert.Throws<InvalidOperationException>(() => new ConfigurationBuilder().ExposeToInspector());
    }

    [Test]
    public void HostEnvironment_PlatformNames_AreShort() {
        Assert.That(UnityHostEnvironment.PlatformNameFor(UnityEngine.RuntimePlatform.WindowsPlayer), Is.EqualTo("Windows"));
        Assert.That(UnityHostEnvironment.PlatformNameFor(UnityEngine.RuntimePlatform.OSXEditor), Is.EqualTo("macOS"));
        Assert.That(UnityHostEnvironment.PlatformNameFor(UnityEngine.RuntimePlatform.Android), Is.EqualTo("Android"));
        Assert.That(UnityHostEnvironment.Current.IsEditor, Is.True);
    }
}
