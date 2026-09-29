@echo off
:: =====================================================================
:: InternetR9 - NIC (Network Adapter) Advanced Properties
:: =====================================================================
:: à¸ªà¸´à¹ˆà¸‡à¸—à¸µà¹ˆà¹„à¸Ÿà¸¥à¹Œà¸™à¸µà¹‰à¸—à¸³: à¸›à¸£à¸±à¸šà¸„à¹ˆà¸² advanced à¸‚à¸­à¸‡ NIC à¸—à¸¸à¸à¸•à¸±à¸§à¸œà¹ˆà¸²à¸™ Registry
::   à¸›à¸´à¸”à¸Ÿà¸µà¹€à¸ˆà¸­à¸£à¹Œà¸›à¸£à¸°à¸«à¸¢à¸±à¸”à¸žà¸¥à¸±à¸‡à¸‡à¸²à¸™, offload à¸—à¸µà¹ˆà¹„à¸¡à¹ˆà¸ˆà¸³à¹€à¸›à¹‡à¸™,
::   à¹€à¸žà¸´à¹ˆà¸¡ buffer, à¹€à¸›à¸´à¸” RSS/LLI
:: âš  à¸•à¹‰à¸­à¸‡ Run as Administrator
:: =====================================================================

echo =============================================
echo  InternetR9 - NIC Advanced Properties
echo =============================================
echo.

:: Path à¸‚à¸­à¸‡ NIC class à¹ƒà¸™ Registry
:: à¹à¸•à¹ˆà¸¥à¸° adapter à¸ˆà¸°à¸­à¸¢à¸¹à¹ˆà¹ƒà¸™ subkey 0001, 0002, ... à¸¯à¸¥à¸¯
set NICPATH=HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}

