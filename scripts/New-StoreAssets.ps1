param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
foreach ($asset in @(@('Square44x44Logo.png',44),@('Square150x150Logo.png',150),@('StoreLogo.png',50),@('ListingIcon300.png',300))) {
    $size = [int]$asset[1]
    $bitmap = [Drawing.Bitmap]::new($size,$size)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $outline = [Drawing.Pen]::new([Drawing.Color]::FromArgb(53,75,93),5)
    $grid = [Drawing.Pen]::new([Drawing.Color]::FromArgb(214,213,199),1)
    $fill = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(247,246,236))
    $accent = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(85,128,104))
    try {
        $graphics.Clear([Drawing.Color]::FromArgb(230,228,214))
        $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.ScaleTransform($size/150.0,$size/150.0)
        foreach ($point in @(18,42,66,90,114,138)) { $graphics.DrawLine($grid,$point,0,$point,150); $graphics.DrawLine($grid,0,$point,150,$point) }
        $graphics.DrawLines($outline,[Drawing.Point[]]@([Drawing.Point]::new(43,37),[Drawing.Point]::new(112,37),[Drawing.Point]::new(112,76),[Drawing.Point]::new(43,76),[Drawing.Point]::new(43,114)))
        foreach ($box in @(@(28,22),@(97,61),@(28,99))) {
            $rectangle = [Drawing.Rectangle]::new([int]$box[0],[int]$box[1],30,30)
            $graphics.FillRectangle($fill,$rectangle); $graphics.DrawRectangle($outline,$rectangle)
        }
        $graphics.FillRectangle($accent,34,28,18,18)
        $graphics.FillRectangle($accent,103,67,18,18)
        $graphics.FillRectangle($accent,34,105,18,18)
        $bitmap.Save((Join-Path $OutputDirectory $asset[0]),[Drawing.Imaging.ImageFormat]::Png)
    } finally { $graphics.Dispose(); $bitmap.Dispose(); $outline.Dispose(); $grid.Dispose(); $fill.Dispose(); $accent.Dispose() }
}
