@echo off
:: =====================================================================
:: InternetR9 - Force Network Reset (aspas style)
:: Hard reset the network stack - internet DROPS for ~10-20 seconds
:: then comes back automatically. Run as Administrator.
:: =====================================================================

echo =============================================
echo  InternetR9 - Force Network Reset
echo =============================================
echo  WARNING: internet disconnects ~10-20s then auto-reconnects
echo.
echo [1/6] Release IP address...
ipconfig /release >nul 2>&1
echo [2/6] Flush DNS cache...
ipconfig /flushdns >nul 2>&1
echo [3/6] Winsock reset (full effect after restart)...
netsh winsock reset >nul 2>&1
echo [4/6] TCP/IP stack reset...
netsh int ip reset >nul 2>&1
echo [5/6] Restart network adapters...
powershell -NoProfile -Command "Get-NetAdapter | Where-Object { $_.Status -eq 'Up' } | Restart-NetAdapter -Confirm:$false" >nul 2>&1
echo [6/6] Renew IP address...
ping -n 4 127.0.0.1 >nul
ipconfig /renew >nul 2>&1
ipconfig /flushdns >nul 2>&1
echo.
echo  Done - internet should be back in a few seconds
