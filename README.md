# Vecerdi.Extensions.Configuration

Unity-aware sources for `Microsoft.Extensions.Configuration`, plus an editor window that shows the
effective configuration and where each value came from. The stock providers work in Unity on desktop;
what they lack is a way to read from `Resources` on every platform, an idea of the environment you are
running in (editor, development build, release), and reload notifications that arrive on the main
thread. This package adds those and nothing else: your container, options binding, and providers from
elsewhere keep working unchanged.

## Features

- **Sources that fit Unity.** JSON from `Resources` (all platforms, before the first scene), from
  `StreamingAssets`, from `persistentDataPath`, from any absolute path, or every JSON file in a
  directory; and a whole text file as one value, for prompts and templates.
- **Environment layering.** `UnityHostEnvironment` names where you run (`Editor`, `Development`,
  `Production`) and on what (`Windows`, `macOS`, `Linux`, `Android`, `iOS`, `WebGL`). Every source
  has an overload that layers `name.json`, `name.Development.json`, `name.Windows.json`, in that order.
- **Reloads on the main thread.** The file-backed sources watch their files, collapse a save into one
  reload, and raise the reload token on the Unity main thread, so `IOptionsMonitor.OnChange` handlers
  can touch Unity APIs. `AddOnMainThread` gives any other provider the same guarantee.
- **Plain JSON.** Keys are exactly what the document spells out; file names carry no meaning.
  Comments and trailing commas are allowed.
- **A Configuration window.** Every key, its winning value, and the provider that supplied it, with a
  filter and a reload button. Follows reload tokens, so a file save shows up immediately.

## Requirements

- One of:
    - **Unity 6.5 or later** with [UnityRoslynUpdater](https://github.com/DaZombieKiller/UnityRoslynUpdater) to
      enable modern C# features (C# 13+) on the Mono runtime
    - **Unity 7 or later**, which runs on CoreCLR and ships the latest C# features out of the box
- The following NuGet packages (e.g. via NuGetForUnity):
    - Microsoft.Extensions.Configuration
    - Microsoft.Extensions.Configuration.Abstractions
    - System.Text.Json

## Installation

This library is designed to be embedded directly in your project. Add it as a submodule or copy the
source under `Assets/`:

```
git submodule add https://github.com/TeodorVecerdi/Vecerdi.Extensions.Configuration.git Assets/Scripts/Vecerdi.Extensions.Configuration
```

The sources use nullable reference types. Add a `csc.rsp` beside each asmdef (they are gitignored here so
each project keeps its own conventions), at minimum `-nullable:enable`. The `Editor/` folder is an
editor-only assembly and `Tests/` is an edit-mode NUnit assembly.

## Quick start

```csharp
using Microsoft.Extensions.Configuration;
using Vecerdi.Extensions.Configuration;

var environment = UnityHostEnvironment.Current;
var configuration = new ConfigurationManager();

configuration
    .AddResourcesJson("config/appsettings", environment)              // shipped defaults + appsettings.Development / appsettings.Windows
    .AddPersistentDataJson("settings.json", environment)               // per-user overrides, reloaded when the file changes
    .AddEnvironmentVariables("MYGAME_")                                // stock Microsoft source, works as is
    .ExposeToInspector();                                              // show it in Window > Configuration

var provider = new ServiceCollection()
    .AddSingleton<IConfiguration>(configuration)
    .Configure<AudioOptions>(configuration.GetSection("Audio"))
    .BuildServiceProvider();
```

With [Vecerdi.Extensions.DependencyInjection](https://github.com/TeodorVecerdi/Vecerdi.Extensions.DependencyInjection)
the same calls go inside `ServiceManager.RegisterConfiguration`, which hands you its `ConfigurationManager`.

### Sources

| Call | Reads | Reload | Notes |
|---|---|---|---|
| `AddResourcesJson("config/appsettings")` | `TextAsset` under a `Resources` folder | no | Every platform. Path without extension. |
| `AddStreamingAssetsJson("appsettings.json")` | file under `StreamingAssets` | watcher | Not a file system on Android/WebGL: treated as absent there. |
| `AddPersistentDataJson("settings.json")` | file under `persistentDataPath` | watcher | |
| `AddUnityJsonFile(absolutePath)` | any JSON file | watcher | |
| `AddDirectoryJson(directory)` | every `*.json` directly in the directory | watcher | Merged in ordinal path order; later files win. |
| `AddTextFile("SystemPrompt", path)` | whole file as one value | watcher | |
| `AddOnMainThread(b => ...)` | whatever you add inside | marshalled | Wraps foreign sources so their tokens fire on the main thread. |

Every call takes `optional` (default `true`) and, where it applies, `reloadOnChange` (default `true`).

### Environments

```csharp
var env = UnityHostEnvironment.Current;   // Editor / Development / Production + platform name
env.IsProduction                          // Debug.isDebugBuild false, outside the editor
UnityHostEnvironment.Override(new("Staging", "Windows"));   // e.g. from a launcher flag; null to detect again
```

The environment overloads add optional variants after the base:

```
appsettings.json → appsettings.Development.json → appsettings.Windows.json
vault/            → vault/Development/            → vault/Windows/
```

so a build can lower `Logging:Unity:StackTraces` to `WarningsAndErrors` without touching the editor's
defaults.

### The Configuration window

`Window > Configuration` lists each registered root. Register a `ConfigurationManager` with
`ExposeToInspector(name)`, or any built root with `ConfigurationInspector.Register(name, root)`. The
Provider column names the source that won for each key, which is the fastest way to find out why a
value is not what you expected.

## License

[MIT](LICENSE).
