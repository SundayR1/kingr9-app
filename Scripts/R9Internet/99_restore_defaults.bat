@echo off
:: =====================================================================
:: InternetR9 - RESTORE ALL (à¸à¸¥à¸±à¸šà¸„à¹ˆà¸²à¹€à¸”à¸´à¸¡)
:: =====================================================================
:: à¸£à¸±à¸™ as Administrator à¹€à¸žà¸·à¹ˆà¸­à¸à¸¥à¸±à¸šà¸„à¹ˆà¸² Windows à¸—à¸±à¹‰à¸‡à¸«à¸¡à¸”à¹€à¸›à¹‡à¸™à¸„à¹ˆà¸² default
:: =====================================================================

echo =============================================
echo  InternetR9 - RESTORE DEFAULT VALUES
echo =============================================
echo.
echo à¸à¸³à¸¥à¸±à¸‡à¸à¸¥à¸±à¸šà¸„à¹ˆà¸²à¹€à¸›à¹‡à¸™ Windows Default...
echo.

:: --- Netsh: à¸à¸¥à¸±à¸šà¸„à¹ˆà¸² default ---
echo [1/4] à¸„à¸·à¸™à¸„à¹ˆà¸² Netsh...
netsh int tcp set heuristics default >nul 2>&1
netsh int tcp set global ecncapability=default >nul 2>&1
netsh int tcp set global timestamps=default >nul 2>&1
netsh int tcp set global nonsackrttresiliency=default >nul 2>&1
netsh int tcp set global initialRto=3000 >nul 2>&1
netsh int tcp set global autotuninglevel=normal >nul 2>&1
netsh int tcp set global rsc=enabled >nul 2>&1
netsh int tcp set global rss=enabled >nul 2>&1
netsh int tcp set global dca=enabled >nul 2>&1
netsh int tcp set global fastopen=enabled >nul 2>&1
netsh int ip set global taskoffload=enabled >nul 2>&1
netsh int ip set global icmpredirects=enabled >nul 2>&1
netsh int ip set global multicasting=enabled >nul 2>&1
echo     âœ“ Netsh à¸„à¸·à¸™à¸„à¹ˆà¸²à¹à¸¥à¹‰à¸§

:: --- BcdEdit: à¸à¸¥à¸±à¸šà¸„à¹ˆà¸² default ---
echo [2/4] à¸„à¸·à¸™à¸„à¹ˆà¸² BcdEdit...
bcdedit /deletevalue useplatformtick >nul 2>&1
bcdedit /deletevalue useplatformclock >nul 2>&1
bcdedit /deletevalue disabledynamictick >nul 2>&1
bcdedit /set hypervisorlaunchtype auto >nul 2>&1
echo     âœ“ BcdEdit à¸„à¸·à¸™à¸„à¹ˆà¸²à¹à¸¥à¹‰à¸§

:: --- Registry: à¸¥à¸šà¸„à¹ˆà¸²à¸—à¸µà¹ˆà¹€à¸žà¸´à¹ˆà¸¡ ---
echo [3/4] à¸„à¸·à¸™à¸„à¹ˆà¸² Registry...
echo     (à¸–à¹‰à¸²à¸¡à¸µ backup_*.reg à¸—à¸µà¹ˆ Documents\KingR9Tools\Backup à¹ƒà¸«à¹‰ double-click à¹€à¸žà¸·à¹ˆà¸­ restore)
echo     à¸à¸³à¸¥à¸±à¸‡à¸¥à¸šà¸„à¹ˆà¸²à¸—à¸µà¹ˆà¹€à¸žà¸´à¹ˆà¸¡à¹€à¸‚à¹‰à¸²à¹„à¸›...

