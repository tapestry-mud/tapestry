using System.Collections.Generic;
using Tapestry.Authoring;
using Tapestry.Engine.Authoring;
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

    private static RoomData CastleContext()
    {
        var ctx = new RoomData
        {
            Id = "castle:hall",
            Area = "castle",
            Biome = "stone",
            Tags = new List<string> { "indoor" },
        };
        ctx.Exits["north"] = new ExitData { Target = "castle:gate" };
        ctx.Exits["south"] = new ExitData { Target = "castle:courtyard" }; // dug but unnamed (no neighbor entry)
        ctx.Neighbors.Add(new RoomNeighbor
        {
            Direction = "north", Id = "castle:gate", Name = "Castle Gate", Biome = "stone",
            Description = "A heavy iron portcullis bars the way, its bars slick with cold dew."
        });
        return ctx;
    }

    [Fact]
    public void Builder_description_user_prompt_includes_structure_intent_and_constraint()
    {
        var builder = new RoomPromptBuilder(RecommendPromptConfig.Default);
        var (system, user) = builder.Build("description", CastleContext(), "a hallway leading to the castle gate");

        Assert.Equal(RecommendPromptConfig.Default.SystemPrompt, system);
        Assert.Contains("Area: castle.", user);
        Assert.Contains("Biome: stone.", user);
        Assert.Contains("north -> \"Castle Gate\"", user);   // resolved name
        Assert.Contains("south -> (unnamed)", user);          // dug, not yet named
        Assert.Contains("Castle Gate", user);                 // neighbor flavor present
        Assert.Contains("Intent: a hallway leading to the castle gate.", user);
        Assert.Contains("Output only the description.", user); // description task line
        Assert.Contains("Constraint: <=2 sentences.", user);   // sentence cap (MaxSentences=2)
    }

    [Fact]
    public void Builder_omits_intent_line_when_hint_is_null()
    {
        var builder = new RoomPromptBuilder(RecommendPromptConfig.Default);
        var (_, user) = builder.Build("description", CastleContext(), null);
        Assert.DoesNotContain("Intent:", user);
    }

    [Fact]
    public void Builder_name_uses_name_task_line_and_no_sentence_constraint()
    {
        var builder = new RoomPromptBuilder(RecommendPromptConfig.Default);
        var (_, user) = builder.Build("name", CastleContext(), "the great hall");

        Assert.Contains("Output only the name.", user);
        Assert.DoesNotContain("Constraint: <=", user); // sentence cap is description-only
    }

    [Fact]
    public void Builder_name_includes_existing_description_as_sibling_context()
    {
        var ctx = CastleContext(); // Name defaults to ""; set the sibling field
        ctx.Description = "A long corridor of cold grey stone stretches toward a heavy gate.";
        var builder = new RoomPromptBuilder(RecommendPromptConfig.Default);
        var (_, user) = builder.Build("name", ctx, null);

        Assert.Contains("Description: A long corridor of cold grey stone stretches toward a heavy gate.", user);
    }

    [Fact]
    public void Builder_description_includes_existing_name_as_sibling_context()
    {
        var ctx = CastleContext(); // Description defaults to ""; set the sibling field
        ctx.Name = "The Gate Approach";
        var builder = new RoomPromptBuilder(RecommendPromptConfig.Default);
        var (_, user) = builder.Build("description", ctx, "a hall");

        Assert.Contains("Name: The Gate Approach", user);
    }

    [Fact]
    public void Builder_includes_neighbor_description_snippet_and_guidance()
    {
        var builder = new RoomPromptBuilder(RecommendPromptConfig.Default);
        var (_, user) = builder.Build("description", CastleContext(), null);

        Assert.Contains("Castle Gate", user);
        Assert.Contains("A heavy iron portcullis bars the way", user);     // neighbor snippet present
        Assert.Contains(RecommendPromptConfig.DefaultNeighborGuidance, user); // stay-on-theme guidance present
    }

    [Fact]
    public void Builder_truncates_long_neighbor_description_to_first_sentence()
    {
        var ctx = new RoomData { Id = "a:1", Area = "a" };
        ctx.Exits["east"] = new ExitData { Target = "a:2" };
        ctx.Neighbors.Add(new RoomNeighbor
        {
            Direction = "east", Id = "a:2", Name = "Hall",
            Description = "Neon light floods the dance floor. A second sentence that must not appear at all."
        });
        var builder = new RoomPromptBuilder(RecommendPromptConfig.Default);
        var (_, user) = builder.Build("description", ctx, null);

        Assert.Contains("Neon light floods the dance floor.", user);
        Assert.DoesNotContain("must not appear", user);
    }

    [Fact]
    public void Builder_omits_guidance_when_no_neighbor_has_a_description()
    {
        var ctx = new RoomData { Id = "a:1", Area = "a" };
        ctx.Exits["east"] = new ExitData { Target = "a:2" };
        ctx.Neighbors.Add(new RoomNeighbor { Direction = "east", Id = "a:2", Name = "Plain Room" }); // no Description
        var builder = new RoomPromptBuilder(RecommendPromptConfig.Default);
        var (_, user) = builder.Build("description", ctx, null);

        Assert.Contains("Plain Room", user);
        Assert.DoesNotContain(RecommendPromptConfig.DefaultNeighborGuidance, user);
    }
}
