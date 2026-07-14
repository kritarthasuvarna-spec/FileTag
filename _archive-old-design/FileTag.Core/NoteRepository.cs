using FileTag.Core.Models;
using Microsoft.Data.Sqlite;

namespace FileTag.Core;

public class NoteRepository : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly SqliteCommand _hasNoteCmd;

    public NoteRepository()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FileTag");
        Directory.CreateDirectory(dir);
        var dbPath = Path.Combine(dir, "notes.db");

        _conn = new SqliteConnection($"Data Source={dbPath}");
        try
        {
            _conn.Open();
            InitSchema();
        }
        catch (SqliteException)
        {
            _conn.Dispose();
            File.Delete(dbPath);
            _conn = new SqliteConnection($"Data Source={dbPath}");
            _conn.Open();
            InitSchema();
        }

        _hasNoteCmd = _conn.CreateCommand();
        _hasNoteCmd.CommandText = "SELECT EXISTS(SELECT 1 FROM notes WHERE file_key=@k)";
        _hasNoteCmd.Parameters.Add("@k", SqliteType.Text);
        _hasNoteCmd.Prepare();
    }

    private void InitSchema()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS notes (
                file_key   TEXT PRIMARY KEY,
                file_path  TEXT NOT NULL,
                text       TEXT NOT NULL,
                color_tag  TEXT DEFAULT '',
                updated_at TEXT NOT NULL
            )
            """;
        cmd.ExecuteNonQuery();
    }

    public Note? Get(string fileKey)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT file_key,file_path,text,color_tag,updated_at FROM notes WHERE file_key=@k";
        cmd.Parameters.AddWithValue("@k", fileKey);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new Note
        {
            FileKey    = r.GetString(0),
            FilePath   = r.GetString(1),
            Text       = r.GetString(2),
            ColorTag   = r.GetString(3),
            UpdatedAt  = DateTime.Parse(r.GetString(4))
        };
    }

    public void Upsert(Note note)
    {
        note.UpdatedAt = DateTime.UtcNow;
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO notes (file_key,file_path,text,color_tag,updated_at)
            VALUES (@k,@p,@t,@c,@u)
            """;
        cmd.Parameters.AddWithValue("@k", note.FileKey);
        cmd.Parameters.AddWithValue("@p", note.FilePath);
        cmd.Parameters.AddWithValue("@t", note.Text);
        cmd.Parameters.AddWithValue("@c", note.ColorTag);
        cmd.Parameters.AddWithValue("@u", note.UpdatedAt.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    public void Delete(string fileKey)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "DELETE FROM notes WHERE file_key=@k";
        cmd.Parameters.AddWithValue("@k", fileKey);
        cmd.ExecuteNonQuery();
    }

    public bool HasNote(string fileKey)
    {
        _hasNoteCmd.Parameters["@k"].Value = fileKey;
        return Convert.ToInt64(_hasNoteCmd.ExecuteScalar()!) == 1;
    }

    public List<Note> GetAll()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT file_key,file_path,text,color_tag,updated_at FROM notes";
        using var r = cmd.ExecuteReader();
        var list = new List<Note>();
        while (r.Read())
            list.Add(new Note
            {
                FileKey   = r.GetString(0),
                FilePath  = r.GetString(1),
                Text      = r.GetString(2),
                ColorTag  = r.GetString(3),
                UpdatedAt = DateTime.Parse(r.GetString(4))
            });
        return list;
    }

    public void Dispose()
    {
        _hasNoteCmd.Dispose();
        _conn.Dispose();
    }
}
