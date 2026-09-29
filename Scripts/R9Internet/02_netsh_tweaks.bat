@echo off
:: =====================================================================
:: InternetR9 - Network Optimization: Netsh Commands
:: =====================================================================
:: à¸ªà¸´à¹ˆà¸‡à¸—à¸µà¹ˆà¹„à¸Ÿà¸¥à¹Œà¸™à¸µà¹‰à¸—à¸³: à¸›à¸£à¸±à¸šà¸„à¹ˆà¸² TCP/IP stack à¸œà¹ˆà¸²à¸™ netsh command
:: âš  à¸•à¹‰à¸­à¸‡ Run as Administrator
:: =====================================================================

echo =============================================
echo  InternetR9 - Netsh TCP/IP Tuning
echo =============================================
echo.

:: --- TCP Heuristics ---
:: à¸›à¸´à¸” heuristics à¸—à¸µà¹ˆ Windows à¹ƒà¸Šà¹‰à¸›à¸£à¸±à¸š auto-tuning à¹€à¸­à¸‡
:: à¹€à¸žà¸·à¹ˆà¸­à¹ƒà¸«à¹‰à¹€à¸£à¸²à¸„à¸§à¸šà¸„à¸¸à¸¡à¸„à¹ˆà¸²à¹€à¸­à¸‡à¹„à¸”à¹‰
echo [1/18] à¸›à¸´à¸” TCP Heuristics...
netsh int tcp set heuristics disabled

:: --- ECN (Explicit Congestion Notification) ---
:: à¸›à¸´à¸” ECN à¹€à¸žà¸£à¸²à¸° router/ISP à¹€à¸à¹ˆà¸²à¸šà¸²à¸‡à¸•à¸±à¸§à¹„à¸¡à¹ˆà¸£à¸­à¸‡à¸£à¸±à¸š
:: à¸—à¸³à¹ƒà¸«à¹‰ packet à¸–à¸¹à¸à¸—à¸´à¹‰à¸‡à¹à¸—à¸™à¸—à¸µà¹ˆà¸ˆà¸°à¸šà¸­à¸ congestion
echo [2/18] à¸›à¸´à¸” ECN...
netsh int tcp set global ecncapability=disabled

:: --- ISATAP (IPv6 tunneling) ---
:: à¸›à¸´à¸” ISATAP tunnel â€” à¹„à¸¡à¹ˆà¸ˆà¸³à¹€à¸›à¹‡à¸™à¹ƒà¸™à¹€à¸™à¹‡à¸•à¸šà¹‰à¸²à¸™à¸—à¸±à¹ˆà¸§à¹„à¸›
:: à¸¥à¸” overhead à¸ˆà¸²à¸ IPv6 tunneling à¸—à¸µà¹ˆà¹„à¸¡à¹ˆà¹„à¸”à¹‰à¹ƒà¸Šà¹‰
echo [3/18] à¸›à¸´à¸” ISATAP...
netsh interface isatap set state disabled

:: --- TCP Timestamps ---
:: à¸›à¸´à¸” TCP timestamp â€” à¸¥à¸”à¸‚à¸™à¸²à¸” TCP header 12 bytes à¸•à¹ˆà¸­ packet
:: âš  à¸­à¸²à¸ˆà¸ªà¹ˆà¸‡à¸œà¸¥à¸à¸±à¸š PAWS (Protection Against Wrapped Sequences)
echo [4/18] à¸›à¸´à¸” TCP Timestamps...
netsh int tcp set global timestamps=disabled

:: --- Non-SACK RTT Resiliency ---
:: à¸›à¸´à¸” â€” à¹„à¸¡à¹ˆà¸•à¹‰à¸­à¸‡à¸à¸²à¸£ fallback RTT calculation à¹€à¸¡à¸·à¹ˆà¸­à¹„à¸¡à¹ˆà¸¡à¸µ SACK
echo [5/18] à¸›à¸´à¸” Non-SACK RTT Resiliency...
netsh int tcp set global nonsackrttresiliency=disabled

