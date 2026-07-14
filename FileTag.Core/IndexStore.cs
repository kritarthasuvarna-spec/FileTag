using System.Text.Json;

namespace FileTag.Core;

/// <summary>
/// Local lookup cache + app config, stored at %LocalAppData%\FileTag\index.json.
/// Tracks which paths currently carry a FileTag comment (paths only, never text).
/// Can go stale, never wrong: comment text always lives in the ADS stream.
/// Fully rebuildable; losing this file loses nothing the user wrote.
/// </summary>
public sealed class IndexStore
{
    public sealed class IndexData
    {
        public bool FirstRunShown { get; set; }
        public bool NonNtfsWarned { get; set; }
        public bool StartWithWindows { get; set; } = true;
        public HashSet<string> Paths { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public static string DataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FileTag");

    public static string IndexFilePath => Path.Combine(DataDirectory, "index.json");

    private readonly object _gate = new();
    private IndexData _data;

    public IndexStore()
    {
        _data = Load();
    }

    private static IndexData Load()
    {
        try
        {
            if (File.Exists(IndexFilePath))
            {
                var data = JsonSerializer.Deserialize(File.ReadAllText(IndexFilePath), FileTagJsonContext.Default.IndexData);
                if (data is not null)
                {
                    data.Paths = new HashSet<string>(data.Paths, StringComparer.OrdinalIgnoreCase);
                    return data;
                }
            }
        }
        catch { /* corrupt/unreadable index is disposable — start fresh */ }
        return new IndexData();
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(DataDirectory);
            File.WriteAllText(IndexFilePath, JsonSerializer.Serialize(_data, FileTagJsonContext.Default.IndexData));
        }
        catch { /* cache only — losing a save is harmless */ }
    }

    public bool FirstRunShown
    {
        get { lock (_gate) return _data.FirstRunShown; }
        set { lock (_gate) { _data.FirstRunShown = value; Save(); } }
    }

    public bool NonNtfsWarned
    {
        get { lock (_gate) return _data.NonNtfsWarned; }
        set { lock (_gate) { _data.NonNtfsWarned = value; Save(); } }
    }

    public bool StartWithWindows
    {
        get { lock (_gate) return _data.StartWithWindows; }
        set { lock (_gate) { _data.StartWithWindows = value; Save(); } }
    }

    public void AddPath(string path)
    {
        lock (_gate) { if (_data.Paths.Add(path)) Save(); }
    }

    public void RemovePath(string path)
    {
        lock (_gate) { if (_data.Paths.Remove(path)) Save(); }
    }

    public IReadOnlyCollection<string> GetPaths()
    {
        lock (_gate) return _data.Paths.ToArray();
    }
}
