# Generates Assets\footnote_demo.gif — a short mock animation of the FootNote
# flow (select file -> hotkey -> bar slides up -> type -> saved). Pure GDI+,
# no external tools. Animated GIF via GDI+ multiframe SaveAdd.
Add-Type -AssemblyName System.Drawing

$W = 640; $H = 360
$out = Join-Path $PSScriptRoot '..\FootNote.App\Assets\footnote_demo.gif'
New-Item -ItemType Directory -Force (Split-Path $out) | Out-Null

function New-Frame {
    param([int]$BarOffset, [string]$TypedText, [string]$Mode, [bool]$HotkeyChip)
    $bmp = New-Object System.Drawing.Bitmap($W, $H)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'ClearTypeGridFit'
    # backdrop + mock explorer window
    $g.Clear([System.Drawing.Color]::FromArgb(255,32,34,45))
    $g.FillRectangle([System.Drawing.Brushes]::White, 40, 30, 560, 250)
    $g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255,240,242,247))), 40, 30, 560, 34)
    $fT = New-Object System.Drawing.Font('Segoe UI', 10)
    $fB = New-Object System.Drawing.Font('Segoe UI', 10, [System.Drawing.FontStyle]::Bold)
    $ink = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255,40,42,55))
    $g.DrawString('Documents', $fB, $ink, 52, 37)
    # file rows; second row selected
    $rows = @('budget.xlsx', 'report.docx', 'photo.jpg')
    for ($i = 0; $i -lt 3; $i++) {
        $y = 80 + $i * 34
        if ($i -eq 1) { $g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255,205,225,252))), 48, $y - 4, 544, 28) }
        $g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255,79,142,247))), 56, $y, 16, 18)
        $g.DrawString($rows[$i], $fT, $ink, 82, $y)
    }
    if ($HotkeyChip) {
        $g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(235,40,42,55))), 210, 150, 220, 44)
        $g.DrawString('Shift + Alt + N', (New-Object System.Drawing.Font('Segoe UI', 14, [System.Drawing.FontStyle]::Bold)),
            [System.Drawing.Brushes]::White, 248, 160)
    }
    # overlay bar (slides from bottom; BarOffset px below resting position; 999 = hidden)
    if ($BarOffset -lt 200) {
        $barY = 288 + $BarOffset
        $bar = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(250,30,30,43))
        $g.FillRectangle($bar, 90, $barY, 460, 60)
        $g.DrawString('📝 report.docx', (New-Object System.Drawing.Font('Segoe UI', 9, [System.Drawing.FontStyle]::Bold)),
            [System.Drawing.Brushes]::White, 102, ($barY + 6))
        if ($Mode -eq 'edit') {
            $g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255,61,61,77))), 102, ($barY + 26), 340, 24)
            $g.DrawString($TypedText, $fT, [System.Drawing.Brushes]::White, 106, ($barY + 29))
            $g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255,79,142,247))), 452, ($barY + 26), 56, 24)
            $g.DrawString('Save', $fT, [System.Drawing.Brushes]::White, 462, ($barY + 29))
        } else {
            $g.DrawString($TypedText, $fT, (New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255,200,203,215))), 102, ($barY + 29))
        }
    }
    $g.Dispose()
    return $bmp
}

$frames = @()
$frames += ,@((New-Frame 999 '' 'read' $false), 90)                     # plain explorer
$frames += ,@((New-Frame 999 '' 'read' $true), 110)                     # hotkey chip
foreach ($o in 60, 30, 10, 0) { $frames += ,@((New-Frame $o '' 'edit' $false), 6) }  # slide up
$txt = 'Final version - sent to client'
for ($i = 6; $i -le $txt.Length; $i += 6) { $frames += ,@((New-Frame 0 $txt.Substring(0, [Math]::Min($i, $txt.Length)) 'edit' $false), 22) }
$frames += ,@((New-Frame 0 $txt 'edit' $false), 60)
$frames += ,@((New-Frame 0 "$txt   (saved)" 'read' $false), 140)        # saved read mode

# --- write animated GIF via GDI+ multiframe ---
$first = $frames[0][0]
# frame delays property (PropertyTagFrameDelay 0x5100), hundredths of a second
$delayBytes = New-Object byte[] (4 * $frames.Count)
for ($i = 0; $i -lt $frames.Count; $i++) { [BitConverter]::GetBytes([int]$frames[$i][1]).CopyTo($delayBytes, $i * 4) }
function New-PropItem([int]$Id, [int16]$Type, [byte[]]$Value) {
    $p = [System.Runtime.Serialization.FormatterServices]::GetUninitializedObject([System.Drawing.Imaging.PropertyItem])
    $p.Id = $Id; $p.Type = $Type; $p.Value = $Value; $p.Len = $Value.Length
    return $p
}
$first.SetPropertyItem((New-PropItem 0x5100 4 $delayBytes))       # frame delays
$first.SetPropertyItem((New-PropItem 0x5101 3 ([byte[]](0,0))))   # loop forever

$enc = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object MimeType -eq 'image/gif'
$ep = New-Object System.Drawing.Imaging.EncoderParameters(1)
$ep.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter([System.Drawing.Imaging.Encoder]::SaveFlag, [long][System.Drawing.Imaging.EncoderValue]::MultiFrame)
$first.Save($out, $enc, $ep)
$epAdd = New-Object System.Drawing.Imaging.EncoderParameters(1)
$epAdd.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter([System.Drawing.Imaging.Encoder]::SaveFlag, [long][System.Drawing.Imaging.EncoderValue]::FrameDimensionTime)
for ($i = 1; $i -lt $frames.Count; $i++) { $first.SaveAdd($frames[$i][0], $epAdd) }
$epEnd = New-Object System.Drawing.Imaging.EncoderParameters(1)
$epEnd.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter([System.Drawing.Imaging.Encoder]::SaveFlag, [long][System.Drawing.Imaging.EncoderValue]::Flush)
$first.SaveAdd($epEnd)
$frames | ForEach-Object { $_[0].Dispose() }
"gif written: $out ($([math]::Round((Get-Item $out).Length/1KB)) KB, $($frames.Count) frames)"
