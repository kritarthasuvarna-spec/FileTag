namespace FileTag.Core.Models;

public class Note
{
    public string FileKey    { get; set; } = "";
    public string FilePath   { get; set; } = "";
    public string Text       { get; set; } = "";
    public string ColorTag   { get; set; } = "";
    public DateTime UpdatedAt { get; set; }
}
