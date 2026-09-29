@echo off
:: =====================================================================
:: InternetR9 - Network Optimization: Registry Tweaks
:: =====================================================================
:: à¸ªà¸´à¹ˆà¸‡à¸—à¸µà¹ˆà¹„à¸Ÿà¸¥à¹Œà¸™à¸µà¹‰à¸—à¸³: à¸›à¸£à¸±à¸šà¸„à¹ˆà¸² TCP/IP, DNS Cache, Winsock, AFD, NetBT, NDIS
::                  à¹ƒà¸™ Windows Registry à¹€à¸žà¸·à¹ˆà¸­ optimize à¸›à¸£à¸°à¸ªà¸´à¸—à¸˜à¸´à¸ à¸²à¸žà¹€à¸™à¹‡à¸•
:: âš  à¸•à¹‰à¸­à¸‡ Run as Administrator
:: âš  à¸„à¸§à¸£ backup registry à¸à¹ˆà¸­à¸™à¸£à¸±à¸™ (à¹„à¸Ÿà¸¥à¹Œà¸™à¸µà¹‰à¸¡à¸µ backup à¹ƒà¸«à¹‰à¸­à¸±à¸•à¹‚à¸™à¸¡à¸±à¸•à¸´)
:: =====================================================================

echo =============================================
echo  InternetR9 - Registry Tweaks (Extracted)
echo =============================================
echo.
echo âš   à¸à¸£à¸¸à¸“à¸²à¸£à¸±à¸™ as Administrator!
echo.

:: à¸ªà¸£à¹‰à¸²à¸‡ backup à¸à¹ˆà¸­à¸™
echo [BACKUP] à¸à¸³à¸¥à¸±à¸‡à¸ªà¸³à¸£à¸­à¸‡ Registry à¸à¹ˆà¸­à¸™à¹à¸à¹‰à¹„à¸‚...
:: backup into the app's own folder (never on Desktop): Documents\KingR9Tools\Backup
set "BKR=%USERPROFILE%\Documents\KingR9Tools\Backup"
if not exist "%BKR%" mkdir "%BKR%" >nul 2>&1
reg export "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" "%BKR%\backup_tcpip.reg" /y >nul 2>&1
reg export "HKLM\SYSTEM\CurrentControlSet\Services\AFD\Parameters" "%BKR%\backup_afd.reg" /y >nul 2>&1
reg export "HKLM\SYSTEM\CurrentControlSet\Services\Dnscache\Parameters" "%BKR%\backup_dnscache.reg" /y >nul 2>&1
reg export "HKLM\SYSTEM\CurrentControlSet\Services\NetBT\Parameters" "%BKR%\backup_netbt.reg" /y >nul 2>&1
echo [BACKUP] à¸ªà¸³à¸£à¸­à¸‡à¹€à¸ªà¸£à¹‡à¸ˆà¸—à¸µà¹ˆ Documents\KingR9Tools\Backup (backup_*.reg)
echo.

:: ===========================================
:: 1. TCP/IP Parameters (à¸«à¸¥à¸±à¸)
:: ===========================================
echo [1/7] TCP/IP Parameters...

:: TCP Window Scaling (RFC 1323) â€” à¹€à¸›à¸´à¸” Window/Timestamp options
:: à¸„à¹ˆà¸² 1 = à¹€à¸›à¸´à¸”à¹€à¸‰à¸žà¸²à¸° Window Scale
:: à¸Šà¹ˆà¸§à¸¢à¹ƒà¸«à¹‰ TCP window à¹ƒà¸«à¸à¹ˆà¸à¸§à¹ˆà¸² 64KB à¹„à¸”à¹‰ â†’ à¹€à¸žà¸´à¹ˆà¸¡ throughput à¸šà¸™ link à¹€à¸£à¹‡à¸§
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v Tcp1323Opts /t REG_DWORD /d 1 /f >nul

