Add-Type -AssemblyName System.Drawing
$root = 'c:\Users\Administrator\Desktop\KingR9Tools'

# backup old assets (once)
if (-not (Test-Path "$root\logo_old.png"))    { Copy-Item "$root\logo.png"    "$root\logo_old.png" }
if (-not (Test-Path "$root\KingR9_old.ico"))  { Copy-Item "$root\KingR9.ico"  "$root\KingR9_old.ico" }

function New-LogoBitmap([int]$S){
  $bmp = New-Object System.Drawing.Bitmap($S, $S)
  $g   = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
  $g.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  $g.Clear([System.Drawing.Color]::Transparent)

  # ---- badge path (rounded square, transparent corners) ----
  $path = New-Object System.Drawing.Drawing2D.GraphicsPath
  $d = [float]($S * 0.37)
  $path.AddArc($S-$d, 0, $d, $d, 270, 90)
  $path.AddArc($S-$d, $S-$d, $d, $d, 0, 90)
  $path.AddArc(0, $S-$d, $d, $d, 90, 90)
  $path.AddArc(0, 0, $d, $d, 180, 90)
  $path.CloseFigure()

  # ---- background: violet -> blue diagonal gradient ----
  $rect = [System.Drawing.RectangleF]::new([float]0,[float]0,[float]$S,[float]$S)
  $c1 = [System.Drawing.Color]::FromArgb(255,139,92,246)   # violet
  $c2 = [System.Drawing.Color]::FromArgb(255,37,99,235)    # blue
  $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, $c1, $c2, [float]38.0)
  $g.FillPath($bg, $path)
  $g.SetClip($path)

  # ---- soft white glow on top (depth) ----
  $ep = New-Object System.Drawing.Drawing2D.GraphicsPath
  $ep.AddEllipse([float]($S*-0.15), [float]($S*-0.42), [float]($S*1.3), [float]($S*0.95))
  $pgb = New-Object System.Drawing.Drawing2D.PathGradientBrush($ep)
  $pgb.CenterColor    = [System.Drawing.Color]::FromArgb(70,255,255,255)
  $pgb.SurroundColors = @([System.Drawing.Color]::FromArgb(0,255,255,255))
  $g.FillPath($pgb, $ep)

  # ---- dark shade at bottom (depth) ----
  $shadeRect = [System.Drawing.RectangleF]::new([float]0,[float]($S*0.45),[float]$S,[float]($S*0.55))
  $shade = New-Object System.Drawing.Drawing2D.LinearGradientBrush($shadeRect,
             [System.Drawing.Color]::FromArgb(0,8,6,30),
             [System.Drawing.Color]::FromArgb(90,8,6,30), [float]90.0)
  $g.FillRectangle($shade, $shadeRect)

  # ---- diagonal shine (glassy sweep) ----
  if ($S -ge 64){
    $shp = New-Object System.Drawing.Drawing2D.GraphicsPath
    $w = [float]($S*0.16)
    $shp.AddPolygon([System.Drawing.PointF[]]@(
      [System.Drawing.PointF]::new([float]($S*0.05),[float]($S*0.55)),
      [System.Drawing.PointF]::new([float]($S*0.55),[float]($S*-0.05)),
      [System.Drawing.PointF]::new([float]($S*0.55+$w),[float]($S*-0.05)),
      [System.Drawing.PointF]::new([float]($S*0.05+$w),[float]($S*0.55))))
    $shineRect = [System.Drawing.RectangleF]::new([float]0,[float]0,[float]$S,[float]$S)
    $shine = New-Object System.Drawing.Drawing2D.LinearGradientBrush($shineRect,
               [System.Drawing.Color]::FromArgb(38,255,255,255),
               [System.Drawing.Color]::FromArgb(0,255,255,255), [float]90.0)
    $g.FillPath($shine, $shp)
    $shp.Dispose(); $shine.Dispose()
  }

  # ---- crown (gold) ----
  $cw   = [float]($S*0.46)
  $cx   = [float]($S*0.5)
  $top  = [float]($S*0.155)
  $base = [float]($S*0.335)
  $cv   = [float]($S*0.075)
  $p = [System.Drawing.PointF[]]@(
    [System.Drawing.PointF]::new([float]($cx-$cw/2), $base),
    [System.Drawing.PointF]::new([float]($cx-$cw/2), [float]($top+$cv)),
    [System.Drawing.PointF]::new([float]($cx-$cw*0.26), [float]($top+$cv*1.85)),
    [System.Drawing.PointF]::new($cx, $top),
    [System.Drawing.PointF]::new([float]($cx+$cw*0.26), [float]($top+$cv*1.85)),
    [System.Drawing.PointF]::new([float]($cx+$cw/2), [float]($top+$cv)),
    [System.Drawing.PointF]::new([float]($cx+$cw/2), $base)
  )
  $cpath = New-Object System.Drawing.Drawing2D.GraphicsPath
  $cpath.AddPolygon($p)
  $goldRect = [System.Drawing.RectangleF]::new([float]($cx-$cw/2), $top, $cw, [float]($base-$top))
  $gold = New-Object System.Drawing.Drawing2D.LinearGradientBrush($goldRect,
            [System.Drawing.Color]::FromArgb(255,255,214,90),
            [System.Drawing.Color]::FromArgb(255,240,158,20), [float]90.0)
  $g.FillPath($gold, $cpath)
  $cpen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(160,120,60,5), [float]([Math]::Max(1.0,$S*0.006)))
  $g.DrawPath($cpen, $cpath)

  # ---- "R9" text ----
  $fam = 'Segoe UI Black'
  try { $null = [System.Drawing.FontFamily]::new($fam) } catch { $fam = 'Segoe UI' }
  try { $null = [System.Drawing.FontFamily]::new($fam) } catch { $fam = 'Arial' }
  $style = [System.Drawing.FontStyle]::Regular
  if ($fam -ne 'Segoe UI Black'){ $style = [System.Drawing.FontStyle]::Bold }
  $font = New-Object System.Drawing.Font($fam, [float]($S*0.40), $style, [System.Drawing.GraphicsUnit]::Pixel)
  $fmt = [System.Drawing.StringFormat]::GenericDefault
  $fmt.Alignment = [System.Drawing.StringAlignment]::Center
  $fmt.LineAlignment = [System.Drawing.StringAlignment]::Center
  $tb = [System.Drawing.RectangleF]::new([float]0,[float]($S*0.36),[float]$S,[float]($S*0.48))

  # soft drop shadow (same rect, tiny offsets — deterministic centering)
  for($i=3; $i -ge 1; $i--){
    $sb = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(22,10,5,40))
    $srect = [System.Drawing.RectangleF]::new([float]0,[float]($S*0.36+$i*$S*0.006),[float]$S,[float]($S*0.48))
    $g.DrawString('R9', $font, $sb, $srect, $fmt) | Out-Null
    $sb.Dispose()
  }
  # main white text (single draw, centered in rect)
  $g.DrawString('R9', $font, [System.Drawing.Brushes]::White, $tb, $fmt) | Out-Null

  # ---- thin inner border ----
  $g.ResetClip()
  $bpen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(70,255,255,255), [float]([Math]::Max(1.0,$S*0.007)))
  $g.DrawPath($bpen, $path)

  $g.Dispose()
  return $bmp
}

