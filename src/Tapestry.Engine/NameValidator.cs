using System.Text.RegularExpressions;

namespace Tapestry.Engine;

public static partial class NameValidator
{
    [GeneratedRegex(@"^[a-zA-Z]{2,12}$")]
    private static partial Regex NamePattern();

    private static readonly HashSet<string> BlockedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "bela"
    };

    private static readonly Dictionary<string, string> BlockedMessages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["bela"] = "The great mare of legend cannot be replaced. Choose another name."
    };

    public static (bool Valid, string? Error) Validate(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return (false, "Please enter a name.");
        }

        if (!NamePattern().IsMatch(name))
        {
            return (false, "Names must be 2-12 letters only.");
        }

        if (BlockedNames.Contains(name))
        {
            return (false, BlockedMessages.GetValueOrDefault(name, "That name is not available."));
        }

        return (true, null);
    }

    public static string Canonicalize(string name) =>
        char.ToUpper(name[0]) + name[1..].ToLower();
}
