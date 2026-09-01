using System.Text.Json.Serialization;

namespace FootNote.Core;

/// <summary>Latest comment on a file — the DTO the UI works with.
/// Also matches the legacy v1 single-note storage format for backward reads.</summary>
public sealed class Note
{
    public string Text { get; set; } = "";
    public DateTime ModifiedUtc { get; set; }
}

/// <summary>One entry in a file's comment history.</summary>
public sealed class NoteEntry
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = "";

    [JsonPropertyName("at")]
    public DateTime At { get; set; }
}

/// <summary>
/// What both storage backends actually persist: a small capped version history,
/// so nothing is silently overwritten and cloud-sync conflicts are recoverable.
/// The UI only shows/edits the newest entry for now; the format is ready for a
/// "view history" feature without a storage migration.
/// </summary>
public sealed class NoteHistory
{
    public const int MaxEntries = 20;

    [JsonPropertyName("history")]
    public List<NoteEntry> History { get; set; } = new();

    [JsonIgnore]
    public NoteEntry? Latest => History.Count > 0 ? History[^1] : null;

    public void Append(string text, DateTime atUtc)
    {
        History.Add(new NoteEntry { Text = text, At = atUtc });
        while (History.Count > MaxEntries) History.RemoveAt(0); // drop oldest
    }
}
