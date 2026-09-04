using UnityEngine;

namespace Vecerdi.Extensions.Configuration;

/// <summary>The environment names this package recognises. Used as file and folder suffixes by the environment-aware sources.</summary>
public static class UnityEnvironments {
    /// <summary>Running inside the Unity Editor (play mode or not).</summary>
    public const string Editor = "Editor";

    /// <summary>A development player build.</summary>
    public const string Development = "Development";

    /// <summary>A release player build.</summary>
    public const string Production = "Production";
}

/// <summary>
/// Where the code is running, for layering configuration the way <c>appsettings.Development.json</c>
/// does on .NET: an environment name (<see cref="UnityEnvironments"/>) plus a short platform name.
/// </summary>
public sealed class UnityHostEnvironment {
    private static UnityHostEnvironment? s_Current;

    /// <summary>Detected from <c>Application.isEditor</c> and <c>Debug.isDebugBuild</c>. Must first be touched on the main thread.</summary>
    public static UnityHostEnvironment Current => s_Current ??= Detect();

    public UnityHostEnvironment(string environmentName, string platformName) {
        EnvironmentName = environmentName;
        PlatformName = platformName;
    }

    /// <summary>One of <see cref="UnityEnvironments"/>, or whatever a host chose to construct.</summary>
    public string EnvironmentName { get; }

    /// <summary>A short platform name: <c>Windows</c>, <c>macOS</c>, <c>Linux</c>, <c>Android</c>, <c>iOS</c>, <c>WebGL</c>, or the raw <c>RuntimePlatform</c> name.</summary>
    public string PlatformName { get; }

    public bool IsEditor => EnvironmentName == UnityEnvironments.Editor;
    public bool IsDevelopment => EnvironmentName == UnityEnvironments.Development;
    public bool IsProduction => EnvironmentName == UnityEnvironments.Production;

    /// <summary>Overrides the detected environment, e.g. from a test or a launcher flag. Pass <c>null</c> to detect again.</summary>
    public static void Override(UnityHostEnvironment? environment) => s_Current = environment;

    private static UnityHostEnvironment Detect() {
        var name = Application.isEditor ? UnityEnvironments.Editor
            : Debug.isDebugBuild ? UnityEnvironments.Development
            : UnityEnvironments.Production;
        return new UnityHostEnvironment(name, PlatformNameFor(Application.platform));
    }

    public static string PlatformNameFor(RuntimePlatform platform) => platform switch {
        RuntimePlatform.WindowsEditor or RuntimePlatform.WindowsPlayer or RuntimePlatform.WindowsServer => "Windows",
        RuntimePlatform.OSXEditor or RuntimePlatform.OSXPlayer or RuntimePlatform.OSXServer => "macOS",
        RuntimePlatform.LinuxEditor or RuntimePlatform.LinuxPlayer or RuntimePlatform.LinuxServer => "Linux",
        RuntimePlatform.Android => "Android",
        RuntimePlatform.IPhonePlayer => "iOS",
        RuntimePlatform.WebGLPlayer => "WebGL",
        _ => platform.ToString(),
    };

    public override string ToString() => $"{EnvironmentName} ({PlatformName})";
}
