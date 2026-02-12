namespace MacroRecorder.Core.Domain;

public sealed record HistoryDelta(Guid ActionId, string Field, string? Before, string? After);

public sealed class HistoryEntry
{
    public Guid EntryId { get; init; } = Guid.NewGuid();
    public long Version { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string Summary { get; init; } = string.Empty;
    public IReadOnlyList<HistoryDelta> Deltas { get; init; } = Array.Empty<HistoryDelta>();
}
