# =====================================================================
# KingR9 Tools — 13_R9_Powerplan.ps1
# สร้าง + เปิดใช้งาน power plan "R9(FPS+INPUTLAG)" (ค่าตรง optimizer ต้นฉบับ)
# ⚠ ต้องรันด้วยสิทธิ์ Administrator
# =====================================================================

Write-Host '=== R9 Power Plan (FPS + INPUTLAG) ===' -ForegroundColor Cyan

# 1) ถ้ามีแผน R9(FPS+INPUTLAG) อยู่แล้ว → ใช้ GUID เดิมเลย (ไม่ลบแผนไหนทั้งสิ้น)
$g = $null
foreach ($ln in (powercfg /list)) {
  if ($ln -match '([0-9a-fA-F-]{36})\s*\((.*)\)') {
    if ($Matches[2] -match 'R9\(FPS\+INPUTLAG\)') { $g = $Matches[1]; break }
  }
}

# 2) ยังไม่มี → duplicate ใหม่ (ลอง Ultimate template ก่อน ถ้า Windows นี้ไม่มี ใช้ High performance)
if (-not $g) {
  $d = powercfg -duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61 2>$null
  if ("$d" -match '([0-9a-fA-F-]{36})') { $g = $Matches[1] }
}
if (-not $g) {
  $d = powercfg -duplicatescheme 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c 2>$null
  if ("$d" -match '([0-9a-fA-F-]{36})') { $g = $Matches[1] }
}

if ($g) {
  powercfg -changename $g 'R9(FPS+INPUTLAG)' 'Max performance plan tuned by KingR9 (FPS + Inputlag)' *>$null
  powercfg -setactive $g *>$null
  Write-Host '  + สร้างแผน R9(FPS+INPUTLAG) จาก Ultimate Performance แล้ว'

  $sub = '54533251-82be-4824-96c1-47b60b740d00'
  # จอ / sleep / hibernate / disk = Never (0) ทั้ง AC และ DC
  foreach ($t in 'monitor','standby','hibernate','disk') { powercfg -change ($t + '-timeout-ac') 0 *>$null; powercfg -change ($t + '-timeout-dc') 0 *>$null }
  # CPU: min & max processor state = 100%
  powercfg -setacvalueindex $g $sub 893dee8e-2bef-41e0-89c6-b55d0929964c 100 *>$null; powercfg -setdcvalueindex $g $sub 893dee8e-2bef-41e0-89c6-b55d0929964c 100 *>$null
  powercfg -setacvalueindex $g $sub bc5038f7-23e0-4960-96da-33abaf5935ec 100 *>$null; powercfg -setdcvalueindex $g $sub bc5038f7-23e0-4960-96da-33abaf5935ec 100 *>$null
  # Processor boost mode = Aggressive (2) + boost policy = 100%
  powercfg -setacvalueindex $g $sub be337238-0d82-4146-a960-4f3749d470c7 2 *>$null; powercfg -setdcvalueindex $g $sub be337238-0d82-4146-a960-4f3749d470c7 2 *>$null
  powercfg -setacvalueindex $g $sub 45bcc044-d885-43e2-8605-ee0ec6e96b59 100 *>$null; powercfg -setdcvalueindex $g $sub 45bcc044-d885-43e2-8605-ee0ec6e96b59 100 *>$null
  # System cooling policy = Active: เร่งพัดลมก่อนลดความถี่
  powercfg -setacvalueindex $g $sub 94d3a615-a899-4ac5-ae2b-e4d8f634367f 1 *>$null; powercfg -setdcvalueindex $g $sub 94d3a615-a899-4ac5-ae2b-e4d8f634367f 1 *>$null
  # PCI Express ASPM off
  powercfg -setacvalueindex $g 501a4d13-42af-4429-9fd1-a8218c268e20 ee12f906-d277-404b-b6da-e5fa1a576df5 0 *>$null; powercfg -setdcvalueindex $g 501a4d13-42af-4429-9fd1-a8218c268e20 ee12f906-d277-404b-b6da-e5fa1a576df5 0 *>$null
  # USB selective suspend off
  powercfg -setacvalueindex $g 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0 *>$null; powercfg -setdcvalueindex $g 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0 *>$null
  # Wake timers = Disable
  powercfg -setacvalueindex $g 238c9fa8-0aad-41ed-83f4-97be242c8f20 bd3b718a-0680-4d9d-8ab2-e1d2b4ac806d 0 *>$null; powercfg -setdcvalueindex $g 238c9fa8-0aad-41ed-83f4-97be242c8f20 bd3b718a-0680-4d9d-8ab2-e1d2b4ac806d 0 *>$null
  # Adaptive brightness = Off
  powercfg -setacvalueindex $g 7516b95f-f776-4464-8c53-06167f40cc99 fbd9aa66-9553-4097-ba44-ed6e9d65eab8 0 *>$null; powercfg -setdcvalueindex $g 7516b95f-f776-4464-8c53-06167f40cc99 fbd9aa66-9553-4097-ba44-ed6e9d65eab8 0 *>$null
  powercfg -setactive $g *>$null
  Write-Host '  + Timeouts Never · CPU 100% · Boost Aggressive · Cooling Active · PCIe ASPM off · USB suspend off · Wake timer off · Adaptive brightness off' -ForegroundColor Green
}
else { Write-Host '  x duplicate scheme ไม่สำเร็จ' -ForegroundColor Yellow }

# 3) Delivery Optimization off + hibernate off
Write-Host '  + Delivery Optimization off + Hibernate off'
reg add 'HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\DeliveryOptimization\Config' /v DODownloadMode /t REG_DWORD /d 0 /f *>$null
powercfg -h off *>$null

# 4) USB selective suspend off (กันแผนอื่นที่ active ค้าง)
powercfg /setacvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0 *>$null
powercfg /setdcvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0 *>$null
powercfg /setactive SCHEME_CURRENT *>$null

Write-Host '=== เสร็จสมบูรณ์ — แผน R9(FPS+INPUTLAG) พร้อมใช้งาน ===' -ForegroundColor Green