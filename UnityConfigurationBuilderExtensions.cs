using Microsoft.Extensions.Configuration;
using UnityEngine;
using Vecerdi.Extensions.Configuration.MainThread;
using Vecerdi.Extensions.Configuration.Sources;

namespace Vecerdi.Extensions.Configuration;

/// <summary>
/// Unity-aware sources for <see cref="IConfigurationBuilder"/>. Every file-backed source reloads on the
/// Unity main thread. The environment-aware overloads layer <c>name.json</c>, then
/// <c>name.{Environment}.json</c>, then <c>name.{Platform}.json</c> (or the equivalent sub-folders for
/// directories); only the base is subject to <c>optional</c>, the variants are always optional.
/// </summary>
public static class UnityConfigurationBuilderExtensions {
    /// <summary>A JSON <c>TextAsset</c> under <c>Resources</c>, path without extension.</summary>
    public static IConfigurationBuilder AddResourcesJson(this IConfigurationBuilder builder, string resourcePath, bool optional = true) {
        return builder.Add(new ResourcesJsonConfigurationSource(resourcePath, optional));
    }

    /// <summary>Resources JSON with environment and platform variants: <c>path</c>, <c>path.Editor</c>, <c>path.Windows</c>, ...</summary>
    public static IConfigurationBuilder AddResourcesJson(this IConfigurationBuilder builder, string resourcePath, UnityHostEnvironment environment, bool optional = true) {
        builder.AddResourcesJson(resourcePath, optional);
        builder.AddResourcesJson($"{resourcePath}.{environment.EnvironmentName}", optional: true);
        builder.AddResourcesJson($"{resourcePath}.{environment.PlatformName}", optional: true);
        return builder;
    }

    /// <summary>A JSON file at an absolute path.</summary>
    public static IConfigurationBuilder AddUnityJsonFile(this IConfigurationBuilder builder, string path, bool optional = true, bool reloadOnChange = true) {
        return builder.Add(new JsonFileConfigurationSource(path, optional, reloadOnChange));
    }

    /// <summary>A JSON file with environment and platform variants next to it: <c>name.json</c>, <c>name.Editor.json</c>, <c>name.Windows.json</c>, ...</summary>
    public static IConfigurationBuilder AddUnityJsonFile(this IConfigurationBuilder builder, string path, UnityHostEnvironment environment, bool optional = true, bool reloadOnChange = true) {
        builder.AddUnityJsonFile(path, optional, reloadOnChange);
        builder.AddUnityJsonFile(WithSuffix(path, environment.EnvironmentName), optional: true, reloadOnChange);
        builder.AddUnityJsonFile(WithSuffix(path, environment.PlatformName), optional: true, reloadOnChange);
        return builder;
    }

    /// <summary>A JSON file under <c>Application.streamingAssetsPath</c>. Desktop and editor only: on Android and WebGL StreamingAssets is not a file system, so the file is treated as absent.</summary>
    public static IConfigurationBuilder AddStreamingAssetsJson(this IConfigurationBuilder builder, string relativePath, bool optional = true, bool reloadOnChange = true) {
        return builder.AddUnityJsonFile(Path.Combine(Application.streamingAssetsPath, relativePath), optional: optional || !StreamingAssetsIsFileSystem, reloadOnChange);
    }

    /// <summary>StreamingAssets JSON with environment and platform variants.</summary>
    public static IConfigurationBuilder AddStreamingAssetsJson(this IConfigurationBuilder builder, string relativePath, UnityHostEnvironment environment, bool optional = true, bool reloadOnChange = true) {
        return builder.AddUnityJsonFile(Path.Combine(Application.streamingAssetsPath, relativePath), environment, optional: optional || !StreamingAssetsIsFileSystem, reloadOnChange);
    }

