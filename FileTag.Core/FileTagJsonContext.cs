using System.Text.Json.Serialization;

namespace FileTag.Core;

/// <summary>Source-generated JSON (trim-safe for the self-contained uninstaller).</summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Note))]
[JsonSerializable(typeof(NoteEntry))]
[JsonSerializable(typeof(NoteHistory))]
[JsonSerializable(typeof(IndexStore.IndexData))]
public sealed partial class FileTagJsonContext : JsonSerializerContext
{
}
