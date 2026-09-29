@echo off
:: =====================================================================
:: InternetR9 - Disable Network Bindings + DNS Optimization
:: =====================================================================
:: à¸ªà¸´à¹ˆà¸‡à¸—à¸µà¹ˆà¹„à¸Ÿà¸¥à¹Œà¸™à¸µà¹‰à¸—à¸³:
::   1. à¸›à¸´à¸” Network Protocol Bindings à¸—à¸µà¹ˆà¹„à¸¡à¹ˆà¸ˆà¸³à¹€à¸›à¹‡à¸™
::   2. à¹€à¸›à¸¥à¸µà¹ˆà¸¢à¸™ DNS à¹€à¸›à¹‡à¸™ Cloudflare (1.1.1.1) + flush cache
:: âš  à¸•à¹‰à¸­à¸‡ Run as Administrator
:: =====================================================================

echo =============================================
echo  InternetR9 - Bindings + DNS
echo =============================================
echo.

:: ===========================================
:: 1. à¸›à¸´à¸” Network Bindings à¸—à¸µà¹ˆà¹„à¸¡à¹ˆà¸ˆà¸³à¹€à¸›à¹‡à¸™
:: ===========================================
echo [1/2] à¸›à¸´à¸” Network Bindings à¸—à¸µà¹ˆà¹„à¸¡à¹ˆà¸ˆà¸³à¹€à¸›à¹‡à¸™...
echo.

:: ms_tcpip6      = IPv6
::   à¸›à¸´à¸”à¹€à¸žà¸£à¸²à¸°: ISP à¸ªà¹ˆà¸§à¸™à¹ƒà¸«à¸à¹ˆà¹ƒà¸™à¹„à¸—à¸¢à¸¢à¸±à¸‡à¹ƒà¸Šà¹‰ IPv4 à¹€à¸›à¹‡à¸™à¸«à¸¥à¸±à¸
::   IPv6 à¹€à¸žà¸´à¹ˆà¸¡ overhead + à¸šà¸²à¸‡ game server à¹„à¸¡à¹ˆà¸£à¸­à¸‡à¸£à¸±à¸š
::   âš  à¸–à¹‰à¸² ISP à¹ƒà¸Šà¹‰ IPv6 à¸­à¸¢à¹ˆà¸²à¸›à¸´à¸”!
echo   - à¸›à¸´à¸” IPv6 (ms_tcpip6)...

:: vmware_bridge  = VMware Bridge Protocol
::   à¸›à¸´à¸”à¹€à¸žà¸£à¸²à¸°: à¹ƒà¸Šà¹‰à¹€à¸‰à¸žà¸²à¸° VMware â†’ à¸–à¹‰à¸²à¹„à¸¡à¹ˆà¹„à¸”à¹‰à¹ƒà¸Šà¹‰ VM à¸›à¸´à¸”à¹„à¸”à¹‰
echo   - à¸›à¸´à¸” VMware Bridge...

:: ms_lldp        = Link-Layer Discovery Protocol
::   à¸›à¸´à¸”à¹€à¸žà¸£à¸²à¸°: à¹ƒà¸Šà¹‰à¹ƒà¸™ enterprise network à¹€à¸—à¹ˆà¸²à¸™à¸±à¹‰à¸™
echo   - à¸›à¸´à¸” LLDP...

:: ms_lltdio      = Link-Layer Topology Discovery I/O
::   à¸›à¸´à¸”à¹€à¸žà¸£à¸²à¸°: à¹ƒà¸Šà¹‰à¸ªà¸³à¸«à¸£à¸±à¸š network map à¹ƒà¸™ Windows â†’ à¹„à¸¡à¹ˆà¸ˆà¸³à¹€à¸›à¹‡à¸™
echo   - à¸›à¸´à¸” LLTD I/O...

:: ms_implat       = Microsoft Network Adapter Multiplexor
::   à¸›à¸´à¸”à¹€à¸žà¸£à¸²à¸°: à¹ƒà¸Šà¹‰à¸ªà¸³à¸«à¸£à¸±à¸š NIC teaming â†’ à¸„à¸™à¸—à¸±à¹ˆà¸§à¹„à¸›à¹„à¸¡à¹ˆà¹„à¸”à¹‰à¹ƒà¸Šà¹‰
echo   - à¸›à¸´à¸” Network Adapter Multiplexor...

:: ms_rspndr      = Link-Layer Topology Discovery Responder
::   à¸›à¸´à¸”à¹€à¸žà¸£à¸²à¸°: à¹€à¸«à¸¡à¸·à¸­à¸™ LLTD I/O â†’ à¹„à¸¡à¹ˆà¸ˆà¸³à¹€à¸›à¹‡à¸™
echo   - à¸›à¸´à¸” LLTD Responder...

