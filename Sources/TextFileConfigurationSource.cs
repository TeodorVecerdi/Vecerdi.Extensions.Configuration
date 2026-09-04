using Microsoft.Extensions.Configuration;

namespace Vecerdi.Extensions.Configuration.Sources;

/// <summary>The whole content of one text file as the value of one key, e.g. a prompt or a template.</summary>
public sealed class TextFileConfigurationSource(string key, string path, bool optional, bool reloadOnChange) : IConfigurationSource {
    public string Key { get; } = key;
    public string Path { get; } = System.IO.Path.GetFullPath(path);
    public bool Optional { get; } = optional;
    public bool ReloadOnChange { get; } = reloadOnChange;

    public IConfigurationProvider Build(IConfigurationBuilder builder) => new Provider(this);

    private sealed class Provider(TextFileConfigurationSource source)
        : WatchingConfigurationProvider(System.IO.Path.GetDirectoryName(source.Path)!, System.IO.Path.GetFileName(source.Path), includeSubdirectories: false, source.ReloadOnChange) {
        protected override Dictionary<string, string?> LoadCore() {
            var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(source.Path)) {
                data[source.Key] = File.ReadAllText(source.Path);
            } else if (!source.Optional) {
                throw new FileNotFoundException($"Configuration text file not found: {source.Path}", source.Path);
            }

            return data;
        }

        public override string ToString() => $"Text file '{source.Path}' as '{source.Key}'";
    }
}
