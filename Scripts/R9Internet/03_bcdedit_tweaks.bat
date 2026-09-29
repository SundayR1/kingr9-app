@echo off
:: =====================================================================
:: InternetR9 - Boot Timer Settings (BcdEdit)
:: =====================================================================
:: à¸ªà¸´à¹ˆà¸‡à¸—à¸µà¹ˆà¹„à¸Ÿà¸¥à¹Œà¸™à¸µà¹‰à¸—à¸³: à¸›à¸£à¸±à¸šà¸„à¹ˆà¸² Boot Configuration à¹€à¸žà¸·à¹ˆà¸­à¸¥à¸” timer overhead
:: âš  à¸•à¹‰à¸­à¸‡ Run as Administrator
:: âš âš  à¸£à¸°à¸§à¸±à¸‡: à¸à¸²à¸£à¹€à¸›à¸¥à¸µà¹ˆà¸¢à¸™ BCD à¸­à¸²à¸ˆà¸ªà¹ˆà¸‡à¸œà¸¥à¸à¸±à¸šà¸à¸²à¸£à¸šà¸¹à¸• / Hyper-V
:: =====================================================================

echo =============================================
echo  InternetR9 - Boot Timer Tweaks (BcdEdit)
echo =============================================
echo.

:: --- Platform Tick ---
:: à¸›à¸´à¸” â€” à¹„à¸¡à¹ˆà¹ƒà¸Šà¹‰ platform timer (HPET) à¸ªà¸³à¸«à¸£à¸±à¸š clock tick
:: à¹ƒà¸«à¹‰ Windows à¹ƒà¸Šà¹‰ TSC (CPU timestamp counter) à¹à¸—à¸™ à¸‹à¸¶à¹ˆà¸‡à¹€à¸£à¹‡à¸§à¸à¸§à¹ˆà¸²
echo [1/5] à¸›à¸´à¸” Platform Tick...
bcdedit /set useplatformtick No

:: --- Platform Clock ---
:: à¸›à¸´à¸” â€” à¹€à¸«à¸¡à¸·à¸­à¸™à¸à¸±à¸šà¸”à¹‰à¸²à¸™à¸šà¸™ à¸šà¸±à¸‡à¸„à¸±à¸šà¹ƒà¸«à¹‰à¹ƒà¸Šà¹‰ TSC
:: à¸¥à¸” interrupt overhead à¸ˆà¸²à¸ HPET
echo [2/5] à¸›à¸´à¸” Platform Clock...
bcdedit /set useplatformclock No

:: --- TSC Sync Policy ---
:: à¸¥à¸šà¸„à¹ˆà¸²à¸™à¸µà¹‰à¹€à¸žà¸·à¹ˆà¸­à¹ƒà¸«à¹‰ Windows à¸£à¸±à¸™à¸„à¹ˆà¸² default
:: (à¸šà¸²à¸‡à¸—à¸µà¸–à¸¹à¸  set à¹€à¸›à¹‡à¸™ Enhanced à¸‹à¸¶à¹ˆà¸‡à¹€à¸žà¸´à¹ˆà¸¡ overhead)
echo [3/5] à¸¥à¸š TSC Sync Policy...
bcdedit /deletevalue tscsyncpolicy 2>nul

:: --- Dynamic Tick ---
:: เปิด — ให้ OS ข้าม timer interrupt เมื่อ CPU idle
:: ลด power consumption + ลด unnecessary interrupts
echo [4/5] เปิด Dynamic Tick (disabledynamictick no)...
bcdedit /set disabledynamictick no

:: --- Hypervisor ---
:: ⚠⚠ ข้าม hypervisorlaunchtype: ปิดแล้วใช้ WSL2/Docker/Windows Sandbox จะใช้งานไม่ได้
:: ถ้าต้องการปิดจริง ให้รันเองด้วย: bcdedit /set hypervisorlaunchtype off
echo [5/5] Hypervisor -- ข้ามเพื่อความปลอดภัย (ป้องกัน WSL2/Docker พัง)
echo        * ถ้าต้องการปิด: bcdedit /set hypervisorlaunchtype off
echo        * ถ้าต้องการเปิดกลับ: bcdedit /set hypervisorlaunchtype auto

echo.
echo =============================================
echo  ✅ BcdEdit Tweaks เสร็จเรียบร้อย!
echo.
echo  ⚠  กรุณา Restart Windows เพื่อให้มีผล
echo =============================================
