using System.Text.Json.Serialization;

namespace FootNote.Core;

/// <summary>Source-generated JSON (trim-safe for the self-contained uninstaller).</summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Note))]
[JsonSerializable(typeof(NoteEntry))]
[JsonSerializable(typeof(NoteHistory))]
[JsonSerializable(typeof(IndexStore.IndexData))]
[JsonSerializable(typeof(NotesBackup.Entry))]
[JsonSerializable(typeof(List<NotesBackup.Entry>))]
public sealed partial class FootNoteJsonContext : JsonSerializerContext
{
}
