using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tapestry.Authoring;
using Tapestry.Engine.Authoring;
using Tapestry.Engine.Recommend;
using Xunit;

namespace Tapestry.Engine.Tests.Oracle;

public class PackRecommendTests
{
    private sealed class FakeLlm : ILlmClient
    {
        public string LastSystem = "", LastUser = "";
        public Task<LlmResult> CompleteAsync(string system, string user, LlmOptions opts, string? responseSchema = null, CancellationToken ct = default)
        {
            LastSystem = system;
            LastUser = user;
            return Task.FromResult(new LlmResult("  the “Hollow” Stag  ", 0, 0)); // smart quotes + padding
        }
    }

    private static RecommendLlmConfig Config() =>
        new(Enabled: true, UseStub: false, BaseUrl: "http://localhost:11434/v1", Model: "qwen2.5:7b",
            ApiKey: "", RequiresKey: false, Temperature: 0.8, MaxSentences: 2, Candidates: 1, TimeoutSeconds: 30);

    private static LlmRecommendProvider ProviderWithFakeLlm(FakeLlm llm) =>
        new(llm, new RoomPromptBuilder(RecommendPromptConfig.Default), new AreaPromptBuilder(RecommendPromptConfig.Default), Config());

    [Fact]
    public void Builder_Stitches_ProjectedContext_AndPackTemplate()
    {
        var ctx = new RoomData
        {
            Area = "rotting-forest",
            AreaName = "The Rotting Forest",
            AreaTheme = "wet decay",
            Biome = "swamp",
        };
        ctx.Neighbors.Add(new RoomNeighbor { Direction = "north", Name = "Black Mire", Biome = "swamp", Description = "Sucking mud." });
        var builder = new RoomPromptBuilder(RecommendPromptConfig.Default);

        var (system, user) = builder.Build("description", ctx,
            packTemplate: "Task: describe this {biome} clearing. Theme hint: {themeHint}.",
            packSystem: "ORACLE-VOICE",
            vars: new Dictionary<string, string> { ["biome"] = "swamp", ["themeHint"] = "rot" });

        Assert.Equal("ORACLE-VOICE", system);                 // pack owns the system voice
        Assert.Contains("Black Mire", user);                  // engine projected the neighbor (load-bearing reuse)
        Assert.Contains("Task: describe this swamp clearing", user); // pack template, vars substituted
        Assert.Contains("Theme hint: rot", user);
    }

    [Fact]
    public async Task Provider_RoutesPackRoomContext_AndSanitizesToAscii()
    {
        var llm = new FakeLlm();
        var provider = ProviderWithFakeLlm(llm);
        var ctx = new PackRoomContext
        {
            Room = new RoomData { Biome = "swamp" },
            Template = "Task: name this {biome} beast.",
            System = "ORACLE-VOICE",
            Vars = new Dictionary<string, string> { ["biome"] = "swamp" },
        };

        var result = await provider.RecommendAsync(new RecommendRequest("name", ctx));

        Assert.Single(result.Suggestions);
        var s = result.Suggestions[0];
        Assert.DoesNotContain('“', s); // smart quotes gone
        Assert.DoesNotContain('”', s);
        Assert.Equal(s.Trim(), s);
        Assert.All(s, c => Assert.True(c < 128, "must be 7-bit ASCII"));
    }
}
