// src/Tapestry.Engine/Mapping/AsciiMapRenderer.cs
using System.Text;

namespace Tapestry.Engine.Mapping;

/// <summary>
/// Renders an AreaMap onto a character grid. One consumer of the AreaMap contract —
/// keep ALL ASCII concerns here; the projector and model must stay rendering-free.
///
/// Geometry: each cell is 3 chars ("[g]"), horizontal pitch 4 (cell + connector
/// column), vertical pitch 2 (room row + connector row). North is up.
/// </summary>
public sealed class AsciiMapRenderer
{
    private const int HPitch = 4;
    private const int VPitch = 2;
    private const string LabelChars = "123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public string Render(AreaMap map, ViewOptions opts)
    {
        if (map.Cells.Count == 0)
        {
            return "There is nothing to map here.";
        }

        var plane = map.Cells.Where(c => c.Z == opts.Plane).ToList();
        var offPlaneCount = map.Cells.Count - plane.Count;
        if (plane.Count == 0)
        {
            return "There is nothing to map on this level.";
        }

        // First room at a position renders; later ones are footnoted (collision).
        var byPos = new Dictionary<(int X, int Y), RoomCell>();
        var overlapping = new List<RoomCell>();
        foreach (var cell in plane)
        {
            if (!byPos.TryAdd((cell.X, cell.Y), cell))
            {
                overlapping.Add(cell);
            }
        }

        var minX = plane.Min(c => c.X);
        var maxX = plane.Max(c => c.X);
        var minY = plane.Min(c => c.Y);
        var maxY = plane.Max(c => c.Y);
        var gridW = (maxX - minX + 1) * HPitch - 1;
        var gridH = (maxY - minY + 1) * VPitch - 1;

        var grid = new char[gridH, gridW];
        for (var r = 0; r < gridH; r++)
        {
            for (var c = 0; c < gridW; c++)
            {
                grid[r, c] = ' ';
            }
        }

        var labels = AssignLabels(plane);

        foreach (var cell in byPos.Values)
        {
            var col = (cell.X - minX) * HPitch;
            var row = (maxY - cell.Y) * VPitch;
            var (open, close) = Brackets(cell, opts);
            grid[row, col] = open;
            grid[row, col + 1] = Glyph(cell, opts, labels);
            grid[row, col + 2] = close;

            // Connectors only for RECIPROCAL exits; drawn east + south so each pair draws once.
            if (cell.Exits.Contains("east")
                && byPos.TryGetValue((cell.X + 1, cell.Y), out var eastCell)
                && eastCell.Exits.Contains("west")
                && col + 3 < gridW)
            {
                grid[row, col + 3] = '-';
            }
            if (cell.Exits.Contains("south")
                && byPos.TryGetValue((cell.X, cell.Y - 1), out var southCell)
                && southCell.Exits.Contains("north")
                && row + 1 < gridH)
            {
                grid[row + 1, col + 1] = '|';
            }
        }

        var lines = new List<string>();
        for (var r = 0; r < gridH; r++)
        {
            var sb = new StringBuilder(gridW);
            for (var c = 0; c < gridW; c++)
            {
                sb.Append(grid[r, c]);
            }
            lines.Add(sb.ToString().TrimEnd());
        }

        AppendLegend(lines, plane, opts, labels);
        AppendFootnotes(lines, map, overlapping, offPlaneCount);

        return string.Join("\r\n", lines);
    }

    private static Dictionary<string, char> AssignLabels(IReadOnlyList<RoomCell> planeCells)
    {
        var labels = new Dictionary<string, char>();
        for (var i = 0; i < planeCells.Count; i++)
        {
            if (!labels.ContainsKey(planeCells[i].Id))
            {
                labels[planeCells[i].Id] = i < LabelChars.Length ? LabelChars[i] : '?';
            }
        }
        return labels;
    }

