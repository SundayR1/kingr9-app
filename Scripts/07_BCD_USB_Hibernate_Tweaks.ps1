# ============================================================
#  R9 Settings - BCD / USB / Hibernate Tweaks
#  กลุ่ม "Windows OS Services Tweaks" → BCD + USB
#  กลุ่ม "R9 Power Plan Tweaks" → Hiber/Sleep
#  แกะมาจาก R9 Settings.exe (tweak-bcd + tweak-usb + tweak-hiber-sleep)
# ============================================================
#  ⚠️ ต้องรัน PowerShell ด้วยสิทธิ์ Admin
#  ⚠️ ค่า BCD มีผลหลังรีสตาร์ทเครื่อง
# ============================================================

Write-Host "=== BCD / USB / Hibernate Tweaks ===" -ForegroundColor Cyan

# ╔═══════════════════════════════════════════════════════════╗
# ║  ส่วนที่ 1: BCD (Boot Configuration Data)                ║
# ╚═══════════════════════════════════════════════════════════╝

# disabledynamictick = no → เปิด dynamic tick (ค่าเริ่มต้น Windows — เสถียร ไม่หน่วง)
#   ⚠ การบังคับ "yes" ทำให้เครื่องบางรุ่นหน่วง/stutter หนักมาก (เคยเจอจริง)
#   ใช้ค่า default = "no" ปลอดภัยที่สุด
bcdedit /set disabledynamictick no 2>$null

# useplatformclock → ลบค่านี้ทิ้ง
#   บังคับใช้ HPET เป็นนาฬิกาหลัก (ช้ากว่า TSC) — ลบแล้วให้ Windows เลือกเอง
bcdedit /deletevalue useplatformclock 2>$null

# useplatformtick → ลบค่านี้ทิ้ง (บังคับ platform tick = เสี่ยงหน่วง/stutter บนเครื่องยุคใหม่)
bcdedit /deletevalue useplatformtick 2>$null

# ╔═══════════════════════════════════════════════════════════╗
# ║  ส่วนที่ 2: ปิด USB Selective Suspend                    ║
# ║  GUID subgroup 2a737441-... = USB settings               ║
# ║  GUID setting  48e6b7a6-... = USB selective suspend      ║
# ╚═══════════════════════════════════════════════════════════╝
#  ปกติ Windows จะ "ลดไฟ" พอร์ต USB ที่ไม่ได้ใช้ → เมาส์/คีย์บอร์ด
#  ต้องปลุกจากโหมดประหยัดไฟก่อน = input delay แวบหนึ่ง
#  ตั้ง 0 = ห้ามเข้าโหมดประหยัดไฟ (ทั้งจอมิเตอร์ AC และแบตเตอรี่ DC)
powercfg /setacvalueindex scheme_current 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0 2>$null
powercfg /setdcvalueindex scheme_current 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0 2>$null
powercfg /setactive scheme_current 2>$null

# ╔═══════════════════════════════════════════════════════════╗
# ║  ส่วนที่ 3: Hibernate / Sleep / Fast Startup             ║
# ╚═══════════════════════════════════════════════════════════╝

# ปิด Hibernate ทั้งระบบ (ลบ hiberfil.sys ด้วย → คืนพื้นที่ดิสก์เท่า RAM)
powercfg /hibernate off

# ใส่ registry ซ้ำอีกชั้นกัน Windows กลับมาเปิดเอง
reg add "HKLM\SYSTEM\CurrentControlSet\Control\Power" /v "HibernateEnabled"       /t REG_DWORD /d 0 /f 2>$null
reg add "HKLM\SYSTEM\CurrentControlSet\Control\Power" /v "HibernateEnabledDefault" /t REG_DWORD /d 0 /f 2>$null

# ซ่อนปุ่ม Lock / Sleep ในเมนู Power (FlyoutMenuSettings)
reg add "HKLM\Software\Microsoft\Windows\CurrentVersion\Explorer\FlyoutMenuSettings" /v "ShowLockOption"  /t REG_DWORD /d 0 /f 2>$null
reg add "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FlyoutMenuSettings" /v "ShowSleepOption" /t REG_DWORD /d 0 /f 2>$null

# HiberbootEnabled = 0 → ปิด Fast Startup (hybrid shutdown)
#   Fast Startup = ปิดเครื่องแบบ hibernate kernel → ไดรเวอร์ค้างเก่าๆ
#   ปิดแล้ว shutdown/restart เต็มรูปแบบ ระบบสะอาดกว่า
reg add "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Power" /v "HiberbootEnabled" /t REG_DWORD /d 0 /f 2>$null

# PowerThrottlingOff = 1 → ปิด Power Throttling
#   ปกติ Windows ลดความเร็ว background app เพื่อประหยัดไฟ
#   ปิดแล้วทุก process วิ่งเต็มสปีดเสมอ
reg add "HKLM\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling" /v "PowerThrottlingOff" /t REG_DWORD /d 1 /f 2>$null

Write-Host "`n✅ BCD / USB / Hibernate เสร็จสมบูรณ์!" -ForegroundColor Green
Write-Host "⚠️ BCD ต้องรีสตาร์ทเครื่องถึงจะมีผล" -ForegroundColor Yellow