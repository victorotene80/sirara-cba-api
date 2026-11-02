using SiraraCBA.BuildingBlocks.Application.Abstractions;

namespace SiraraCBA.BuildingBlocks.Infrastructure.Identifiers;

/// <summary>
/// The one place in the system permitted to generate identifiers (ER-7).
/// </summary>
/// <remarks>
/// UUIDv7 embeds a timestamp in its high bits, so generated ids are monotonically
/// ordered. Rows written close in time land close in the index instead of being
/// scattered the way v4 scatters them — which matters on a partitioned,
/// append-only table that only ever grows.
/// </remarks>
public sealed class UuidV7IdGenerator : IIdGenerator
{
    public Guid NewId() => Guid.CreateVersion7();
}
