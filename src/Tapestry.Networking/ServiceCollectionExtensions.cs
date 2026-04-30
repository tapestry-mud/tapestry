using Microsoft.Extensions.DependencyInjection;

namespace Tapestry.Networking;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTapestryNetworking(this IServiceCollection services)
    {
        // TelnetServer needs port from config — registered in Program.cs with factory lambda
        return services;
    }
}
