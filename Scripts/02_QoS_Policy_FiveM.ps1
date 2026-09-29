# ============================================================
#  R9 Settings - QoS Policy Optimization (FiveM / GTA5)
#  สคริปต์ตั้งค่า Quality of Service สำหรับ FiveM
#  แกะมาจาก R9 Settings.exe
# ============================================================
#  ⚠️ ต้องรัน PowerShell ด้วยสิทธิ์ Admin
# ============================================================

Write-Host "=== QoS Policy Optimization (FiveM / GTA5) ===" -ForegroundColor Cyan

# ─── สร้าง QoS Policy สำหรับ FiveM และ GTA5 ───
# QoS (Quality of Service) = ระบบจัดลำดับความสำคัญของ network traffic
#
# DSCP Value = 46 → "Expedited Forwarding" (EF)
#   = ระดับความสำคัญสูงสุด ในมาตรฐาน DiffServ
#   = บอก router/switch ว่า traffic นี้ต้องส่งก่อนอย่างอื่น
#   = ลด latency และ jitter → ดีสำหรับเกมออนไลน์
#
# Throttle Rate = -1 → ไม่จำกัดความเร็ว (unlimited bandwidth)
#
# Do not use NLA = 1 → ไม่ต้องตรวจ Network Location Awareness
#   = ทำให้ QoS policy มีผลทันทีโดยไม่ต้องรอตรวจชนิดเครือข่าย

$policies = @(
    @{ Name = 'fivem';              App = 'FiveM.exe' },
    @{ Name = 'fivem_gtaprocess';   App = 'FiveM_GTAProcess.exe' },
    @{ Name = 'GTA5';               App = 'GTA5.exe' }
)

foreach ($p in $policies) {
    $keyPath = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\QoS\' + $p.Name

    # สร้าง registry key ถ้ายังไม่มี
    if (!(Test-Path $keyPath)) {
        New-Item -Path $keyPath -Force | Out-Null
    }

    # ─── ตั้งค่า QoS Policy ───
    Set-ItemProperty -Path $keyPath -Name 'Version'                -Value '1.0'
    Set-ItemProperty -Path $keyPath -Name 'Application Name'       -Value $p.App      # ชื่อ process ที่จะใช้ policy
    Set-ItemProperty -Path $keyPath -Name 'Protocol'               -Value '*'          # ทุก protocol (TCP, UDP, etc.)
    Set-ItemProperty -Path $keyPath -Name 'Local Port'             -Value '*'          # ทุก port
    Set-ItemProperty -Path $keyPath -Name 'Local IP'               -Value '*'          # ทุก IP
    Set-ItemProperty -Path $keyPath -Name 'Local IP Prefix Length' -Value '*'
    Set-ItemProperty -Path $keyPath -Name 'Remote Port'            -Value '*'          # ทุก port ปลายทาง
    Set-ItemProperty -Path $keyPath -Name 'Remote IP'              -Value '*'          # ทุก IP ปลายทาง
    Set-ItemProperty -Path $keyPath -Name 'Remote IP Prefix Length'-Value '*'
    Set-ItemProperty -Path $keyPath -Name 'DSCP Value'             -Value '46'         # 46 = Expedited Forwarding (สูงสุด)
    Set-ItemProperty -Path $keyPath -Name 'Throttle Rate'          -Value '-1'         # -1 = ไม่จำกัดความเร็ว

    Write-Host "  ✓ สร้าง QoS Policy: $($p.Name) → $($p.App) [DSCP=46, No throttle]" -ForegroundColor Green
}

# ─── ตั้ง "Do not use NLA" ───
# NLA = Network Location Awareness
# ปิดการใช้ NLA สำหรับ QoS → policy มีผลทันทีไม่ต้องรอ Windows ระบุชนิดเครือข่าย
$nlaPath = 'HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\QoS'
if (!(Test-Path $nlaPath)) {
    New-Item -Path $nlaPath -Force | Out-Null
}
Set-ItemProperty -Path $nlaPath -Name 'Do not use NLA' -Value '1'
Write-Host "  ✓ Do not use NLA = Enabled" -ForegroundColor Green

# ─── บังคับ Group Policy Update ───
Start-Process gpupdate -ArgumentList '/force' -WindowStyle Hidden -ErrorAction SilentlyContinue

Write-Host "`n✅ QoS Policies ตั้งค่าเสร็จสมบูรณ์!" -ForegroundColor Green
Write-Host ""
Write-Host "สรุป:" -ForegroundColor Yellow
Write-Host "  • FiveM.exe              → DSCP 46 (Expedited Forwarding)"
Write-Host "  • FiveM_GTAProcess.exe   → DSCP 46 (Expedited Forwarding)"
Write-Host "  • GTA5.exe               → DSCP 46 (Expedited Forwarding)"
Write-Host "  • NLA Bypass             → Enabled"
Write-Host ""
Write-Host "⚠️ หมายเหตุ: DSCP marking จะมีผลจริงต่อเมื่อ router/switch ของคุณรองรับ QoS" -ForegroundColor DarkYellow
