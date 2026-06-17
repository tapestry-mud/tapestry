using Tapestry.Engine.Help;
using Xunit;
using FluentAssertions;

namespace Tapestry.Engine.Tests.Help;

public class CategoryLoaderTests
{
    [Fact]
    public void Categories_KeepDeclarationOrder_ThenPackLoadOrder()
    {
        var svc = new HelpService();
        // core declares movement then combat (loadOrder 0)
        svc.RegisterCategory("movement", "Movement", false, "tapestry-core", 0);
        svc.RegisterCategory("combat", "Combat", false, "tapestry-core", 0);
        // a later pack appends crafting (loadOrder 1)
        svc.RegisterCategory("crafting", "Crafting", false, "some-pack", 1);

        svc.DeclaredCategoryIds.Should().ContainInOrder("movement", "combat", "crafting");
    }

    [Fact]
    public void Category_Hidden_IsReported_DefaultIsListed()
    {
        var svc = new HelpService();
        svc.RegisterCategory("communication", "Communication", false, "tapestry-core", 0);
        svc.RegisterCategory("social", "Socials", true, "tapestry-core", 0);

        svc.IsCategoryHidden("social").Should().BeTrue();
        svc.IsCategoryHidden("communication").Should().BeFalse();
        // an undeclared category is not "hidden" - it is simply unknown (the category gate handles that).
        svc.IsCategoryHidden("nope").Should().BeFalse();
    }
}