# ---- main logo.png (1024) ----
$logo = New-LogoBitmap 1024
$logo.Save("$root\logo.png", [System.Drawing.Imaging.ImageFormat]::Png)
$logo.Dispose()
Write-Output 'logo.png OK'

# ---- KingR9.ico (multi-size, PNG-compressed entries) ----
$sizes = @(256,128,64,48,32,16)
$blobs = @()
foreach($sz in $sizes){
  $b = New-LogoBitmap $sz
  $ms = New-Object System.IO.MemoryStream
  $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
  $b.Dispose()
  $blobs += ,@{ Len = [int]$ms.Length; Data = $ms.ToArray(); Size = $sz }
  $ms.Dispose()
}
$stream = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($stream)
$bw.Write([UInt16]0)      # reserved
$bw.Write([UInt16]1)      # type: icon
$bw.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
foreach($e in $blobs){
  $dim = $(if($e.Size -ge 256){0}else{$e.Size})
  $bw.Write([Byte]$dim)   # width
  $bw.Write([Byte]$dim)   # height
  $bw.Write([Byte]0)      # colors
  $bw.Write([Byte]0)      # reserved
  $bw.Write([UInt16]1)    # planes
  $bw.Write([UInt16]32)   # bitcount
  $bw.Write([UInt32]$e.Len)
  $bw.Write([UInt32]$offset)
  $offset += $e.Len
}
foreach($e in $blobs){ $bw.Write($e.Data) }
$bw.Flush()
[System.IO.File]::WriteAllBytes("$root\KingR9.ico", $stream.ToArray())
$bw.Dispose(); $stream.Dispose()
Write-Output 'KingR9.ico OK'
