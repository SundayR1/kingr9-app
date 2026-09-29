# ============================================================
#  R9 Settings - Disable Background Services (20 ตัว)
#  กลุ่ม "Windows OS Services Tweaks" → Services
#  แกะมาจาก R9 Settings.exe (tweak-services)
# ============================================================
#  ⚠️ ต้องรัน PowerShell ด้วยสิทธิ์ Admin
#  ⚠️ ปิดถาวร — เปิดกลับได้ด้วย:  sc config <ชื่อ> start=auto  (+ start service)
# ============================================================

Write-Host "=== Disable Telemetry / Bloatware Services (20) ===" -ForegroundColor Cyan

# รายชื่อ + เหตุผลทีละตัว
$services = @(
    @{ Name='SysMain';                                  Note='Superfetch — preload app ลง RAM ล่วงหน้า กินดิสก์/RAM' },
    @{ Name='DiagTrack';                                Note='Connected User Experiences & Telemetry — ส่งข้อมูลการใช้งานให้ Microsoft' },
    @{ Name='dmwappushservice';                         Note='WAP Push Message Routing — telemetry channel อีกตัว' },
    @{ Name='MapsBroker';                               Note='Downloaded Maps Manager — อัปเดตแผนที่ (ไม่จำเป็นบน PC เกม)' },
    @{ Name='WSearch';                                  Note='Windows Search — index ไฟล์ตลอดเวลา กินดิสก์ I/O สูง' },
    @{ Name='WerSvc';                                   Note='Windows Error Reporting — ส่งรายงาน crash' },
    @{ Name='RemoteRegistry';                           Note='ให้เครื่องอื่นแก้ registry ได้จากข้างนอก (ควรปิดเสมอ)' },
    @{ Name='Fax';                                      Note='Fax service — ไม่ใช้แล้วยุคนี้' },
    @{ Name='RetailDemo';                               Note='โหมดเครื่องโชว์หน้าร้าน' },
    @{ Name='WMPNetworkSvc';                            Note='Windows Media Player Network Sharing — แชร์มีเดียข้ามเครือข่าย' },
    @{ Name='PhoneSvc';                                 Note='Phone Dialer / telephony — ไม่ใช้' },
    @{ Name='AJRouter';                                 Note='AllJoyn Router — IoT protocol ที่แทบไม่มีใครใช้' },
    @{ Name='lfsvc';                                    Note='Geolocation Service — ระบุตำแหน่งเครื่อง' },
    @{ Name='PimIndexMaintenanceSvc';                   Note='Contact/Personal Information Index — telemetry ผูกกับ Contacts' },
    @{ Name='BcastDVRUserService';                      Note='Game DVR / Game Bar background record — กิน GPU/CPU' },
    @{ Name='diagnosticshub.standardcollector.service'; Note='Diagnostics Hub Collector — เก็บข้อมูล diagnostic' },
    @{ Name='SSDPSRV';                                  Note='SSDP Discovery — ค้นหาอุปกรณ์ UPnP บนเน็ตเวิร์ก' },
    @{ Name='upnphost';                                 Note='UPnP Device Host — คู่กับ SSDPSRV' },
    @{ Name='TrkWks';                                   Note='Distributed Link Tracking Client — ตามลิงก์ไฟล์ที่ย้าย (NTFS)' },
    @{ Name='TabletInputService';                       Note='Touch Keyboard / Handwriting — ไม่จำเป็นถ้าไม่ใช้จอสัมผัส' }
)

foreach ($svc in $services) {
    Write-Host ("  ปิด: {0,-42} # {1}" -f $svc.Name, $svc.Note) -ForegroundColor DarkGray
    # หยุด service ทันที (ถ้ากำลังรัน) แล้วตั้ง startup type = disabled ถาวร
    sc.exe stop $svc.Name 2>$null | Out-Null
    sc.exe config $svc.Name start=disabled 2>$null | Out-Null
}

Write-Host "`n✅ ปิด Services เรียบร้อย 20 ตัว!" -ForegroundColor Green
Write-Host "   เปิดกลับ: sc config <ชื่อ> start=auto แล้ว sc start <ชื่อ>" -ForegroundColor Yellow