echo à¹ƒà¸Šà¹‰ PowerShell à¹€à¸žà¸·à¹ˆà¸­à¸›à¸£à¸±à¸šà¸„à¹ˆà¸² NIC à¸—à¸¸à¸à¸•à¸±à¸§à¸­à¸±à¸•à¹‚à¸™à¸¡à¸±à¸•à¸´...
echo.

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
"$nicRoot = 'HKLM:\SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}';" ^
"$adapters = Get-ChildItem $nicRoot -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -match '^\d{4}$' -and $_.GetValue('DriverDesc') -ne $null };" ^
"Write-Host ('à¸žà¸š NIC ' + $adapters.Count + ' à¸•à¸±à¸§');" ^
"" ^
"# === à¸£à¸²à¸¢à¸à¸²à¸£à¸„à¹ˆà¸²à¸—à¸µà¹ˆà¸›à¸£à¸±à¸š ===" ^
"$props = @{" ^
"    # --- à¸›à¸´à¸”à¸Ÿà¸µà¹€à¸ˆà¸­à¸£à¹Œà¸›à¸£à¸°à¸«à¸¢à¸±à¸”à¸žà¸¥à¸±à¸‡à¸‡à¸²à¸™ ---" ^
"    # à¸›à¸´à¸”à¸à¸²à¸£ sleep à¹€à¸¡à¸·à¹ˆà¸­à¸–à¸­à¸”à¸ªà¸²à¸¢ LAN" ^
"    '*DeviceSleepOnDisconnect' = '0'" ^
"    # à¸›à¸´à¸” Energy Efficient Ethernet (à¸¥à¸” speed à¹€à¸žà¸·à¹ˆà¸­à¸›à¸£à¸°à¸«à¸¢à¸±à¸”à¹„à¸Ÿ)" ^
"    '*EEE' = '0'" ^
"    # à¸›à¸´à¸” Flow Control (à¸ªà¹ˆà¸‡ PAUSE frame à¹€à¸¡à¸·à¹ˆà¸­ buffer à¹€à¸•à¹‡à¸¡)" ^
"    '*FlowControl' = '0'" ^
"    # à¸›à¸´à¸” Auto Power Save Mode" ^
"    'AutoPowerSaveModeEnabled' = '0'" ^
"    # à¸›à¸´à¸” Wake-on-LAN (Magic Packet)" ^
"    '*WakeOnMagicPacket' = '0'" ^
"    # à¸›à¸´à¸” Wake-on-Pattern" ^
"    '*WakeOnPattern' = '0'" ^
"    # à¸›à¸´à¸” Wake on Link Change" ^
"    'WakeOnLink' = '0'" ^
"    'WakeOnSlot' = '0'" ^
"    'WakeUpModeCap' = '0'" ^
"    # à¸›à¸´à¸” Selective Suspend (USB NIC)" ^
"    '*SelectiveSuspend' = '0'" ^
"    # à¸›à¸´à¸” EEE variants" ^
"    'EEELinkAdvertisement' = '0'" ^
"    'EeePhyEnable' = '0'" ^
"    'AdvancedEEE' = '0'" ^
"    # à¸›à¸´à¸” Green Ethernet (à¸¥à¸” speed à¸•à¸²à¸¡à¸ªà¸²à¸¢)" ^
"    'EnableGreenEthernet' = '0'" ^
"    'GigaLite' = '0'" ^
"    # à¸›à¸´à¸” Power Saving modes à¸—à¸¸à¸à¸•à¸±à¸§" ^
"    'PowerSavingMode' = '0'" ^
"    'ULPMode' = '0'" ^
"    'ReduceSpeedOnPowerDown' = '0'" ^
"    'PowerDownPll' = '0'" ^
"    'EnablePME' = '0'" ^
"    'EnableDynamicPowerGating' = '0'" ^
"    'EnableConnectedPowerGating' = '0'" ^
"    'NicAutoPowerSaver' = '0'" ^
"    'DisableDelayedPowerUp' = '0'" ^
"    '' = ''" ^
"    # --- à¸›à¸´à¸” Interrupt Moderation ---" ^
"    # Interrupt Moderation = NIC à¸£à¸­à¸ªà¸°à¸ªà¸¡ packet à¸à¹ˆà¸­à¸™à¸ªà¹ˆà¸‡ interrupt" ^
"    # à¸›à¸´à¸” = interrupt à¸—à¸¸à¸ packet â†’ latency à¸•à¹ˆà¸³à¸ªà¸¸à¸” (à¸”à¸µà¸ªà¸³à¸«à¸£à¸±à¸šà¹€à¸à¸¡)" ^
"    # âš  à¸­à¸²à¸ˆà¹€à¸žà¸´à¹ˆà¸¡ CPU usage à¹€à¸¥à¹‡à¸à¸™à¹‰à¸­à¸¢" ^
"    '*InterruptModeration' = '0'" ^
"    '*InterruptModerationRate' = '0'" ^
"    'DMACoalescing' = '0'" ^
"    'ITR' = '0'" ^
"    '' = ''" ^
"    # --- à¸›à¸´à¸” Offload à¸—à¸µà¹ˆà¹„à¸¡à¹ˆà¸ˆà¸³à¹€à¸›à¹‡à¸™ ---" ^
"    # LSO (Large Send Offload) = NIC à¹à¸šà¹ˆà¸‡ segment à¹à¸—à¸™ CPU" ^
"    # à¸›à¸´à¸”à¹€à¸žà¸£à¸²à¸°à¸šà¸²à¸‡ NIC à¸—à¸³à¹„à¸”à¹‰à¹„à¸¡à¹ˆà¸”à¸µ â†’ à¹€à¸žà¸´à¹ˆà¸¡ latency" ^
"    '*LsoV2IPv4' = '0'" ^
"    '*LsoV2IPv6' = '0'" ^
"    '*LsoV1IPv4' = '0'" ^
"    # à¸›à¸´à¸” Checksum Offload" ^
"    '*IPChecksumOffloadIPv4' = '0'" ^
"    '*TCPChecksumOffloadIPv4' = '0'" ^
"    '*TCPChecksumOffloadIPv6' = '0'" ^
"    '*UDPChecksumOffloadIPv4' = '0'" ^
"    '*UDPChecksumOffloadIPv6' = '0'" ^
"    # à¸›à¸´à¸” ARP/NS Offload (Wake-on-LAN related)" ^
"    '*PMARPOffload' = '0'" ^
"    '*PMNSOffload' = '0'" ^
"    '*PacketDirect' = '0'" ^
"    '' = ''" ^
"    # --- à¹€à¸›à¸´à¸”à¸Ÿà¸µà¹€à¸ˆà¸­à¸£à¹Œà¸—à¸µà¹ˆà¸Šà¹ˆà¸§à¸¢ ---" ^
"    # RSS = à¸à¸£à¸°à¸ˆà¸²à¸¢ packet à¹„à¸›à¸«à¸¥à¸²à¸¢ CPU core" ^
"    '*RSS' = '1'" ^
"    '*NumRssQueues' = '2'" ^
"    # à¹€à¸žà¸´à¹ˆà¸¡ Buffer (à¸„à¹ˆà¸² default à¸¡à¸±à¸à¹€à¸›à¹‡à¸™ 256)" ^
"    '*ReceiveBuffers' = '2048'" ^
"    '*TransmitBuffers' = '2048'" ^
"    # Priority VLAN Tag (à¸ˆà¸³à¹€à¸›à¹‡à¸™à¸ªà¸³à¸«à¸£à¸±à¸š QoS)" ^
"    '*PriorityVLANTag' = '1'" ^
"    # à¹„à¸¡à¹ˆà¸£à¸­ Auto-Negotiation à¹ƒà¸«à¹‰à¹€à¸ªà¸£à¹‡à¸ˆ â†’ connect à¹€à¸£à¹‡à¸§à¸‚à¸¶à¹‰à¸™" ^
"    'WaitAutoNegComplete' = '0'" ^
"    # à¹€à¸›à¸´à¸” Low Latency Interrupt" ^
"    'EnableLLI' = '1'" ^
"    # à¸›à¸´à¸” Coalescing" ^
"    'EnableCoalesce' = '0'" ^
"    'CoalesceBufferSize' = '2048'" ^
"    'EnableUDPTxScaling' = '0'" ^
"    # MTU = 1492 (à¹€à¸«à¸¡à¸²à¸°à¸à¸±à¸š PPPoE)" ^
"    'MTU' = '1492'" ^
"};" ^
"" ^
"$count = 0;" ^
"foreach ($adapter in $adapters) {" ^
"    $desc = $adapter.GetValue('DriverDesc');" ^
"    Write-Host ('  NIC: ' + $desc);" ^
"    foreach ($key in $props.Keys) {" ^
"        if ($key -eq '') { continue }" ^
"        try { Set-ItemProperty -Path $adapter.PSPath -Name $key -Value $props[$key] -ErrorAction SilentlyContinue } catch {}" ^
"    }" ^
"    # PnPCapabilities = 24 (DWord) â€” à¸›à¸´à¸” Power Management à¸‚à¸­à¸‡ NIC" ^
"    try { Set-ItemProperty -Path $adapter.PSPath -Name 'PnPCapabilities' -Value 24 -Type DWord -ErrorAction SilentlyContinue } catch {}" ^
"    $count++;" ^
"}" ^
"Write-Host '';" ^
"Write-Host ('âœ… à¸›à¸£à¸±à¸šà¸„à¹ˆà¸² NIC à¹€à¸ªà¸£à¹‡à¸ˆ ' + $count + ' à¸•à¸±à¸§')"

echo.
echo =============================================
echo  âœ… NIC Advanced Properties à¹€à¸ªà¸£à¹‡à¸ˆà¹€à¸£à¸µà¸¢à¸šà¸£à¹‰à¸­à¸¢!
echo  âš  à¸à¸£à¸¸à¸“à¸² Restart Windows à¹€à¸žà¸·à¹ˆà¸­à¹ƒà¸«à¹‰à¸¡à¸µà¸œà¸¥
echo =============================================
