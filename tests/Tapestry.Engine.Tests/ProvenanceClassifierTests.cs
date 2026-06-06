using Tapestry.Engine.Authoring;
using Xunit;

namespace Tapestry.Engine.Tests;

public class ProvenanceClassifierTests
{
    [Theory]
    [InlineData("@mallek/lf", false, "[pack]")]
    [InlineData("@mallek/lf", true, "[pack +edits]")]
    [InlineData(null, true, "[authored]")]
    [InlineData("", true, "[authored]")]
    [InlineData(null, false, "[authored]")]
    public void Classify_ReturnsExpectedTag(string? sourcePack, bool sideCarExists, string expected)
    {
        Assert.Equal(expected, ProvenanceClassifier.Classify(sourcePack, sideCarExists));
    }
}
