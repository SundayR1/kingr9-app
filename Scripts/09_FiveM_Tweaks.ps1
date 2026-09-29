# ============================================================
#  R9 Settings - FiveM Tweaks
#  แท็บ FiveM: Clear Cache / High Priority / GPU / Network / Timer Resolution
#  แกะมาจาก R9 Settings.exe
# ============================================================
#  ⚠️ ต้องรัน PowerShell ด้วยสิทธิ์ Admin
#  ⚠️ ปิด FiveM ก่อนล้าง cache
# ============================================================

Write-Host "=== FiveM Optimization Tweaks ===" -ForegroundColor Cyan

# ╔═══════════════════════════════════════════════════════════╗
# ║  ส่วนที่ 1: Clear Cache                                  ║
# ║  ลบ cache ทั้ง 4 โฟลเดอร์ → โหลด server ใหม่นานขึ้น      ║
# ║  ครั้งแรกหลังล้าง แต่ช่วยแก้ asset เก่าเสีย/ผุด            ║
# ╚═══════════════════════════════════════════════════════════╝

Remove-Item -Path "$env:LOCALAPPDATA\FiveM\FiveM.app\cache",
                   "$env:LOCALAPPDATA\FiveM\FiveM.app\data\cache",
                   "$env:LOCALAPPDATA\FiveM\FiveM.app\data\server-cache",
                   "$env:LOCALAPPDATA\FiveM\FiveM.app\data\nui-storage" `
    -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "  ✓ ล้าง FiveM cache ทั้ง 4 โฟลเดอร์แล้ว" -ForegroundColor Green

# ╔═══════════════════════════════════════════════════════════╗
# ║  ส่วนที่ 2: High Priority (CPU Priority Class = 3)       ║
# ║  Image File Execution Options\<exe>\PerfOptions          ║
# ║  CpuPriorityClass: 3 = High (ตั้งเปิด) / 2 = Normal      ║
# ║  → ให้ Windows จัดสรร CPU ให้เกมก่อน process อื่น        ║
# ╚═══════════════════════════════════════════════════════════╝

$targets = @('FiveM.exe', 'FiveM_GTAProcess.exe', 'GTA5.exe')
foreach ($exe in $targets) {
    $p = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\$exe\PerfOptions"
    if (!(Test-Path $p)) { New-Item -Path $p -Force -ErrorAction SilentlyContinue | Out-Null }
    Set-ItemProperty -Path $p -Name 'CpuPriorityClass' -Value 3 -Type DWord -Force -ErrorAction SilentlyContinue
    Write-Host "  ✓ $exe → CpuPriorityClass = 3 (High)" -ForegroundColor Green
}

# ╔═══════════════════════════════════════════════════════════╗
# ║  ส่วนที่ 3: CEF Hardware Acceleration                    ║
# ║  HKCU:\Software\CitizenFX\FiveM → CEFHardwareAcceleration║
# ║  0 = ปิด HW accel ของเบราว์เซอร์ในตัว (แก้ UI กระตุก)   ║
# ║  1 = เปิด (ค่าเริ่มต้น) — toggle ในแอปสลับระหว่าง 0/1     ║
# ╚═══════════════════════════════════════════════════════════╝

$p2 = 'HKCU:\Software\CitizenFX\FiveM'
if (!(Test-Path $p2)) { New-Item -Path $p2 -Force -ErrorAction SilentlyContinue | Out-Null }
Set-ItemProperty -Path $p2 -Name 'CEFHardwareAcceleration' -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
Write-Host "  ✓ CEFHardwareAcceleration = 0 (ปิด HW accel ของ CEF)" -ForegroundColor Green

# ╔═══════════════════════════════════════════════════════════╗
# ║  ส่วนที่ 4: Network System Profile                       ║
# ║  Multimedia\SystemProfile:                               ║
# ║  NetworkThrottlingIndex = 0xFFFFFFFF (4294967295)        ║
# ║    → ปิด network throttling (ปกติ Windows หน่วงเน็ต       ║
# ║      เป็นรอบ 17ms เมื่อมี media playback)                 ║
# ║  SystemResponsiveness = 0                                ║
# ║    → ไม่สงวน CPU 20% ให้ background multimedia           ║
# ╚═══════════════════════════════════════════════════════════╝

reg add "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile" /v NetworkThrottlingIndex /t REG_DWORD /d 4294967295 /f 2>$null
reg add "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile" /v SystemResponsiveness    /t REG_DWORD /d 0 /f 2>$null
Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile" -Name "NetworkThrottlingIndex" -Value 0xFFFFFFFF -Type DWord -Force -ErrorAction SilentlyContinue
Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile" -Name "SystemResponsiveness"   -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
Write-Host "  ✓ NetworkThrottlingIndex = 0xFFFFFFFF / SystemResponsiveness = 0" -ForegroundColor Green

# ╔═══════════════════════════════════════════════════════════╗
# ║  ส่วนที่ 5: Timer Resolution Service (STR) — low-latency ║
# ║  สร้าง Windows service ที่เรียก NtSetTimerResolution      ║
# ║  บังคับ timer resolution 0.5ms (5000 หน่วย 100ns)        ║
# ║  ตลอดเวลา → ลด input delay / frametime jitter            ║
# ║  (สคริปต์ต้นฉบับเต็มอยู่ใน str_service_source.ps1.txt)   ║
# ╚═══════════════════════════════════════════════════════════╝

# 5.1 ลบ service เดิมถ้ามี (กันซ้ำ)
if (Get-Service -Name "STR" -ErrorAction SilentlyContinue) {
    Stop-Service -Name "STR" -Force -ErrorAction SilentlyContinue
    sc.exe delete "STR" 2>$null
}
if (Get-Service -Name "Set Timer Resolution Service" -ErrorAction SilentlyContinue) {
    Stop-Service -Name "Set Timer Resolution Service" -Force -ErrorAction SilentlyContinue
}
sc.exe delete "Set Timer Resolution Service" 2>$null
Start-Sleep 1

# 5.2 เขียนโค้ด C# ของ service ลง ProgramData
$csPath  = "$env:ProgramData\SetTimerResolutionService.cs"
$exePath = "$env:ProgramData\SetTimerResolutionService.exe"

@'
using System;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;
namespace TimerSvc {
    public class TimerService : ServiceBase {
        [DllImport("ntdll.dll")]
        static extern int NtSetTimerResolution(uint DesiredResolution, bool SetResolution, out uint CurrentResolution);
        [DllImport("ntdll.dll")]
        static extern int NtQueryTimerResolution(out uint MinimumResolution, out uint MaximumResolution, out uint CurrentResolution);
        Thread _worker;
        public TimerService() { ServiceName = "Set Timer Resolution Service"; }
        static void Main() { ServiceBase.Run(new TimerService()); }
        protected override void OnStart(string[] args) {
            _worker = new Thread(new ThreadStart(Worker));
            _worker.IsBackground = true;
            _worker.Start();
        }
        protected override void OnStop() { }
        void Worker() {
            uint min = 0, max = 0, cur = 0;
            NtQueryTimerResolution(out min, out max, out cur);
            uint target = 5000; // 0.5ms (5000 x 100ns) — ตรงๆ ไม่ขึ้นกับ hardware
            if (target < max) target = max; // fallback: ถ้า hardware ไม่รองรับ 5000 ใช้ max
            NtSetTimerResolution(target, true, out cur);   // ตั้ง 0.5ms (5000 x 100ns) ตรงๆ — fallback เป็น max ถ้าไม่รองรับ
            System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite);
        }
    }
}
'@ | Out-File -FilePath $csPath -Encoding UTF8 -Force

# 5.3 คอมไพล์ด้วย csc.exe ที่มากับ .NET Framework
$cscPaths = @(
    "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
    "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe",
    "C:\Windows\Microsoft.NET\Framework64\v3.5\csc.exe",
    "C:\Windows\Microsoft.NET\Framework\v3.5\csc.exe"
)
$csc = $cscPaths | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $csc) {
    $csc = Get-ChildItem "$env:SystemRoot\Microsoft.NET" -Filter "csc.exe" -Recurse -ErrorAction SilentlyContinue |
           Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
}
if ($csc) {
    & $csc -nologo /r:System.dll /r:System.ServiceProcess.dll -out:"$exePath" "$csPath"
    Remove-Item $csPath -ErrorAction SilentlyContinue

    # 5.4 สร้าง + เริ่ม service
    sc.exe create "Set Timer Resolution Service" binPath= "$exePath" start= auto obj= LocalSystem DisplayName= "Set Timer Resolution Service"
    sc.exe description "Set Timer Resolution Service" "Maintains 0.5ms system timer resolution for low-latency performance"
    sc.exe start "Set Timer Resolution Service"

    # 5.5 ให้ app ทั่วไปขอ timer resolution ละเอียดได้ + ค่า 0.5ms — ไม่บังคับ platform clock (HPET) กันหน่วง
    Set-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\kernel' -Name 'GlobalTimerResolutionRequests' -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue
    Set-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\kernel' -Name 'TimerResolution' -Value 5000 -Type DWord -Force -ErrorAction SilentlyContinue
    bcdedit /deletevalue useplatformclock 2>$null
    Write-Host "  ✓ ติดตั้ง STR service (0.5ms timer) เรียบร้อย" -ForegroundColor Green
} else {
    Write-Host "  ✗ หา csc.exe ไม่เจอ — ต้องมี .NET Framework" -ForegroundColor Red
}

# ─── หมายเหตุ: ถ้าอยาก "ถอน" STR (โหมด default ของแอป) ให้รันชุดนี้ ───
# sc.exe delete "STR"; sc.exe delete "Set Timer Resolution Service"
# Remove-Item "$env:ProgramData\SetTimerResolutionService.exe" -Force
# Remove-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\kernel' -Name 'GlobalTimerResolutionRequests' -EA SilentlyContinue

Write-Host "`n✅ FiveM Tweaks เสร็จสมบูรณ์!" -ForegroundColor Green