:: à¸¥à¸š TCP/IP tweaks
for %%v in (Tcp1323Opts TcpWindowSize GlobalMaxTcpWindowSize MaxUserPort TcpTimedWaitDelay TcpMaxDataRetransmissions DefaultTTL EnablePMTUDiscovery EnablePMTUBHDetect SackOpts MaxFreeTcbs MaxHashTableSize IRPStackSize DisableTaskOffload TcpNumConnections EnableECNCapability DisableUserTOSSetting ArpCacheLife ArpCacheMinReferencedLife ArpUseEtherSNAP KeepAliveTime KeepAliveInterval TcpAckFrequency TCPNoDelay TcpDelAckTicks DelayedAckTicks DelayedAckFrequency EnableWsd UdpMaxDatagramSend UdpMaxDatagramReceive TcpMaxDupAcks DefaultRcvWindow DefaultSendWindow TcpRfc1323 SynAttackProtect MaxSynBacklog TcpInitialRtt TcpSendSegmentSize FastUserModeLimit TcpConnectionsPerNetworkInterface) do (
    reg delete "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v %%v /f >nul 2>&1
)

:: à¸¥à¸š QoS
reg delete "HKLM\SOFTWARE\Policies\Microsoft\Windows\Psched" /v NonBestEffortLimit /f >nul 2>&1

:: à¸¥à¸š AFD tweaks
for %%v in (FastSendDatagramThreshold FastCopyReceiveThreshold DefaultReceiveWindow DefaultSendWindow DynamicSendBufferDisable IgnorePushBitOnReceives NonBlockingSendSpecialBuffering DoNotUseBufferChaining PriorityBoost ReusePortMinimum ReusePortUpper) do (
    reg delete "HKLM\SYSTEM\CurrentControlSet\Services\AFD\Parameters" /v %%v /f >nul 2>&1
)

:: à¸¥à¸š DNS Cache tweaks
for %%v in (MaxCacheTtl MaxNegativeCacheTtl NegativeCacheTime NetFailureCacheTime CacheHashTableBucketSize MaximumCacheSize) do (
    reg delete "HKLM\SYSTEM\CurrentControlSet\Services\Dnscache\Parameters" /v %%v /f >nul 2>&1
)

echo     âœ“ Registry à¸„à¸·à¸™à¸„à¹ˆà¸²à¹à¸¥à¹‰à¸§

:: --- DNS: à¸à¸¥à¸±à¸šà¹€à¸›à¹‡à¸™ DHCP ---
echo [4/4] à¸„à¸·à¸™à¸„à¹ˆà¸² DNS à¹€à¸›à¹‡à¸™ DHCP...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
"Get-NetAdapter | Where-Object { $_.Status -eq 'Up' } | ForEach-Object {" ^
"    netsh interface ip set dns name=$($_.Name) dhcp | Out-Null;" ^
"    Write-Host ('    âœ“ ' + $_.Name + ' â†’ DHCP')" ^
"}"

:: --- Bindings: à¹€à¸›à¸´à¸”à¸„à¸·à¸™ ---
echo.
echo à¹€à¸›à¸´à¸” Bindings à¸„à¸·à¸™...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
"$bindings = @('ms_tcpip6','ms_lldp','ms_lltdio','ms_rspndr','ms_server','ms_msclient');" ^
"foreach ($b in $bindings) {" ^
"    try { Enable-NetAdapterBinding -Name '*' -ComponentID $b -EA SilentlyContinue; Write-Host ('    âœ“ à¹€à¸›à¸´à¸” ' + $b) } catch {}" ^
"}"

echo.
echo =============================================
echo  âœ… à¸à¸¥à¸±à¸šà¸„à¹ˆà¸² Default à¸—à¸±à¹‰à¸‡à¸«à¸¡à¸”à¹€à¸ªà¸£à¹‡à¸ˆà¹à¸¥à¹‰à¸§!
echo  âš  à¸à¸£à¸¸à¸“à¸² Restart Windows à¹€à¸žà¸·à¹ˆà¸­à¹ƒà¸«à¹‰à¸¡à¸µà¸œà¸¥
echo =============================================