:: TCP Window Size â€” à¸‚à¸™à¸²à¸” receive window
:: à¸„à¹ˆà¸² 524287 (512KB-1) = à¸„à¹ˆà¸²à¸ªà¸¹à¸‡à¸ªà¸¸à¸”à¸—à¸µà¹ˆ TCP window scale à¸£à¸­à¸‡à¸£à¸±à¸š
:: à¸¢à¸´à¹ˆà¸‡à¹ƒà¸«à¸à¹ˆ = à¸£à¸±à¸šà¸‚à¹‰à¸­à¸¡à¸¹à¸¥à¹„à¸”à¹‰à¸¡à¸²à¸à¸‚à¸¶à¹‰à¸™à¸à¹ˆà¸­à¸™à¸•à¹‰à¸­à¸‡ ACK â†’ à¸”à¸µà¸ªà¸³à¸«à¸£à¸±à¸š download
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v TcpWindowSize /t REG_DWORD /d 524287 /f >nul
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v GlobalMaxTcpWindowSize /t REG_DWORD /d 524287 /f >nul

:: Max Ephemeral Ports â€” à¸ˆà¸³à¸™à¸§à¸™ port à¸—à¸µà¹ˆà¹ƒà¸Šà¹‰ connect à¸­à¸­à¸
:: à¸„à¹ˆà¸² 65534 = à¹ƒà¸Šà¹‰à¹„à¸”à¹‰à¹€à¸à¸·à¸­à¸šà¸—à¸¸à¸ port â†’ à¸”à¸µà¹€à¸¡à¸·à¹ˆà¸­à¹€à¸›à¸´à¸”à¸«à¸¥à¸²à¸¢ connection à¸žà¸£à¹‰à¸­à¸¡à¸à¸±à¸™
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v MaxUserPort /t REG_DWORD /d 65534 /f >nul

:: TIME_WAIT delay â€” à¹€à¸§à¸¥à¸²à¸£à¸­à¸à¹ˆà¸­à¸™à¸›à¸´à¸” connection à¸–à¸²à¸§à¸£
:: à¸„à¹ˆà¸² 30 à¸§à¸´à¸™à¸²à¸—à¸µ (à¸›à¸à¸•à¸´ 240 à¸§à¸´à¸™à¸²à¸—à¸µ) = à¸›à¸¥à¹ˆà¸­à¸¢ port à¸à¸¥à¸±à¸šà¹€à¸£à¹‡à¸§à¸‚à¸¶à¹‰à¸™
:: âš  à¸„à¹ˆà¸²à¸•à¹ˆà¸³à¹€à¸à¸´à¸™à¸­à¸²à¸ˆà¸—à¸³à¹ƒà¸«à¹‰ connection à¸‹à¹‰à¸³à¸à¸±à¸™
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v TcpTimedWaitDelay /t REG_DWORD /d 30 /f >nul

:: TCP Data Retransmissions â€” à¸ªà¹ˆà¸‡à¸‹à¹‰à¸³à¸à¸µà¹ˆà¸„à¸£à¸±à¹‰à¸‡à¸à¹ˆà¸­à¸™à¸•à¸±à¸”à¸ªà¸´à¸™à¸§à¹ˆà¸² connection à¸•à¸²à¸¢
:: ค่า 3 = ส่งซ้ำ 3 ครั้งก่อนตัด connection (ต่ำสุดที่ปลอดภัยสำหรับเกม)
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v TcpMaxDataRetransmissions /t REG_DWORD /d 3 /f >nul

:: Default TTL â€” Time To Live à¸‚à¸­à¸‡ packet
:: à¸„à¹ˆà¸² 64 = à¸¡à¸²à¸•à¸£à¸à¸²à¸™ Linux (Windows à¸›à¸à¸•à¸´ 128)
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v DefaultTTL /t REG_DWORD /d 64 /f >nul

:: PMTU Discovery â€” à¸„à¹‰à¸™à¸«à¸² MTU à¸—à¸µà¹ˆà¹ƒà¸«à¸à¹ˆà¸—à¸µà¹ˆà¸ªà¸¸à¸”à¸—à¸µà¹ˆà¸ªà¹ˆà¸‡à¹„à¸”à¹‰à¹‚à¸”à¸¢à¹„à¸¡à¹ˆ fragment
:: à¸„à¹ˆà¸² 1 = à¹€à¸›à¸´à¸” â†’ à¸¥à¸” fragmentation = à¸ªà¹ˆà¸‡à¸‚à¹‰à¸­à¸¡à¸¹à¸¥à¸¡à¸µà¸›à¸£à¸°à¸ªà¸´à¸—à¸˜à¸´à¸ à¸²à¸žà¸à¸§à¹ˆà¸²
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v EnablePMTUDiscovery /t REG_DWORD /d 1 /f >nul

