using System.Text.Json;

namespace FootNote.Core;

/// <summary>
/// Shared (de)serialization for both backends. Reads three formats:
///   1. current: {"history":[{"text":"…","at":"…"}]}
///   2. legacy v1: {"Text":"…","ModifiedUtc":"…"} — wrapped as a one-entry history
///   3. plain text (hand-written stream/file) — wrapped as a one-entry history
/// Always writes format 1.
/// </summary>
public static class NoteFormat
{
    public static string Serialize(NoteHistory history) =>
        JsonSerializer.Serialize(history, FootNoteJsonContext.Default.NoteHistory);

    public static NoteHistory? Parse(string raw, DateTime fallbackTimestampUtc)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        if (raw.TrimStart().StartsWith('{'))
        {
            try
            {
                using var doc = JsonDocument.Parse(raw);
                if (doc.RootElement.TryGetProperty("history", out _))
                {
                    var h = JsonSerializer.Deserialize(raw, FootNoteJsonContext.Default.NoteHistory);
                    if (h is not null && h.History.Count > 0) return h;
                    return null;
                }
                if (doc.RootElement.TryGetProperty("Text", out _))
                {
                    var legacy = JsonSerializer.Deserialize(raw, FootNoteJsonContext.Default.Note);
                    if (legacy is not null && legacy.Text.Length > 0)
                    {
                        var h = new NoteHistory();
                        h.Append(legacy.Text, legacy.ModifiedUtc);
                        return h;
                    }
                    return null;
                }
            }
            catch (JsonException) { /* fall through: treat as plain text */ }
        }

        var plain = new NoteHistory();
        plain.Append(raw, fallbackTimestampUtc);
        return plain;
    }
}
