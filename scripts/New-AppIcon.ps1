# Package the approved transparent PNG as a multi-resolution Windows icon.
param(
    [string]$Source = "$PSScriptRoot/../assets/branding/wysicraft-wc-icon.png",
    [string]$Destination = "$PSScriptRoot/../assets/branding/wysicraft.ico"
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$original = [System.Drawing.Image]::FromFile([IO.Path]::GetFullPath($Source))
try {
    $frames = foreach ($size in @(16, 24, 32, 48, 64, 128, 256)) {
        $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $stream = [IO.MemoryStream]::new()
        try {
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.DrawImage($original, [System.Drawing.Rectangle]::new(0, 0, $size, $size))
            $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            @{ Size = $size; Bytes = $stream.ToArray() }
        } finally { $stream.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
    }
    $output = [IO.File]::Create([IO.Path]::GetFullPath($Destination))
    $writer = [IO.BinaryWriter]::new($output)
    try {
        $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
        $offset = 6 + 16 * $frames.Count
        foreach ($frame in $frames) {
            $dimension = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
            $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
            $writer.Write([byte]0); $writer.Write([byte]0)
            $writer.Write([uint16]1); $writer.Write([uint16]32)
            $writer.Write([uint32]$frame.Bytes.Length); $writer.Write([uint32]$offset)
            $offset += $frame.Bytes.Length
        }
        foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Bytes) }
    } finally { $writer.Dispose(); $output.Dispose() }
} finally { $original.Dispose() }
