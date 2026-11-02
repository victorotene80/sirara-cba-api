using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SiraraCBA.BuildingBlocks.Infrastructure.Modules;

/// <summary>
/// A bounded context's self-registration.
/// </summary>
/// <remarks>
/// The host discovers modules and calls these two methods; it knows nothing else
/// about any of them. Extraction is then moving the projects and giving them their
/// own host — no other module and no shared file changes (ER-6).
/// </remarks>
public interface IModule
{
    /// <summary>Stable module name, also its database schema name.</summary>
    public string Name { get; }

    /// <summary>Registers everything this module needs. Called once at startup.</summary>
    public void RegisterServices(IServiceCollection services, IConfiguration configuration);

    /// <summary>Maps this module's HTTP endpoints. The host adds no routes of its own.</summary>
    public void MapEndpoints(IEndpointRouteBuilder endpoints);
}
