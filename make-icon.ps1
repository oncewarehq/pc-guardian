# Draws the PC Guardian shield icon and saves it as guardian.ico
Add-Type -AssemblyName System.Drawing

$bmp = New-Object System.Drawing.Bitmap 256, 256
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)

# Shield outline
$shield = New-Object System.Drawing.Drawing2D.GraphicsPath
$shield.AddPolygon([System.Drawing.Point[]]@(
    (New-Object System.Drawing.Point 128, 12),
    (New-Object System.Drawing.Point 238, 54),
    (New-Object System.Drawing.Point 238, 128),
    (New-Object System.Drawing.Point 214, 186),
    (New-Object System.Drawing.Point 128, 246),
    (New-Object System.Drawing.Point 42, 186),
    (New-Object System.Drawing.Point 18, 128),
    (New-Object System.Drawing.Point 18, 54)
))

$grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    (New-Object System.Drawing.Point 0, 0),
    (New-Object System.Drawing.Point 256, 256),
    [System.Drawing.Color]::FromArgb(0, 214, 165),
    [System.Drawing.Color]::FromArgb(30, 110, 210))
$g.FillPath($grad, $shield)

$edge = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(18, 22, 30)), 8
$edge.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
$g.DrawPath($edge, $shield)

# Checkmark
$check = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), 26
$check.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$check.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
$check.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
$g.DrawLines($check, [System.Drawing.Point[]]@(
    (New-Object System.Drawing.Point 76, 132),
    (New-Object System.Drawing.Point 114, 172),
    (New-Object System.Drawing.Point 184, 92)
))
$g.Dispose()

# Wrap the PNG in an ICO container (Vista+ supports PNG-compressed icons)
$ms = New-Object System.IO.MemoryStream
$bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
$png = $ms.ToArray()

$ico = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $ico
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]1)   # ICO header, 1 image
$w.Write([Byte]0); $w.Write([Byte]0)                            # 256x256 encoded as 0
$w.Write([Byte]0); $w.Write([Byte]0)                            # no palette, reserved
$w.Write([UInt16]1); $w.Write([UInt16]32)                       # planes, bpp
$w.Write([UInt32]$png.Length); $w.Write([UInt32]22)             # size, offset
$w.Write($png)
[System.IO.File]::WriteAllBytes("$PSScriptRoot\guardian.ico", $ico.ToArray())
$w.Close(); $bmp.Dispose()
"guardian.ico created: $((Get-Item "$PSScriptRoot\guardian.ico").Length) bytes"