:: PMTU Black Hole Detection â€” à¸•à¸£à¸§à¸ˆà¸ˆà¸±à¸š router à¸—à¸µà¹ˆà¸—à¸´à¹‰à¸‡ packet à¹ƒà¸«à¸à¹ˆ
:: à¸„à¹ˆà¸² 0 = à¸›à¸´à¸” â†’ à¸¥à¸” overhead
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v EnablePMTUBHDetect /t REG_DWORD /d 0 /f >nul

:: SACK (Selective Acknowledgment) â€” ACK à¹€à¸‰à¸žà¸²à¸° packet à¸—à¸µà¹ˆà¸«à¸²à¸¢à¹à¸—à¸™à¸—à¸µà¹ˆà¸ˆà¸°à¸ªà¹ˆà¸‡à¹ƒà¸«à¸¡à¹ˆà¸—à¸±à¹‰à¸‡à¸«à¸¡à¸”
:: à¸„à¹ˆà¸² 1 = à¹€à¸›à¸´à¸” â†’ à¸¥à¸”à¸à¸²à¸£à¸ªà¹ˆà¸‡à¸‚à¹‰à¸­à¸¡à¸¹à¸¥à¸‹à¹‰à¸³
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v SackOpts /t REG_DWORD /d 1 /f >nul

:: Max Free TCBs â€” à¸ˆà¸³à¸™à¸§à¸™ TCP Control Block à¸—à¸µà¹ˆà¹€à¸à¹‡à¸šà¹„à¸§à¹‰à¹ƒà¸™ memory
:: à¸„à¹ˆà¸² 64000 = à¸£à¸­à¸‡à¸£à¸±à¸š connection à¸žà¸£à¹‰à¸­à¸¡à¸à¸±à¸™à¹„à¸”à¹‰à¸¡à¸²à¸
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v MaxFreeTcbs /t REG_DWORD /d 64000 /f >nul
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v MaxHashTableSize /t REG_DWORD /d 65536 /f >nul

:: IRP Stack Size â€” à¸‚à¸™à¸²à¸” I/O Request Packet stack
:: à¸„à¹ˆà¸² 50 = à¹€à¸žà¸´à¹ˆà¸¡à¸ˆà¸²à¸à¸„à¹ˆà¸² default (15-20) â†’ à¸”à¸µà¸ªà¸³à¸«à¸£à¸±à¸š network intensive apps
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v IRPStackSize /t REG_DWORD /d 50 /f >nul

