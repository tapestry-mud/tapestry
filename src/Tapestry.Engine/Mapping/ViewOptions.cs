// src/Tapestry.Engine/Mapping/ViewOptions.cs
namespace Tapestry.Engine.Mapping;

public enum LabelMode
{
    /// <summary>Indexed cells + a legend of short id, name, exits — the builder view.</summary>
    Id,
    /// <summary>Indexed cells + a legend of room names only — id-free.</summary>
    Name,
    /// <summary>Glyph-only cells (markers / current / vertical) — the player view.</summary>
    Dot,
}

/// <summary>Render-time options. Projection-time concerns (scope/radius) live in MapScope.</summary>
public sealed record ViewOptions
{
    /// <summary>The room to mark as "you are here" when ShowCurrent is true.</summary>
    public string CurrentRoomId { get; init; } = "";

    public LabelMode Label { get; init; } = LabelMode.Dot;

    /// <summary>Marker key → glyph (first char of the value is used). A cell's glyph is
    /// the first of its Markers that has a legend entry.</summary>
    public IReadOnlyDictionary<string, string> Legend { get; init; } =
        new Dictionary<string, string>();

    public bool ShowCurrent { get; init; } = true;

    /// <summary>Which z-plane to draw. Cells off this plane are footnoted, not drawn.</summary>
    public int Plane { get; init; }
}
