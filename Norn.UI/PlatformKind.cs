namespace Norn.UI;

/// <summary>The platforms this project resolves a save directory for.</summary>
public enum PlatformKind
{
    Windows,
    Linux,
}

/// <summary>
/// The one place <see cref="OperatingSystem.IsWindows"/> is read to produce a
/// <see cref="PlatformKind"/> — previously copy-pasted identically across 9
/// separate shim classes (found in review), which let one of them claim in
/// its own doc comment to be "the only platform-gated code in this project"
/// while 8 other copies already existed elsewhere. Every shim now reads
/// <see cref="Current"/> instead of detecting the platform itself.
/// </summary>
public static class PlatformDetection
{
    public static PlatformKind Current => OperatingSystem.IsWindows() ? PlatformKind.Windows : PlatformKind.Linux;
}
