using Microsoft.Extensions.Configuration;

namespace Vecerdi.Extensions.Configuration.Sources;

/// <summary>
/// Every JSON file matching <see cref="SearchPattern"/> directly inside a directory, merged in ordinal
/// path order so later files override earlier ones. File names carry no meaning; the keys are exactly
/// what each document spells out.
/// </summary>
public sealed class DirectoryJsonConfigurationSource(string directory, string searchPattern, bool optional, bool reloadOnChange) : IConfigurationSource {
    public string Directory { get; } = Path.GetFullPath(directory);
    public string SearchPattern { get; } = searchPattern;
    public bool Optional { get; } = optional;
    public bool ReloadOnChange { get; } = reloadOnChange;

    public IConfigurationProvider Build(IConfigurationBuilder builder) => new Provider(this);

    private sealed class Provider(DirectoryJsonConfigurationSource source)
        : WatchingConfigurationProvider(source.Directory, source.SearchPattern, includeSubdirectories: false, source.ReloadOnChange) {
        protected override Dictionary<string, string?> LoadCore() {
            var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            if (!System.IO.Directory.Exists(source.Directory)) {
                return source.Optional ? data : throw new DirectoryNotFoundException($"Configuration directory not found: {source.Directory}");
            }

            foreach (var file in System.IO.Directory.EnumerateFiles(source.Directory, source.SearchPattern, SearchOption.TopDirectoryOnly).OrderBy(f => f, StringComparer.Ordinal)) {
                try {
                    JsonConfigurationParser.ParseInto(File.ReadAllText(file), data);
                } catch (Exception ex) {
                    throw new FormatException($"Failed to parse configuration file '{file}': {ex.Message}", ex);
                }
            }

            return data;
        }

        public override string ToString() => $"JSON files '{Path.Combine(source.Directory, source.SearchPattern)}'";
    }
}