:: --- Initial RTO (Retransmission Timeout) ---
:: à¸„à¹ˆà¸² 1000ms (1 à¸§à¸´à¸™à¸²à¸—à¸µ) â€” à¸„à¹ˆà¸² default à¸„à¸·à¸­ 3000ms
:: à¸ªà¹ˆà¸‡à¸‹à¹‰à¸³à¹€à¸£à¹‡à¸§à¸‚à¸¶à¹‰à¸™à¸–à¹‰à¸² packet à¹à¸£à¸à¸«à¸²à¸¢ â†’ à¹€à¸Šà¸·à¹ˆà¸­à¸¡à¸•à¹ˆà¸­à¹€à¸£à¹‡à¸§à¸‚à¸¶à¹‰à¸™
echo [6/18] à¸•à¸±à¹‰à¸‡ Initial RTO = 1000ms...
netsh int tcp set global initialRto=1000

:: --- Congestion Provider = CUBIC ---
:: à¹ƒà¸Šà¹‰ CUBIC algorithm à¸ªà¸³à¸«à¸£à¸±à¸šà¸—à¸¸à¸ template (internet/compat/custom)
:: CUBIC à¸”à¸µà¸à¸§à¹ˆà¸² NewReno à¸ªà¸³à¸«à¸£à¸±à¸š high-bandwidth, high-latency links
:: à¹ƒà¸Šà¹‰à¸à¸±à¸™à¸—à¸±à¹ˆà¸§à¹„à¸›à¹ƒà¸™ Linux / modern networks
echo [7/18] à¸•à¸±à¹‰à¸‡ Congestion Provider = CUBIC...
netsh int tcp set supplemental template=internet congestionprovider=cubic
netsh int tcp set supplemental template=compat congestionprovider=cubic
netsh int tcp set supplemental template=custom congestionprovider=cubic

:: --- Task Offload ---
:: à¹€à¸›à¸´à¸” â€” à¹ƒà¸«à¹‰ NIC à¸Šà¹ˆà¸§à¸¢à¸„à¸³à¸™à¸§à¸“ checksum, segmentation à¹à¸—à¸™ CPU
echo [8/18] à¹€à¸›à¸´à¸” Task Offload...
netsh int ip set global taskoffload=enabled

:: --- Auto-Tuning Level = Restricted ---
:: à¸ˆà¸³à¸à¸±à¸”à¸à¸²à¸£à¸‚à¸¢à¸²à¸¢ receive window à¸­à¸±à¸•à¹‚à¸™à¸¡à¸±à¸•à¸´
:: "restricted" = à¸‚à¸¢à¸²à¸¢à¹„à¸”à¹‰à¸šà¹‰à¸²à¸‡à¹à¸•à¹ˆà¹„à¸¡à¹ˆà¸¡à¸²à¸
:: à¸Šà¹ˆà¸§à¸¢à¸à¸±à¸š ISP à¸šà¸²à¸‡à¹€à¸ˆà¹‰à¸²à¸—à¸µà¹ˆà¸ˆà¸³à¸à¸±à¸” window size
:: à¸„à¹ˆà¸²à¸­à¸·à¹ˆà¸™à¸—à¸µà¹ˆà¹€à¸¥à¸·à¸­à¸à¹„à¸”à¹‰: normal, highlyrestricted, disabled, experimental
echo [9/18] à¸•à¸±à¹‰à¸‡ Auto-Tuning = Restricted...
netsh int tcp set global autotuninglevel=restricted

:: --- RSC (Receive Segment Coalescing) ---
:: à¸›à¸´à¸” â€” RSC à¸£à¸§à¸¡ packet à¸«à¸¥à¸²à¸¢à¸•à¸±à¸§à¹€à¸›à¹‡à¸™à¸à¹‰à¸­à¸™à¹ƒà¸«à¸à¹ˆ
:: à¸”à¸µà¸ªà¸³à¸«à¸£à¸±à¸š throughput à¹à¸•à¹ˆà¹€à¸žà¸´à¹ˆà¸¡ latency â†’ à¸›à¸´à¸”à¹€à¸žà¸·à¹ˆà¸­à¸¥à¸” latency
echo [10/18] à¸›à¸´à¸” RSC...
netsh int tcp set global rsc=disabled

