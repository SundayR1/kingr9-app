# ============================================================
#  R9 Settings - Interface Settings + Adapter Power
#  แท็บ Network: Interface Settings + Adapter Power (ต่อยอดไฟล์ 01/04)
#  แกะมาจาก R9 Settings.exe
# ============================================================
#  ⚠️ ต้องรัน PowerShell ด้วยสิทธิ์ Admin
#  ⚠️ ชุดนี้รีสตาร์ทการ์ดเน็ต → เชื่อมต่อหลุดชั่วคราว
#  แก้ตัวแปร $adapterName เป็นชื่อการ์ดของคุณก่อนรัน
# ============================================================

# เลือก adapter ตัวแรกที่ Up — ต้องมีทั้ง Description (ใช้กับ NetAdapter cmdlets)
# และ Alias/ชื่อเชื่อมต่อ + InterfaceIndex (ใช้กับ NetIPInterface cmdlets / netsh)
$adapter    = Get-NetAdapter -Physical | Where-Object Status -eq 'Up' | Select-Object -First 1
$adapterName = $adapter.InterfaceDescription   # เช่น "Realtek Gaming 2.5GbE Family Controller"
$ifAlias     = $adapter.Name                   # เช่น "Ethernet" (ใช้กับ netsh / NetIPInterface)
$ifIndex     = $adapter.InterfaceIndex
Write-Host "=== Interface Settings + Adapter Power: $adapterName ($ifAlias) ===" -ForegroundColor Cyan

# ╔═══════════════════════════════════════════════════════════╗
# ║  ส่วนที่ 1: Interface Settings (netsh + Set-NetIPInterface)║
# ╚═══════════════════════════════════════════════════════════╝

$mtu       = 1472   # ขนาด packet สูงสุด (1472 = 1500 - 28 header, ไม่ fragment)
$hop       = 64     # Default Hop Limit (TTL)
$ecn       = 'disabled'
$advRoute  = 'disabled'   # AdvertiseDefaultRoute
$advertising = 'disabled' # Advertising
$metric    = 'enabled'    # AutomaticMetric (ให้ Windows เลือก route เอง)
$mss       = 'enabled'    # ClampMss (ตัดขนาด MSS ให้พอดี MTU)
$forwarding  = 'disabled' # Forwarding (ส่งต่อ packet — PC ปกติไม่ต้อง)
$ignoreRoute = 'disabled' # IgnoreDefaultRoutes
$routerDisc  = 'disabled' # RouterDiscovery
$baseReach   = 15         # BaseReachableTime (วินาที ก่อน re-check neighbor)
$retrans     = 3000       # RetransmitTime (มิลลิวินาที ก่อนส่งซ้ำ)

# MTU ระดับ subinterface (persistent = จำถาวรหลังรีบูต) — netsh ใช้ชื่อ alias
netsh int ip set subinterface "$ifAlias" mtu=$mtu store=persistent
# Hop limit ระดับ global IPv4
netsh int ipv4 set global defaultcurhoplimit=$hop
# ECN ระดับ global TCP
netsh int tcp set global ecncapability=$ecn

# คุณสมบัติ interface ที่เหลือผ่าน cmdlet ทีเดียวครบ
# (Set-NetIPInterface ใช้ InterfaceIndex — หน่วยเวลาเป็นมิลลิวินาทีแบบ ...Ms)
Set-NetIPInterface -InterfaceIndex $ifIndex -AddressFamily IPv4 `
    -AdvertiseDefaultRoute ($advRoute  -eq 'enabled') `
    -Advertising          ($advertising -eq 'enabled') `
    -AutomaticMetric      ($metric    -eq 'enabled') `
    -ClampMss             ($mss       -eq 'enabled') `
    -Forwarding           ($forwarding -eq 'enabled') `
    -IgnoreDefaultRoutes  ($ignoreRoute -eq 'enabled') `
    -RouterDiscovery      ($routerDisc -eq 'enabled') `
    -BaseReachableTimeMs $baseReach -RetransmitTimeMs $retrans `
    -ErrorAction SilentlyContinue
Write-Host "  ✓ MTU=$mtu Hop=$hop ECN=$ecn + interface params" -ForegroundColor Green

# ╔═══════════════════════════════════════════════════════════╗
# ║  ส่วนที่ 2: Adapter Power (Registry ระดับ driver)        ║
# ║  หา registry path ของการ์ดจาก NetCfgInstanceId อัตโนมัติ ║
# ╚═══════════════════════════════════════════════════════════╝

$guid = (Get-NetAdapter -InterfaceDescription "$adapterName" -ErrorAction SilentlyContinue).InterfaceGuid
$sub = Get-ChildItem -Path "HKLM:\SYSTEM\CurrentControlSet\Control\Class\{4D36E972-E325-11CE-BFC1-08002bE10318}" -ErrorAction SilentlyContinue |
    Where-Object { $_.PSChildName -match '^\d{4}$' } |
    Where-Object { (Get-ItemProperty $_.PSPath -Name "NetCfgInstanceId" -ErrorAction SilentlyContinue).NetCfgInstanceId -eq $guid } |
    Select-Object -ExpandProperty PSChildName

if ($sub) {
    $ap = "HKLM:\SYSTEM\CurrentControlSet\Control\Class\{4D36E972-E325-11CE-BFC1-08002bE10318}\$sub"
    # ค่า power ของการ์ด — ตั้ง 0 = ปิดฟีเจอร์ประหยัดไฟทั้งหมด (latency ต่ำสุด)
    Set-ItemProperty -Path $ap -Name 'EnablePME'                   -Value 0 -Force -ErrorAction SilentlyContinue  # Wake on PME
    Set-ItemProperty -Path $ap -Name '*EnableConnectedPowerGating' -Value 0 -Force -ErrorAction SilentlyContinue
    Set-ItemProperty -Path $ap -Name '*EnableDynamicPowerGating'   -Value 0 -Force -ErrorAction SilentlyContinue
    Set-ItemProperty -Path $ap -Name 'AutoPowerSaveModeEnabled'    -Value 0 -Force -ErrorAction SilentlyContinue
    Set-ItemProperty -Path $ap -Name '*NicAutoPowerSaver'          -Value 0 -Force -ErrorAction SilentlyContinue
    Set-ItemProperty -Path $ap -Name 'DisableDelayedPowerUp'       -Value 0 -Force -ErrorAction SilentlyContinue
    Set-ItemProperty -Path $ap -Name 'ReduceSpeedOnPowerDown'      -Value 0 -Force -ErrorAction SilentlyContinue
    Write-Host "  ✓ Adapter Power keys = 0 (ปิด power saving ระดับ driver)" -ForegroundColor Green

    # ─── รีสตาร์ทการ์ดให้ค่ามีผล (เน็ตหลุดแป๊บนึง) ───
    Restart-NetAdapter -InterfaceDescription "$adapterName" -Confirm:$false -ErrorAction SilentlyContinue
    Write-Host "  ✓ รีสตาร์ท adapter แล้ว" -ForegroundColor Green
} else {
    Write-Host "  ✗ หา registry key ของ adapter ไม่เจอ" -ForegroundColor Red
}

Write-Host "`n✅ Interface + Adapter Power เสร็จสมบูรณ์!" -ForegroundColor Green