# Generates the radial floor buttons: UIAtlases\UIAtlas\ui_game_symbol_hslift_<label>.png
# Labels: A-Z, 0-99, B1-B9 (must match HSLiftFloorMenu.Icon).
Add-Type -AssemblyName System.Drawing
$out = Join-Path $PSScriptRoot "..\UIAtlases\UIAtlas"
New-Item -ItemType Directory -Force -Path $out | Out-Null
Get-ChildItem $out -Filter "ui_game_symbol_hslift_*.png" | Remove-Item

$labels = @()
$labels += [char[]](65..90) | ForEach-Object { [string]$_ }
$labels += 0..99 | ForEach-Object { [string]$_ }
$labels += 1..9 | ForEach-Object { "B$_" }

$size = 128
foreach ($label in $labels) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear([System.Drawing.Color]::Transparent)

    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), 7
    $g.DrawEllipse($pen, 6, 6, $size - 12, $size - 12)

    $emSize = if ($label.Length -eq 1) { 66 } else { 50 }
    $font = New-Object System.Drawing.Font "Arial", $emSize, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
    $fmt = New-Object System.Drawing.StringFormat
    $fmt.Alignment = [System.Drawing.StringAlignment]::Center
    $fmt.LineAlignment = [System.Drawing.StringAlignment]::Center
    $rect = New-Object System.Drawing.RectangleF 0, 3, $size, $size
    $g.DrawString($label, $font, [System.Drawing.Brushes]::White, $rect, $fmt)

    $bmp.Save((Join-Path $out "ui_game_symbol_hslift_$label.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $font.Dispose(); $pen.Dispose(); $g.Dispose(); $bmp.Dispose()
}
Write-Host "$($labels.Count) icons -> $out"