:: --- RSS (Receive Side Scaling) ---
:: à¹€à¸›à¸´à¸” â€” à¸à¸£à¸°à¸ˆà¸²à¸¢ network interrupt à¹„à¸›à¸«à¸¥à¸²à¸¢ CPU core
:: à¸”à¸µà¸ªà¸³à¸«à¸£à¸±à¸š multi-core CPU â†’ à¸›à¸£à¸°à¸¡à¸§à¸¥à¸œà¸¥ packet à¹„à¸”à¹‰à¹€à¸£à¹‡à¸§à¸‚à¸¶à¹‰à¸™
echo [11/18] à¹€à¸›à¸´à¸” RSS...
netsh int tcp set global rss=enabled

:: --- DCA (Direct Cache Access) ---
:: à¹€à¸›à¸´à¸” â€” à¹ƒà¸«à¹‰ NIC à¹€à¸‚à¸µà¸¢à¸™à¸‚à¹‰à¸­à¸¡à¸¹à¸¥à¸•à¸£à¸‡à¹€à¸‚à¹‰à¸² CPU cache
:: à¸¥à¸” memory latency à¸ªà¸³à¸«à¸£à¸±à¸š network data
echo [12/18] à¹€à¸›à¸´à¸” DCA...
netsh int tcp set global dca=enabled

:: --- TCP Fast Open ---
:: à¹€à¸›à¸´à¸” â€” à¸ªà¹ˆà¸‡à¸‚à¹‰à¸­à¸¡à¸¹à¸¥à¸žà¸£à¹‰à¸­à¸¡ SYN packet à¸•à¸±à¹‰à¸‡à¹à¸•à¹ˆ handshake à¹à¸£à¸
:: à¸¥à¸” round-trip 1 à¸„à¸£à¸±à¹‰à¸‡ â†’ à¹€à¸›à¸´à¸”à¹€à¸§à¹‡à¸šà¹€à¸£à¹‡à¸§à¸‚à¸¶à¹‰à¸™
echo [13/18] à¹€à¸›à¸´à¸” TCP Fast Open...
netsh int tcp set global fastopen=enabled
netsh int tcp set global fastopenfallback=enabled

:: --- ICMP Redirects ---
:: à¸›à¸´à¸” â€” à¹„à¸¡à¹ˆà¹ƒà¸«à¹‰ router à¹€à¸›à¸¥à¸µà¹ˆà¸¢à¸™à¹€à¸ªà¹‰à¸™à¸—à¸²à¸‡ packet à¸œà¹ˆà¸²à¸™ ICMP
:: à¹€à¸žà¸´à¹ˆà¸¡à¸„à¸§à¸²à¸¡à¸›à¸¥à¸­à¸”à¸ à¸±à¸¢ (à¸›à¹‰à¸­à¸‡à¸à¸±à¸™ ICMP redirect attack)
echo [14/18] à¸›à¸´à¸” ICMP Redirects...
netsh int ip set global icmpredirects=disabled

:: --- Multicasting ---
:: à¸›à¸´à¸” â€” à¹„à¸¡à¹ˆà¹ƒà¸Šà¹‰ multicast (IGMP) â†’ à¸¥à¸” traffic à¸—à¸µà¹ˆà¹„à¸¡à¹ˆà¸ˆà¸³à¹€à¸›à¹‡à¸™
echo [15/18] à¸›à¸´à¸” Multicasting...
netsh int ip set global multicasting=disabled

echo.
echo =============================================
echo  âœ… Netsh TCP/IP Tuning à¹€à¸ªà¸£à¹‡à¸ˆà¹€à¸£à¸µà¸¢à¸šà¸£à¹‰à¸­à¸¢!
echo  âš  à¸à¸£à¸¸à¸“à¸² Restart Windows à¹€à¸žà¸·à¹ˆà¸­à¹ƒà¸«à¹‰à¸¡à¸µà¸œà¸¥
echo =============================================
