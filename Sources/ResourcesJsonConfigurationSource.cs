using Microsoft.Extensions.Configuration;
using UnityEngine;

namespace Vecerdi.Extensions.Configuration.Sources;

/// <summary>
/// A JSON <see cref="TextAsset"/> loaded through <c>Resources.Load</c>. Works on every platform and
/// before the first scene, which makes it the right place for a project's shipped defaults. Loads on
/// the main thread (a Unity requirement) and never reloads: Resources are immutable at runtime.
/// </summary>
public sealed class ResourcesJsonConfigurationSource(string resourcePath, bool optional) : IConfigurationSource {
    /// <summary>Path under a <c>Resources</c> folder, without extension, e.g. <c>config/appsettings</c>.</summary>
    public string ResourcePath { get; } = resourcePath;

    public bool Optional { get; } = optional;

    public IConfigurationProvider Build(IConfigurationBuilder builder) => new Provider(this);

    private sealed class Provider(ResourcesJsonConfigurationSource source) : ConfigurationProvider {
        public override void Load() {
            var asset = Resources.Load<TextAsset>(source.ResourcePath);
            if (asset == null) {
                if (!source.Optional) {
                    throw new FileNotFoundException($"Configuration resource not found: Resources/{source.ResourcePath}");
                }

                Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                return;
            }

            Data = JsonConfigurationParser.Parse(asset.text);
        }

        public override string ToString() => $"Resources '{source.ResourcePath}'";
    }
}
