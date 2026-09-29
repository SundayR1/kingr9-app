param(
  [string]$InputPath = 'c:\Users\Administrator\Desktop\KingR9Tools\logo_raw.png',
  [string]$Root = 'c:\Users\Administrator\Desktop\KingR9Tools',
  [double]$Corner = 0.22
)
Add-Type -AssemblyName System.Drawing

if (-not (Test-Path $InputPath)) {
  Write-Output "File not found: $InputPath - put your generated image at logo_raw.png or use -InputPath"
  exit 1
}
# backup เดิม (ครั้งเดียว)
if (-not (Test-Path "$Root\logo_old.png"))   { Copy-Item "$Root\logo.png"   "$Root\logo_old.png" -ErrorAction SilentlyContinue }
if (-not (Test-Path "$Root\KingR9_old.ico")) { Copy-Item "$Root\KingR9.ico" "$Root\KingR9_old.ico" -ErrorAction SilentlyContinue }

function Round-Image([System.Drawing.Image]$src, [int]$S){
  # crop กลางเป็นจัตุรัส → ย่อ S → ตัดมุมโค้ง (นอกโค้งโปร่งใส)
  $bmp = New-Object System.Drawing.Bitmap($S, $S)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.InterpolationMode  = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.PixelOffsetMode    = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  $g.Clear([System.Drawing.Color]::Transparent)

  $side = [Math]::Min($src.Width, $src.Height)
  $sx = [int](($src.Width - $side) / 2)
  $sy = [int](($src.Height - $side) / 2)

  $path = New-Object System.Drawing.Drawing2D.GraphicsPath
  $d = [float]($S * (2.0 * $Corner))
  $path.AddArc($S-$d, 0, $d, $d, 270, 90)
  $path.AddArc($S-$d, $S-$d, $d, $d, 0, 90)
  $path.AddArc(0, $S-$d, $d, $d, 90, 90)
  $path.AddArc(0, 0, $d, $d, 180, 90)
  $path.CloseFigure()
  $g.SetClip($path)

  $dst = [System.Drawing.RectangleF]::new([float]0,[float]0,[float]$S,[float]$S)
  $srcR = [System.Drawing.RectangleF]::new([float]$sx,[float]$sy,[float]$side,[float]$side)
  $g.DrawImage($src, $dst, $srcR, [System.Drawing.GraphicsUnit]::Pixel)
  $g.Dispose()
  return $bmp
}

# ---- โหลดรูปต้นฉบับ ----
$srcImg = [System.Drawing.Image]::FromFile($InputPath)

# ---- logo.png (1024, มุมโค้งโปร่งใส) ----
$out = Round-Image $srcImg 1024
$out.Save("$Root\logo.png", [System.Drawing.Imaging.ImageFormat]::Png)
$out.Dispose()
Write-Output 'logo.png OK (1024)'

# ---- KingR9.ico 6 ขนาด (PNG-compressed entries) ----
$sizes = @(256,128,64,48,32,16)
$blobs = @()
foreach($sz in $sizes){
  $b = Round-Image $srcImg $sz
  $ms = New-Object System.IO.MemoryStream
  $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
  $b.Dispose()
  $blobs += ,@{ Len = [int]$ms.Length; Data = $ms.ToArray(); Size = $sz }
  $ms.Dispose()
}
$srcImg.Dispose()

$stream = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($stream)
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
foreach($e in $blobs){
  $dim = $(if($e.Size -ge 256){0}else{$e.Size})
  $bw.Write([Byte]$dim); $bw.Write([Byte]$dim)
  $bw.Write([Byte]0);    $bw.Write([Byte]0)
  $bw.Write([UInt16]1);  $bw.Write([UInt16]32)
  $bw.Write([UInt32]$e.Len); $bw.Write([UInt32]$offset)
  $offset += $e.Len
}
foreach($e in $blobs){ $bw.Write($e.Data) }
$bw.Flush()
[System.IO.File]::WriteAllBytes("$Root\KingR9.ico", $stream.ToArray())
$bw.Dispose(); $stream.Dispose()
Write-Output 'KingR9.ico OK (256/128/64/48/32/16)'
Write-Output 'Done - run BUILD.bat to build a new exe with the new logo'
