namespace Tapestry.Scripting.Interop;

/// <summary>Which interop primitive a recorded site invoked. <c>Call</c> requires the export to
/// exist; <c>Has</c> is a non-throwing probe and must tolerate a missing export.</summary>
public enum InteropCallKind { Call, Has }

/// <summary>
/// A statically resolvable <c>tapestry.packs.call</c>/<c>has</c> site discovered at script-load
/// time. Pack names are namespace form (e.g. "tapestry-survival"). Only sites whose first two
/// arguments are string literals are recorded; dynamic-dispatch sites are skipped.
/// </summary>
public sealed record InteropCallSite(
    string CallerPack,
    string TargetPack,
    string ExportName,
    InteropCallKind Kind,
    string SourceFile,
    int Line);
