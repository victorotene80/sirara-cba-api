namespace SiraraCBA.BuildingBlocks.Application.Abstractions;

/// <summary>
/// The only source of identifiers in Domain and Application code (ER-7).
/// </summary>
/// <remarks>
/// Identifiers are UUIDv7: time-ordered, so they cluster in an index instead of
/// scattering writes across a large B-tree the way v4 does. That difference is
/// measurable on a table the size of <c>journal_lines</c>.
/// </remarks>
public interface IIdGenerator
{
    /// <summary>A new time-ordered identifier.</summary>
    public Guid NewId();
}
