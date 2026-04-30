using System.Text;

namespace Tapestry.Engine.Ui;

public static class TagStripper
{
    public static string StripTags(string input)
    {
        if (string.IsNullOrEmpty(input) || !input.Contains('<'))
        {
            return input;
        }

        var sb = new StringBuilder(input.Length);
        var i = 0;

        while (i < input.Length)
        {
            if (input[i] == '<')
            {
                var end = input.IndexOf('>', i);
                if (end == -1)
                {
                    // No closing > — treat remainder as literal
                    sb.Append(input, i, input.Length - i);
                    break;
                }
                i = end + 1;
            }
            else
            {
                sb.Append(input[i]);
                i++;
            }
        }

        return sb.ToString();
    }

    public static int VisibleLength(string input)
    {
        if (string.IsNullOrEmpty(input)) { return 0; }
        if (!input.Contains('<')) { return input.Length; }
        return StripTags(input).Length;
    }
}
