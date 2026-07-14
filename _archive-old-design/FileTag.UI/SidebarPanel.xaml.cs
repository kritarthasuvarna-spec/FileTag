using FileTag.Core;
using FileTag.Core.Models;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FileTag.UI;

public partial class SidebarPanel : UserControl
{
    private static readonly NoteRepository _repo = RepositoryLocator.Notes;

    private string _filePath = "";
    private string _fileKey  = "";
    private Note?  _note;
    private bool   _editMode;

    public SidebarPanel() => InitializeComponent();

    public void Load(string filePath)
    {
        _filePath = filePath;
        _fileKey  = FileKeyHelper.GetKey(filePath);
        _note     = _repo.Get(_fileKey);

        PopulateFileInfo();
        CheckRelink();

        if (_note == null)
            ShowWrite(preload: "");
        else
            ShowRead();
    }

    // ── File metadata ──────────────────────────────────────────────────

    private void PopulateFileInfo()
    {
        TxtFileName.Text = Path.GetFileName(_filePath);

        try
        {
            var info = new FileInfo(_filePath);
            TxtFileType.Text = GetFriendlyType(_filePath);
            TxtSize.Text     = FormatSize(info.Length);
            TxtDate.Text     = info.LastWriteTime.ToString("d");
        }
        catch
        {
            TxtFileType.Text = TxtSize.Text = TxtDate.Text = "—";
        }
    }

    private static string FormatSize(long bytes) =>
        bytes >= 1_048_576 ? $"{bytes / 1_048_576.0:F1} MB"
        : bytes >= 1024    ? $"{bytes / 1024.0:F1} KB"
        : $"{bytes} B";

    private static string GetFriendlyType(string path)
    {
        var ext = Path.GetExtension(path).ToLower();
        return ext switch
        {
            ".xlsx" or ".xls" => "Excel Spreadsheet",
            ".docx" or ".doc" => "Word Document",
            ".pdf"            => "PDF Document",
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" => "Image",
            ".txt"            => "Text Document",
            ".zip"            => "ZIP Archive",
            ".mp4" or ".avi" or ".mkv" => "Video",
            ".mp3" or ".wav" or ".flac" => "Audio",
            _                 => $"{ext.TrimStart('.').ToUpper()} File"
        };
    }

    // ── Relink ─────────────────────────────────────────────────────────

    private void CheckRelink()
    {
        if (_note != null && _note.FilePath != _filePath)
            PanelRelink.Visibility = Visibility.Visible;
        else
            PanelRelink.Visibility = Visibility.Collapsed;
    }

    private void BtnRelink_Click(object sender, RoutedEventArgs e)
    {
        if (_note == null) return;
        _note.FilePath = _filePath;
        _repo.Upsert(_note);
        PanelRelink.Visibility = Visibility.Collapsed;
    }

    // ── State transitions ───────────────────────────────────────────────

    private void ShowRead()
    {
        _editMode = false;
        PanelRead.Visibility  = Visibility.Visible;
        PanelWrite.Visibility = Visibility.Collapsed;

        TxtComment.Text   = _note!.Text;
        TxtTimestamp.Text = $"Edited {_note.UpdatedAt.ToLocalTime():d MMM yyyy}";
    }

    private void ShowWrite(string preload)
    {
        _editMode = true;
        PanelRead.Visibility  = Visibility.Collapsed;
        PanelWrite.Visibility = Visibility.Visible;

        TxtInput.Text    = preload;
        TxtError.Visibility = Visibility.Collapsed;
        UpdateCounter();

        Dispatcher.BeginInvoke(() =>
        {
            TxtInput.Focus();
            TxtInput.CaretIndex = TxtInput.Text.Length;
        });
    }

    private void BtnModify_Click(object sender, RoutedEventArgs e) =>
        ShowWrite(_note?.Text ?? "");

    private void BtnSave_Click(object sender, RoutedEventArgs e) => Save();

    private void BtnDiscard_Click(object sender, RoutedEventArgs e)
    {
        if (_note == null) ShowWrite("");
        else ShowRead();
    }

    private void Save()
    {
        var text = TxtInput.Text.Trim();
        if (string.IsNullOrEmpty(text))
        {
            TxtError.Text       = "Comment cannot be empty";
            TxtError.Visibility = Visibility.Visible;
            return;
        }

        _note = new Note
        {
            FileKey  = _fileKey,
            FilePath = _filePath,
            Text     = text,
            ColorTag = _note?.ColorTag ?? ""
        };
        _repo.Upsert(_note);
        ShowRead();
    }

    // ── Keyboard & helpers ──────────────────────────────────────────────

    private void TxtInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { BtnDiscard_Click(sender, new RoutedEventArgs()); e.Handled = true; }
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control) { Save(); e.Handled = true; }
    }

    private void TxtInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        TxtPlaceholder.Visibility = TxtInput.Text.Length == 0
            ? Visibility.Visible : Visibility.Collapsed;
        TxtError.Visibility = Visibility.Collapsed;
        UpdateCounter();
    }

    private void UpdateCounter() =>
        TxtCounter.Text = $"{TxtInput.Text.Length} / 500";
}