:: Task Offload â€” à¹ƒà¸«à¹‰ NIC à¸Šà¹ˆà¸§à¸¢à¸„à¸³à¸™à¸§à¸“ TCP/IP checksum à¹à¸—à¸™ CPU
:: à¸„à¹ˆà¸² 0 = à¹€à¸›à¸´à¸” (0 = Don't disable = à¹€à¸›à¸´à¸”)
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v DisableTaskOffload /t REG_DWORD /d 0 /f >nul

:: Max TCP Connections â€” à¸ˆà¸³à¸à¸±à¸”à¸ˆà¸³à¸™à¸§à¸™ connection à¸žà¸£à¹‰à¸­à¸¡à¸à¸±à¸™
:: à¸„à¹ˆà¸² 16777214 (16M) = à¹à¸—à¸šà¹„à¸¡à¹ˆà¸ˆà¸³à¸à¸±à¸”
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v TcpNumConnections /t REG_DWORD /d 16777214 /f >nul

:: ECN (Explicit Congestion Notification)
:: à¸„à¹ˆà¸² 0 = à¸›à¸´à¸” â†’ à¸šà¸²à¸‡ router à¹€à¸à¹ˆà¸²à¹„à¸¡à¹ˆà¸£à¸­à¸‡à¸£à¸±à¸š ECN à¸—à¸³à¹ƒà¸«à¹‰ packet à¸«à¸²à¸¢
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v EnableECNCapability /t REG_DWORD /d 0 /f >nul

:: TOS Setting â€” à¹ƒà¸«à¹‰ app à¸à¸³à¸«à¸™à¸” Type of Service à¹„à¸”à¹‰
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v DisableUserTOSSetting /t REG_DWORD /d 0 /f >nul

:: ARP Cache â€” à¸£à¸°à¸¢à¸°à¹€à¸§à¸¥à¸²à¹€à¸à¹‡à¸š ARP table (à¸§à¸´à¸™à¸²à¸—à¸µ)
:: à¸„à¹ˆà¸² 9 = refresh à¹€à¸£à¹‡à¸§ â†’ à¸¥à¸”à¸›à¸±à¸à¸«à¸² stale ARP entry
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v ArpCacheLife /t REG_DWORD /d 9 /f >nul
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v ArpCacheMinReferencedLife /t REG_DWORD /d 9 /f >nul
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v ArpUseEtherSNAP /t REG_DWORD /d 0 /f >nul

:: Keep-Alive â€” à¸ªà¹ˆà¸‡ keep-alive packet à¸—à¸¸à¸à¸à¸µà¹ˆ milliseconds
:: à¸„à¹ˆà¸² 100000 (100 à¸§à¸´à¸™à¸²à¸—à¸µ) â†’ à¸›à¸à¸•à¸´ 7200000 (2 à¸Šà¸±à¹ˆà¸§à¹‚à¸¡à¸‡)
:: à¸•à¸£à¸§à¸ˆà¸ˆà¸±à¸š dead connection à¹€à¸£à¹‡à¸§à¸‚à¸¶à¹‰à¸™
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v KeepAliveTime /t REG_DWORD /d 100000 /f >nul
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v KeepAliveInterval /t REG_DWORD /d 120 /f >nul

:: === Nagle's Algorithm / TCP ACK Delay ===
:: à¸›à¸´à¸” Nagle + à¸¥à¸” ACK delay â†’ à¸¥à¸” latency (à¸ªà¸³à¸„à¸±à¸à¸¡à¸²à¸à¸ªà¸³à¸«à¸£à¸±à¸šà¹€à¸à¸¡)
:: TcpAckFrequency=1 â†’ ACK à¸—à¸¸à¸ packet (à¹„à¸¡à¹ˆà¸£à¸­)
:: TCPNoDelay=1 â†’ à¸›à¸´à¸” Nagle (à¸ªà¹ˆà¸‡ packet à¹€à¸¥à¹‡à¸à¸—à¸±à¸™à¸—à¸µ)
:: TcpDelAckTicks=0 â†’ à¹„à¸¡à¹ˆà¸«à¸™à¹ˆà¸§à¸‡ ACK
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v TcpAckFrequency /t REG_DWORD /d 1 /f >nul
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v TCPNoDelay /t REG_DWORD /d 1 /f >nul
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v TcpDelAckTicks /t REG_DWORD /d 0 /f >nul
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v DelayedAckTicks /t REG_DWORD /d 0 /f >nul
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v DelayedAckFrequency /t REG_DWORD /d 1 /f >nul

:: WSD (Web Services Discovery) â€” à¸›à¸´à¸”à¹€à¸žà¸·à¹ˆà¸­à¸¥à¸” broadcast traffic
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v EnableWsd /t REG_DWORD /d 0 /f >nul

:: UDP Max Datagram Size
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v UdpMaxDatagramSend /t REG_DWORD /d 65531 /f >nul
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v UdpMaxDatagramReceive /t REG_DWORD /d 65531 /f >nul

:: TCP DupAck threshold â€” à¸ˆà¸³à¸™à¸§à¸™ duplicate ACK à¸à¹ˆà¸­à¸™ fast retransmit
:: à¸„à¹ˆà¸² 10 = à¸£à¸­ duplicate ACK à¸¡à¸²à¸à¸‚à¸¶à¹‰à¸™à¸à¹ˆà¸­à¸™à¸ªà¹ˆà¸‡à¸‹à¹‰à¸³ â†’ à¸¥à¸” false retransmit
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v TcpMaxDupAcks /t REG_DWORD /d 10 /f >nul

:: Default Send/Receive Window â€” 4MB
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v DefaultRcvWindow /t REG_DWORD /d 4194304 /f >nul
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v DefaultSendWindow /t REG_DWORD /d 4194304 /f >nul

:: RFC 1323, SYN Protection, SYN Backlog
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v TcpRfc1323 /t REG_DWORD /d 1 /f >nul
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v SynAttackProtect /t REG_DWORD /d 1 /f >nul
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v MaxSynBacklog /t REG_DWORD /d 8192 /f >nul

:: TCP Initial RTT â€” à¸„à¹ˆà¸² round-trip time à¹€à¸£à¸´à¹ˆà¸¡à¸•à¹‰à¸™ (ms)
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v TcpInitialRtt /t REG_DWORD /d 0 /f >nul

:: TCP Segment Size + Fast User Mode + Connections per NIC
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v TcpSendSegmentSize /t REG_DWORD /d 65531 /f >nul
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v FastUserModeLimit /t REG_DWORD /d 100000 /f >nul
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters" /v TcpConnectionsPerNetworkInterface /t REG_DWORD /d 4096 /f >nul

:: IPv6 â€” à¸›à¸´à¸” ECN + Task Offload enabled
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters" /v DisableTaskOffload /t REG_DWORD /d 0 /f >nul
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters" /v EnableECNCapability /t REG_DWORD /d 0 /f >nul

echo     âœ“ TCP/IP Parameters à¹€à¸ªà¸£à¹‡à¸ˆ

:: ===========================================
:: 2. QoS / Bandwidth Throttling
:: ===========================================
echo [2/7] QoS Policy...

:: à¸›à¸´à¸”à¸à¸²à¸£à¸ˆà¸³à¸à¸±à¸” bandwidth à¸‚à¸­à¸‡ QoS Packet Scheduler
:: à¸„à¹ˆà¸² 0 = à¹„à¸¡à¹ˆà¸ˆà¸³à¸à¸±à¸” bandwidth à¸ªà¸³à¸«à¸£à¸±à¸š non-best-effort traffic
reg add "HKLM\SOFTWARE\Policies\Microsoft\Windows\Psched" /v NonBestEffortLimit /t REG_DWORD /d 0 /f >nul

echo     âœ“ QoS à¹€à¸ªà¸£à¹‡à¸ˆ

:: ===========================================
:: 3. Winsock Parameters
:: ===========================================
echo [3/7] Winsock...

reg add "HKLM\SYSTEM\CurrentControlSet\Services\Winsock" /v DisabledComponents /t REG_DWORD /d 0 /f >nul
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Winsock" /v MaxSockAddrLength /t REG_DWORD /d 32 /f >nul
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Winsock" /v MaxProtocolChain /t REG_DWORD /d 8 /f >nul

echo     âœ“ Winsock à¹€à¸ªà¸£à¹‡à¸ˆ

:: ===========================================
:: 4. AFD (Ancillary Function Driver) â€” Winsock kernel driver
:: ===========================================
echo [4/7] AFD Parameters...

:: AFD à¸„à¸·à¸­ driver à¸—à¸µà¹ˆà¸ˆà¸±à¸”à¸à¸²à¸£ socket à¹ƒà¸™ kernel
:: FastSendDatagramThreshold â€” à¸‚à¸™à¸²à¸” datagram à¸—à¸µà¹ˆà¸ªà¹ˆà¸‡à¹à¸šà¸š fast path
reg add "HKLM\SYSTEM\CurrentControlSet\Services\AFD\Parameters" /v FastSendDatagramThreshold /t REG_DWORD /d 512 /f >nul
reg add "HKLM\SYSTEM\CurrentControlSet\Services\AFD\Parameters" /v FastCopyReceiveThreshold /t REG_DWORD /d 512 /f >nul

:: Default Send/Receive Window (4MB)
reg add "HKLM\SYSTEM\CurrentControlSet\Services\AFD\Parameters" /v DefaultReceiveWindow /t REG_DWORD /d 4194304 /f >nul
reg add "HKLM\SYSTEM\CurrentControlSet\Services\AFD\Parameters" /v DefaultSendWindow /t REG_DWORD /d 4194304 /f >nul

:: Dynamic Buffer â€” à¸›à¸´à¸”à¹€à¸žà¸·à¹ˆà¸­à¹ƒà¸Šà¹‰ fixed buffer size
reg add "HKLM\SYSTEM\CurrentControlSet\Services\AFD\Parameters" /v DynamicSendBufferDisable /t REG_DWORD /d 1 /f >nul

:: IgnorePushBitOnReceives â€” à¹„à¸¡à¹ˆà¸ªà¸™à¹ƒà¸ˆ PUSH bit â†’ buffer à¸‚à¹‰à¸­à¸¡à¸¹à¸¥à¹„à¸”à¹‰à¸¡à¸²à¸à¸‚à¸¶à¹‰à¸™
reg add "HKLM\SYSTEM\CurrentControlSet\Services\AFD\Parameters" /v IgnorePushBitOnReceives /t REG_DWORD /d 1 /f >nul

:: Special Buffering + Buffer Chaining optimizations
reg add "HKLM\SYSTEM\CurrentControlSet\Services\AFD\Parameters" /v NonBlockingSendSpecialBuffering /t REG_DWORD /d 1 /f >nul
reg add "HKLM\SYSTEM\CurrentControlSet\Services\AFD\Parameters" /v DoNotUseBufferChaining /t REG_DWORD /d 1 /f >nul

:: Priority Boost â€” à¹€à¸žà¸´à¹ˆà¸¡ priority à¹ƒà¸«à¹‰ network I/O
reg add "HKLM\SYSTEM\CurrentControlSet\Services\AFD\Parameters" /v PriorityBoost /t REG_DWORD /d 1 /f >nul

:: Port Reuse Range
reg add "HKLM\SYSTEM\CurrentControlSet\Services\AFD\Parameters" /v ReusePortMinimum /t REG_DWORD /d 256 /f >nul
reg add "HKLM\SYSTEM\CurrentControlSet\Services\AFD\Parameters" /v ReusePortUpper /t REG_DWORD /d 65535 /f >nul

echo     âœ“ AFD à¹€à¸ªà¸£à¹‡à¸ˆ

:: ===========================================
:: 5. DNS Cache
:: ===========================================
echo [5/7] DNS Cache...

:: MaxCacheTtl â€” à¹€à¸à¹‡à¸š DNS record à¹„à¸§à¹‰à¸ªà¸¹à¸‡à¸ªà¸¸à¸”à¸à¸µà¹ˆà¸§à¸´à¸™à¸²à¸—à¸µ (1 à¸Šà¸±à¹ˆà¸§à¹‚à¸¡à¸‡)
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Dnscache\Parameters" /v MaxCacheTtl /t REG_DWORD /d 3600 /f >nul

:: Negative Cache â€” à¹„à¸¡à¹ˆà¹€à¸à¹‡à¸š DNS query à¸—à¸µà¹ˆ fail (0 = à¸›à¸´à¸”)
:: à¸Šà¹ˆà¸§à¸¢à¹ƒà¸«à¹‰ retry DNS à¹„à¸”à¹‰à¹€à¸£à¹‡à¸§à¸‚à¸¶à¹‰à¸™à¸–à¹‰à¸² DNS server à¸•à¸­à¸šà¹„à¸¡à¹ˆà¹„à¸”à¹‰à¸Šà¸±à¹ˆà¸§à¸„à¸£à¸²à¸§
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Dnscache\Parameters" /v MaxNegativeCacheTtl /t REG_DWORD /d 0 /f >nul
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Dnscache\Parameters" /v NegativeCacheTime /t REG_DWORD /d 0 /f >nul
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Dnscache\Parameters" /v NetFailureCacheTime /t REG_DWORD /d 0 /f >nul

:: Cache Hash Table + Max Size
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Dnscache\Parameters" /v CacheHashTableBucketSize /t REG_DWORD /d 1 /f >nul
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Dnscache\Parameters" /v MaximumCacheSize /t REG_DWORD /d 16384 /f >nul

echo     âœ“ DNS Cache à¹€à¸ªà¸£à¹‡à¸ˆ

:: ===========================================
:: 6. NetBT (NetBIOS over TCP/IP)
:: ===========================================
echo [6/7] NetBT...

:: à¸¥à¸” timeout à¸‚à¸­à¸‡ NetBIOS name resolution
reg add "HKLM\SYSTEM\CurrentControlSet\Services\NetBT\Parameters" /v BcastNameQueryCount /t REG_DWORD /d 1 /f >nul
reg add "HKLM\SYSTEM\CurrentControlSet\Services\NetBT\Parameters" /v BcastQueryTimeout /t REG_DWORD /d 50 /f >nul
reg add "HKLM\SYSTEM\CurrentControlSet\Services\NetBT\Parameters" /v NameSrvQueryCount /t REG_DWORD /d 1 /f >nul
reg add "HKLM\SYSTEM\CurrentControlSet\Services\NetBT\Parameters" /v NameSrvQueryTimeout /t REG_DWORD /d 50 /f >nul
reg add "HKLM\SYSTEM\CurrentControlSet\Services\NetBT\Parameters" /v CacheTimeout /t REG_DWORD /d 160000 /f >nul
reg add "HKLM\SYSTEM\CurrentControlSet\Services\NetBT\Parameters" /v SessionKeepAlive /t REG_DWORD /d 4800000 /f >nul
reg add "HKLM\SYSTEM\CurrentControlSet\Services\NetBT\Parameters" /v InitialBackoff /t REG_DWORD /d 100 /f >nul
reg add "HKLM\SYSTEM\CurrentControlSet\Services\NetBT\Parameters" /v MaximumBackoff /t REG_DWORD /d 600 /f >nul
reg add "HKLM\SYSTEM\CurrentControlSet\Services\NetBT\Parameters" /v MinimumTimeout /t REG_DWORD /d 1 /f >nul

echo     âœ“ NetBT à¹€à¸ªà¸£à¹‡à¸ˆ

:: ===========================================
:: 7. NDIS + Memory Management
:: ===========================================
echo [7/7] NDIS + Memory...

reg add "HKLM\SYSTEM\CurrentControlSet\Services\Ndis\Parameters" /v ProcessorAffinityMask /t REG_DWORD /d 0 /f >nul
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Ndis\Parameters" /v LogicalProcessorsPerPhysicalProcessor /t REG_DWORD /d 1 /f >nul

:: Memory Management â€” à¹€à¸žà¸´à¹ˆà¸¡ Session Pool + Large Page
reg add "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management" /v SessionPoolSize /t REG_DWORD /d 524288 /f >nul
reg add "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management" /v LargePageMinimum /t REG_DWORD /d 2097152 /f >nul

:: à¸¥à¸šà¸„à¹ˆà¸²à¸—à¸µà¹ˆà¹„à¸¡à¹ˆà¸ˆà¸³à¹€à¸›à¹‡à¸™
reg delete "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\ServiceProvider" /v Class /f >nul 2>&1
reg delete "HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\ServiceProvider" /v LocalAddressSortList /f >nul 2>&1

echo     âœ“ NDIS + Memory à¹€à¸ªà¸£à¹‡à¸ˆ

echo.
echo =============================================
echo  âœ… Registry Tweaks à¸—à¸±à¹‰à¸‡à¸«à¸¡à¸”à¹€à¸ªà¸£à¹‡à¸ˆà¹€à¸£à¸µà¸¢à¸šà¸£à¹‰à¸­à¸¢!
echo  âš  à¸à¸£à¸¸à¸“à¸² Restart Windows à¹€à¸žà¸·à¹ˆà¸­à¹ƒà¸«à¹‰à¸¡à¸µà¸œà¸¥
echo  ðŸ“ Backup files à¸­à¸¢à¸¹à¹ˆà¸—à¸µà¹ˆ Documents\KingR9Tools\Backup
echo =============================================
