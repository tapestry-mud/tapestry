namespace Tapestry.Engine.Help;

public sealed class CategoryDeclaration
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public bool Hidden { get; set; }
}

public sealed class CategoriesFile
{
    public List<CategoryDeclaration> Categories { get; set; } = new();
}
