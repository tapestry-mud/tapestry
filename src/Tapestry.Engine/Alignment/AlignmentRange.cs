// src/Tapestry.Engine/Alignment/AlignmentRange.cs
namespace Tapestry.Engine.Alignment;

public sealed record AlignmentRange
{
    public int? Min { get; init; }
    public int? Max { get; init; }

    public bool Allows(int alignment)
    {
        if (Min.HasValue && alignment < Min.Value) { return false; }
        if (Max.HasValue && alignment > Max.Value) { return false; }
        return true;
    }
}

public sealed record AlignmentHistoryEntry(long Timestamp, int Delta, string Reason, int NewValue);
