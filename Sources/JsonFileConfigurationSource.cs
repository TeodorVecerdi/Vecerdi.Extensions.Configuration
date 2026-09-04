using Microsoft.Extensions.Configuration;

namespace Vecerdi.Extensions.Configuration.Sources;

/// <summary>A single JSON file at an absolute path. Keys come straight from the document.</summary>
public sealed class JsonFileConfigurationSource(string path, bool optional, bool reloadOnChange) : IConfigurationSource {
    public string Path { get; } = System.IO.Path.GetFullPath(path);
    public bool Optional { get; } = optional;
    public bool ReloadOnChange { get; } = reloadOnChange;

    public IConfigurationProvider Build(IConfigurationBuilder builder) => new Provider(this);

    private sealed class Provider(JsonFileConfigurationSource source)
        : WatchingConfigurationProvider(System.IO.Path.GetDirectoryName(source.Path)!, System.IO.Path.GetFileName(source.Path), includeSubdirectories: false, source.ReloadOnChange) {
        protected override Dictionary<string, string?> LoadCore() {
            if (!File.Exists(source.Path)) {
                return source.Optional
                    ? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                    : throw new FileNotFoundException($"Configuration file not found: {source.Path}", source.Path);
            }

            return JsonConfigurationParser.Parse(File.ReadAllText(source.Path));
        }

        public override string ToString() => $"JSON file '{source.Path}'";
    }
}