:: ms_server      = File and Printer Sharing
::   à¸›à¸´à¸”à¹€à¸žà¸£à¸²à¸°: à¸–à¹‰à¸²à¹„à¸¡à¹ˆ share file/printer à¹ƒà¸™à¹€à¸„à¸£à¸·à¸­à¸‚à¹ˆà¸²à¸¢ à¸›à¸´à¸”à¹„à¸”à¹‰
::   âš  à¸–à¹‰à¸²à¹ƒà¸Šà¹‰ share folder à¹ƒà¸™à¸šà¹‰à¸²à¸™ à¸­à¸¢à¹ˆà¸²à¸›à¸´à¸”!
echo   - à¸›à¸´à¸” File and Printer Sharing...

:: ms_msclient    = Client for Microsoft Networks
::   à¸›à¸´à¸”à¹€à¸žà¸£à¸²à¸°: à¹ƒà¸Šà¹‰à¸ªà¸³à¸«à¸£à¸±à¸š access shared resources
::   âš  à¸–à¹‰à¸²à¹ƒà¸Šà¹‰ network drive / share folder à¸­à¸¢à¹ˆà¸²à¸›à¸´à¸”!
echo   - à¸›à¸´à¸” Client for Microsoft Networks...

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
"$bindings = @('ms_tcpip6','vmware_bridge','ms_lldp','ms_lltdio','ms_implat','ms_rspndr','ms_server','ms_msclient');" ^
"foreach ($b in $bindings) {" ^
"    try {" ^
"        Disable-NetAdapterBinding -Name '*' -ComponentID $b -ErrorAction SilentlyContinue" ^
"        Write-Host ('    âœ“ à¸›à¸´à¸” ' + $b)" ^
"    } catch {" ^
"        Write-Host ('    âš  à¸‚à¹‰à¸²à¸¡ ' + $b + ' (à¹„à¸¡à¹ˆà¸žà¸š)')" ^
"    }" ^
"}"

echo.
echo     âœ“ Bindings à¹€à¸ªà¸£à¹‡à¸ˆ
echo.

:: ===========================================
:: 2. DNS Optimization
:: ===========================================
echo [2/2] à¹€à¸›à¸¥à¸µà¹ˆà¸¢à¸™ DNS à¹€à¸›à¹‡à¸™ Cloudflare + Google...
echo.

:: Cloudflare DNS:
::   Primary:   1.1.1.1
::   Secondary: 1.0.0.1
::   à¸‚à¹‰à¸­à¸”à¸µ: à¹€à¸£à¹‡à¸§à¸—à¸µà¹ˆà¸ªà¸¸à¸”à¹ƒà¸™à¹‚à¸¥à¸ (avg ~11ms), privacy-focused
::
:: Google DNS:
::   Primary:   8.8.8.8
::   Secondary: 8.8.4.4
::   à¸‚à¹‰à¸­à¸”à¸µ: à¹€à¸ªà¸–à¸µà¸¢à¸£, à¸¡à¸µ global anycast
::
:: InternetR9 à¹ƒà¸Šà¹‰: Preferred=1.1.1.1, Alternate=1.0.0.1 (Cloudflare à¹€à¸—à¹ˆà¸²à¸™à¸±à¹‰à¸™)

:: à¸›à¸£à¸±à¸š DNS à¸—à¸¸à¸ adapter à¸—à¸µà¹ˆ active
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
"$adapters = Get-NetAdapter | Where-Object { $_.Status -eq 'Up' -and $_.InterfaceType -ne 24 };" ^
"foreach ($a in $adapters) {" ^
"    $name = $a.Name;" ^
"    Write-Host ('  Adapter: ' + $name);" ^
"    netsh interface ip set dns name=$name static 1.1.1.1 primary | Out-Null;" ^
"    netsh interface ip add dns name=$name 1.0.0.1 index=2 | Out-Null;" ^
"    Write-Host ('    DNS â†’ 1.1.1.1 / 1.0.0.1 (Cloudflare)');" ^
"}" ^
"Write-Host ''" ^
"Write-Host '  Flushing DNS cache...';" ^
"ipconfig /flushdns | Out-Null;" ^
"Write-Host '    âœ“ DNS cache cleared'"

echo.
echo =============================================
echo  âœ… Bindings + DNS à¹€à¸ªà¸£à¹‡à¸ˆà¹€à¸£à¸µà¸¢à¸šà¸£à¹‰à¸­à¸¢!
echo.
echo  DNS à¸›à¸±à¸ˆà¸ˆà¸¸à¸šà¸±à¸™: 1.1.1.1 / 1.0.0.1 (Cloudflare)
echo.
echo  à¸–à¹‰à¸²à¸•à¹‰à¸­à¸‡à¸à¸²à¸£à¹€à¸›à¸¥à¸µà¹ˆà¸¢à¸™à¹€à¸›à¹‡à¸™ Google DNS:
echo    netsh interface ip set dns name="Wi-Fi" static 8.8.8.8 primary
echo    netsh interface ip add dns name="Wi-Fi" 8.8.4.4 index=2
echo.
echo  à¸–à¹‰à¸²à¸•à¹‰à¸­à¸‡à¸à¸²à¸£à¸à¸¥à¸±à¸šà¹€à¸›à¹‡à¸™ Auto (DHCP):
echo    netsh interface ip set dns name="Wi-Fi" dhcp
echo =============================================
