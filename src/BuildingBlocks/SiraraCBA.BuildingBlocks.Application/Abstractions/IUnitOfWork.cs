namespace SiraraCBA.BuildingBlocks.Application.Abstractions;

/// <summary>
/// One module's transaction boundary.
/// </summary>
/// <remarks>
/// Deliberately scoped to a single module: there is no unit of work spanning two
/// modules, because a shared transaction is the one change that makes later
/// extraction impossible (ER-3). Cross-module consistency goes through the outbox.
/// </remarks>
public interface IUnitOfWork
{
    /// <summary>
    /// Commits the module's changes, dispatching the aggregates' domain events and
    /// writing any integration events to this module's outbox in the same
    /// transaction.
    /// </summary>
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
