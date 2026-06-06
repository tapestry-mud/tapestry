namespace Tapestry.Engine.Authoring;

/// <summary>
/// Classifies content provenance from its pack origin and whether a runtime side-car exists.
/// Same logic for areas (source_pack + area.yaml) and rooms (source_pack + rooms/&lt;key&gt;.yaml).
/// </summary>
public static class ProvenanceClassifier
{
    public const string Pack = "[pack]";
    public const string Authored = "[authored]";
    public const string PackEdited = "[pack +edits]";

    public static string Classify(string? sourcePack, bool sideCarExists)
    {
        var hasPack = !string.IsNullOrEmpty(sourcePack);
        if (hasPack && sideCarExists)
        {
            return PackEdited;
        }
        if (hasPack)
        {
            return Pack;
        }
        return Authored;
    }
}
