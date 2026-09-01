using System.IO;
using System.Reflection;
using System.Text.Json;

namespace FootNote.App;

/// <summary>User-facing patch notes, embedded (never fetched). Separate from
/// the dev-facing CHANGELOG.md by design.</summary>
public static class PatchNotes
{
    public static List<(Version Version, string[] Notes)> Load(Assembly assembly)
    {
        var result = new List<(Version, string[])>();
        try
        {
            string? name = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("PatchNotes.json", StringComparison.OrdinalIgnoreCase));
            if (name is null) return result;
            using var s = assembly.GetManifestResourceStream(name)!;
            using var doc = JsonDocument.Parse(s);
            foreach (var e in doc.RootElement.EnumerateArray())
            {
                if (Version.TryParse(e.GetProperty("version").GetString(), out var v))
                    result.Add((v, e.GetProperty("notes").EnumerateArray()
                        .Select(n => n.GetString() ?? "").ToArray()));
            }
        }
        catch { }
        return result; // newest first, as authored
    }

    /// <summary>Entries newer than <paramref name="after"/> (exclusive); null = all.</summary>
    public static List<(Version Version, string[] Notes)> Load(Assembly assembly, Version? after) =>
        after is null ? Load(assembly) : Load(assembly).Where(e => e.Version > after).ToList();
}
