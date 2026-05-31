using Tapestry.Authoring;
using Xunit;

namespace Tapestry.Engine.Tests;

public class AuthoringPromptTests
{
    [Fact]
    public void Sanitizer_strips_special_token_spans_and_trims()
    {
        var raw = "  <|im_start|>assistant\nA quiet stone chamber.<|im_end|>  ";
        Assert.Equal("assistant\nA quiet stone chamber.", OutputSanitizer.Clean(raw));
    }

    [Fact]
    public void Sanitizer_is_null_and_empty_safe()
    {
        Assert.Equal("", OutputSanitizer.Clean(null));
        Assert.Equal("", OutputSanitizer.Clean("   "));
    }
}
