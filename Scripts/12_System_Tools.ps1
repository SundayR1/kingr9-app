# ============================================================
#  R9 Settings - System Tools
#  Tools: Flush DNS / Winsock Reset / Junk Cleaner / Restore Point
#         + Restore Defaults (ถอน tweak ทั้งหมด)
#  แกะมาจาก R9 Settings.exe
# ============================================================
#  ⚠️ ต้องรัน PowerShell ด้วยสิทธิ์ Admin
#  ใช้: .\12_System_Tools.ps1 -Action dns|winsock|junk|restorept|defaults|all
# ============================================================

param([string]$Action = "all")

function Flush-DnsCache {
    Write-Host "--- Flush DNS Cache ---" -ForegroundColor Cyan
    # แอปใช้ 2 วิธี: เรียก DnsFlushResolverCache ผ่าน dnsapi.dll โดยตรง
    # และสำรองด้วย ipconfig /flushdns
    ipconfig /flushdns | Out-Null
    Write-Host "  ✓ ล้าง DNS resolver cache แล้ว" -ForegroundColor Green
}

function Reset-Winsock {
    Write-Host "--- Winsock Reset ---" -ForegroundColor Cyan
    # ล้าง Winsock catalog กลับค่าเริ่มต้น (แก้เน็ตเพี้ยนจาก LSP แปลกปลอม)
    # ⚠️ ต้องรีสตาร์ทเครื่องหลังรัน
    netsh winsock reset
    Write-Host "  ✓ Winsock reset แล้ว — ต้องรีสตาร์ทเครื่อง" -ForegroundColor Green
}

function Clean-Junk {
    Write-Host "--- Junk Cleaner ---" -ForegroundColor Cyan
    # หมวด temp: %TEMP% + C:\Windows\Temp
    Remove-Item -Path "$env:TEMP\*", "C:\Windows\Temp\*" -Recurse -Force -ErrorAction SilentlyContinue
    # หมวด log: C:\Windows\Logs
    Remove-Item -Path "C:\Windows\Logs\*" -Recurse -Force -ErrorAction SilentlyContinue
    # หมวด cache: INetCache + ThumbCache
    Remove-Item -Path "$env:LOCALAPPDATA\Microsoft\Windows\INetCache\*" -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -Path "$env:LOCALAPPDATA\Microsoft\Windows\Explorer\thumbcache_*.db" -Force -ErrorAction SilentlyContinue
    # หมวด bin: เทถังรีไซเคิล (เงียบๆ ไม่ถาม)
    Clear-RecycleBin -Force -ErrorAction SilentlyContinue
    Write-Host "  ✓ ล้างไฟล์ขยะ 4 หมวดแล้ว (temp/log/cache/bin)" -ForegroundColor Green
}

function New-RestorePoint {
    Write-Host "--- Create Restore Point ---" -ForegroundColor Cyan
    # เปิด System Restore บน C: แล้วสร้างจุด restore ก่อนแตะระบบ
    Enable-ComputerRestore -Drive "C:\" -ErrorAction SilentlyContinue
    Checkpoint-Computer -Description "R9SettingsBackup" -RestorePointType MODIFY_SETTINGS -ErrorAction SilentlyContinue
    Write-Host "  ✓ สร้าง restore point 'R9SettingsBackup' แล้ว" -ForegroundColor Green
}

function Restore-Defaults {
    Write-Host "--- Restore Defaults (ถอน tweak ทั้งหมด) ---" -ForegroundColor Yellow
    # 1) Multimedia SystemProfile กลับค่าเริ่มต้น (10 / 20)
    Set-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile" -Name "NetworkThrottlingIndex" -Value 10 -Type DWord -Force -ErrorAction SilentlyContinue
    Set-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile" -Name "SystemResponsiveness"  -Value 20 -Type DWord -Force -ErrorAction SilentlyContinue
    # 2) ลบ latency registry keys ที่ตั้งไว้
    Remove-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" -Name "TcpAckFrequency","TCPNoDelay","TcpDelAckTicks","DisableTaskOffload" -ErrorAction SilentlyContinue
    Get-ChildItem "HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces" -ErrorAction SilentlyContinue |
        ForEach-Object { Remove-ItemProperty -Path $_.PSPath -Name "TcpAckFrequency","TCPNoDelay","TcpDelAckTicks" -ErrorAction SilentlyContinue }
    Remove-ItemProperty "HKLM:\SOFTWARE\Microsoft\MSMQ\Parameters" -Name "TCPNoDelay" -ErrorAction SilentlyContinue
    Remove-ItemProperty "HKLM:\SOFTWARE\Policies\Microsoft\Windows\Psched" -Name "NonBestEffortLimit" -ErrorAction SilentlyContinue
    Remove-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Services\Dnscache\Parameters" -Name "MaxCacheTtl","MaxNegativeCacheTtl" -ErrorAction SilentlyContinue
    # 3) ลบ priority ของเกม + QoS policies + NLA
    Remove-Item "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\FiveM.exe" -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\FiveM_GTAProcess.exe" -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\GTA5.exe" -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item "HKLM:\SOFTWARE\Policies\Microsoft\Windows\QoS" -Recurse -Force -ErrorAction SilentlyContinue
    Remove-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\QoS" -Name "Do not use NLA" -ErrorAction SilentlyContinue
    # 4) ถอน Timer Resolution service
    if (Get-Service "Set Timer Resolution Service" -ErrorAction SilentlyContinue) {
        Stop-Service "Set Timer Resolution Service" -Force -ErrorAction SilentlyContinue
        sc.exe delete "Set Timer Resolution Service" 2>$null
    }
    if (Get-Service "STR" -ErrorAction SilentlyContinue) {
        Stop-Service "STR" -Force -ErrorAction SilentlyContinue
        sc.exe delete "STR" 2>$null
    }
    Remove-Item "$env:ProgramData\SetTimerResolutionService.exe" -Force -ErrorAction SilentlyContinue
    Remove-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\kernel" -Name "GlobalTimerResolutionRequests","TimerResolution" -ErrorAction SilentlyContinue
    # 5) ลบ config เกมที่แอปเขียนไว้
    Remove-Item "$env:APPDATA\CitizenFX\gta5_settings.xml" -Force -ErrorAction SilentlyContinue
    Remove-Item "$env:LOCALAPPDATA\FiveM\FiveM.app\CitizenFX.ini" -Force -ErrorAction SilentlyContinue
    # 6) Reset network + power plan + BCD กลับค่าเริ่มต้น
    netsh int ip reset
    netsh winsock reset
    powercfg -restoredefaultschemes
    powercfg /setactive 381b4222-f694-41f0-9685-ff5bb260df2e   # Balanced
    bcdedit /deletevalue disabledynamictick 2>$null
    bcdedit /deletevalue useplatformtick 2>$null
    bcdedit /deletevalue useplatformclock 2>$null
    gpupdate /force 2>$null | Out-Null
    Write-Host "  ✓ คืนค่าเริ่มต้นทั้งหมดแล้ว — แนะนำรีสตาร์ทเครื่อง" -ForegroundColor Green
}

# ─── เลือกทำงานตาม -Action ───
switch ($Action.ToLower()) {
    "dns"       { Flush-DnsCache }
    "winsock"   { Reset-Winsock }
    "junk"      { Clean-Junk }
    "restorept" { New-RestorePoint }
    "defaults"  { Restore-Defaults }
    "all"       { Flush-DnsCache; Clean-Junk; New-RestorePoint }
    default     { Write-Host "ใช้: .\12_System_Tools.ps1 -Action dns|winsock|junk|restorept|defaults|all" -ForegroundColor Yellow }
}