    private static (char Open, char Close) Brackets(RoomCell cell, ViewOptions opts)
    {
        // Id/Name modes mark the current room with * brackets (the glyph slot holds the index).
        if (opts.Label != LabelMode.Dot
            && opts.ShowCurrent
            && cell.Id == opts.CurrentRoomId)
        {
            return ('*', '*');
        }
        return ('[', ']');
    }

    private static char Glyph(RoomCell cell, ViewOptions opts, IReadOnlyDictionary<string, char> labels)
    {
        if (opts.Label == LabelMode.Id || opts.Label == LabelMode.Name)
        {
            return labels.TryGetValue(cell.Id, out var label) ? label : '?';
        }

        // Dot mode priority: current > marker glyph > vertical glyph > blank.
        if (opts.ShowCurrent && cell.Id == opts.CurrentRoomId)
        {
            return '*';
        }
        foreach (var marker in cell.Markers)
        {
            if (opts.Legend.TryGetValue(marker, out var glyph) && glyph.Length > 0)
            {
                return glyph[0];
            }
        }
        if (cell.HasVertical)
        {
            var up = cell.Exits.Contains("up");
            var down = cell.Exits.Contains("down");
            if (up && down)
            {
                return '%';
            }
            return up ? '^' : 'v';
        }
        return ' ';
    }

    private static void AppendLegend(
        List<string> lines,
        IReadOnlyList<RoomCell> planeCells,
        ViewOptions opts,
        IReadOnlyDictionary<string, char> labels)
    {
        if (opts.Label == LabelMode.Id || opts.Label == LabelMode.Name)
        {
            lines.Add("");
            foreach (var cell in planeCells)
            {
                var label = labels[cell.Id];
                var entry = opts.Label == LabelMode.Id
                    ? $" {label}) {ShortId(cell.Id)}  {cell.Name}  ({string.Join(", ", cell.Exits)})"
                    : $" {label}) {cell.Name}";
                if (opts.Label == LabelMode.Id && cell.Markers.Count > 0)
                {
                    entry += $"  [{string.Join(", ", cell.Markers)}]";
                }
                if (opts.ShowCurrent && cell.Id == opts.CurrentRoomId)
                {
                    entry += "  <- you";
                }
                lines.Add(entry);
            }
            return;
        }

        // Dot mode: glyph legend for marker keys that appear on this plane.
        var used = new List<string>();
        foreach (var cell in planeCells)
        {
            foreach (var marker in cell.Markers)
            {
                if (opts.Legend.TryGetValue(marker, out var glyph) && glyph.Length > 0)
                {
                    if (!used.Contains(marker))
                    {
                        used.Add(marker);
                    }
                    break; // only the first legend-matched marker renders for a cell
                }
            }
        }
        if (used.Count > 0)
        {
            lines.Add("");
            foreach (var key in used)
            {
                lines.Add($" {opts.Legend[key][0]} = {key}");
            }
        }
    }

    private static void AppendFootnotes(
        List<string> lines,
        AreaMap map,
        List<RoomCell> overlapping,
        int offPlaneCount)
    {
        var notes = new List<string>();
        if (overlapping.Count > 0)
        {
            notes.Add($" ! {overlapping.Count} room(s) overlap others (non-grid layout) and are not drawn: "
                + string.Join(", ", overlapping.Select(c => ShortId(c.Id))));
        }
        if (map.UnpositionedRoomIds.Count > 0)
        {
            notes.Add(" ! not connected to the map: "
                + string.Join(", ", map.UnpositionedRoomIds.Select(ShortId)));
        }
        if (offPlaneCount > 0)
        {
            notes.Add($" ! {offPlaneCount} room(s) above or below this level are not shown");
        }
        if (notes.Count > 0)
        {
            lines.Add("");
            lines.AddRange(notes);
        }
    }

    private static string ShortId(string id)
    {
        var idx = id.IndexOf(':');
        return idx >= 0 ? id[(idx + 1)..] : id;
    }
}
