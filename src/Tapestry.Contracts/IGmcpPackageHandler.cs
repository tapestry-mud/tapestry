namespace Tapestry.Contracts;

public interface IGmcpPackageHandler
{
    string Name { get; }
    IReadOnlyList<string> PackageNames { get; }
    void Configure();
    void SendBurst(string connectionId, object entity);
}
