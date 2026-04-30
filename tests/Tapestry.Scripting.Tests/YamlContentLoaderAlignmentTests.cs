using Tapestry.Scripting;

namespace Tapestry.Scripting.Tests;

public class YamlContentLoaderAlignmentTests
{
    [Fact]
    public void LoadRoom_ParsesAlignmentRange()
    {
        var yaml = @"
id: 'example-pack:deep-woods'
name: 'The Pit of Doom'
description: 'Dark.'
alignment_range:
  max: -500
alignment_block_message: 'The land itself rejects you.'
";
        var result = YamlContentLoader.LoadRoom(yaml);
        Assert.NotNull(result.Room.AlignmentRange);
        Assert.Null(result.Room.AlignmentRange!.Min);
        Assert.Equal(-500, result.Room.AlignmentRange!.Max);
        Assert.Equal("The land itself rejects you.", result.Room.AlignmentBlockMessage);
    }

    [Fact]
    public void LoadRoom_NoAlignmentRange_LeavesFieldNull()
    {
        var yaml = @"
id: 'example-pack:town-square'
name: 'Town Square'
description: 'A square.'
";
        var result = YamlContentLoader.LoadRoom(yaml);
        Assert.Null(result.Room.AlignmentRange);
    }
}