    /// <summary>A JSON file under <c>Application.persistentDataPath</c>: the natural place for per-user overrides.</summary>
    public static IConfigurationBuilder AddPersistentDataJson(this IConfigurationBuilder builder, string relativePath, bool optional = true, bool reloadOnChange = true) {
        return builder.AddUnityJsonFile(Path.Combine(Application.persistentDataPath, relativePath), optional, reloadOnChange);
    }

    /// <summary>Persistent-data JSON with environment and platform variants.</summary>
    public static IConfigurationBuilder AddPersistentDataJson(this IConfigurationBuilder builder, string relativePath, UnityHostEnvironment environment, bool optional = true, bool reloadOnChange = true) {
        return builder.AddUnityJsonFile(Path.Combine(Application.persistentDataPath, relativePath), environment, optional, reloadOnChange);
    }

    /// <summary>Every JSON file directly inside a directory, merged in ordinal path order.</summary>
    public static IConfigurationBuilder AddDirectoryJson(this IConfigurationBuilder builder, string directory, string searchPattern = "*.json", bool optional = true, bool reloadOnChange = true) {
        return builder.Add(new DirectoryJsonConfigurationSource(directory, searchPattern, optional, reloadOnChange));
    }

    /// <summary>A directory with environment and platform sub-folders: <c>dir/</c>, then <c>dir/Editor/</c>, then <c>dir/Windows/</c>, ...</summary>
    public static IConfigurationBuilder AddDirectoryJson(this IConfigurationBuilder builder, string directory, UnityHostEnvironment environment, string searchPattern = "*.json", bool optional = true, bool reloadOnChange = true) {
        builder.AddDirectoryJson(directory, searchPattern, optional, reloadOnChange);
        builder.AddDirectoryJson(Path.Combine(directory, environment.EnvironmentName), searchPattern, optional: true, reloadOnChange);
        builder.AddDirectoryJson(Path.Combine(directory, environment.PlatformName), searchPattern, optional: true, reloadOnChange);
        return builder;
    }

    /// <summary>The whole content of a text file as one key's value.</summary>
    public static IConfigurationBuilder AddTextFile(this IConfigurationBuilder builder, string key, string path, bool optional = true, bool reloadOnChange = true) {
        return builder.Add(new TextFileConfigurationSource(key, path, optional, reloadOnChange));
    }

    /// <summary>
    /// Adds sources from any provider so that their reload tokens fire on the Unity main thread, e.g.
    /// <c>builder.AddOnMainThread(b =&gt; b.AddJsonFile("x.json", reloadOnChange: true))</c>.
    /// </summary>
    public static IConfigurationBuilder AddOnMainThread(this IConfigurationBuilder builder, Action<IConfigurationBuilder> configure) {
        var inner = new ConfigurationBuilder();
        foreach (var (key, value) in builder.Properties) {
            inner.Properties[key] = value;
        }

        configure(inner);
        foreach (var source in inner.Sources) {
            builder.Add(new MainThreadReloadConfigurationSource(source));
        }

        return builder;
    }

    /// <summary>
    /// Makes the built configuration visible in the editor's Configuration window. Works directly on a
    /// <c>ConfigurationManager</c> (which is both a builder and a root); for a plain builder, register the
    /// root you build with <see cref="ConfigurationInspector.Register"/> instead.
    /// </summary>
    public static IConfigurationBuilder ExposeToInspector(this IConfigurationBuilder builder, string name = "Default") {
        if (builder is IConfigurationRoot root) {
            ConfigurationInspector.Register(name, root);
            return builder;
        }

        throw new InvalidOperationException($"{builder.GetType().Name} is not an IConfigurationRoot. Build it first and call {nameof(ConfigurationInspector)}.{nameof(ConfigurationInspector.Register)} with the result.");
    }

    private static bool StreamingAssetsIsFileSystem => Application.platform is not (RuntimePlatform.Android or RuntimePlatform.WebGLPlayer);

    private static string WithSuffix(string path, string suffix) {
        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        return Path.Combine(directory, $"{name}.{suffix}{extension}");
    }
}
