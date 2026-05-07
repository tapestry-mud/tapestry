namespace Tapestry.Contracts;

public interface IGameModule
{
    string Name { get; }
    void Configure();
}
