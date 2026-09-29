# ============================================================
#  R9 Settings - Netsh TCP Global + MTU + Latency Registry
#  กลุ่ม "Windows OS Services Tweaks" → Netsh TCP + Low Latency Keys
#  แกะมาจาก R9 Settings.exe (tweak-netsh-tcp + tweak-latency-reg)
# ============================================================
#  ⚠️ ต้องรัน PowerShell ด้วยสิทธิ์ Admin
# ============================================================

Write-Host "=== Netsh TCP Global + MTU + Latency Registry ===" -ForegroundColor Cyan

# ╔═══════════════════════════════════════════════════════════╗
# ║  ส่วนที่ 1: TCP Global Settings (netsh)                  ║
# ╚═══════════════════════════════════════════════════════════╝

# Auto-Tuning Level = normal → ให้ Windows ปรับ TCP Receive Window อัตโนมัติ
netsh int tcp set global autotuninglevel=normal

# Chimney Offload = disabled → ถูกยกเลิกใน Windows 8+; เปิดบนไดรเวอร์เก่าอาจเพิ่ม latency
netsh int tcp set global chimney=disabled

# Receive-Side Scaling = enabled → กระจายการรับ packet ไปหลาย CPU core
netsh int tcp set global rss=enabled

# NetDMA = enabled → ให้ DMA ช่วยย้ายข้อมูลเครือข่าย ลดภาระ CPU
netsh int tcp set global netdma=enabled

# Direct Cache Access = enabled → CPU เข้าถึง cache ข้อมูลเน็ตโดยตรง
netsh int tcp set global dca=enabled

# ECN Capability = disabled → ปิด Explicit Congestion Notification
# (ลด overhead ต่อ connection แต่เสียการแจ้งเตือน congestion ล่วงหน้า)
netsh int tcp set global ecncapability=disabled

# TCP Timestamps = disabled → ตัด field timestamp ออกจากทุก packet
# → header เล็กลง ลด overhead สำหรับ latency
netsh int tcp set global timestamps=disabled

# TCP Heuristics = disabled → ปิดการปรับ window แบบ heuristic ของ Windows
# (ป้องกัน Windows ลด window size จน throughput ตก)
netsh int tcp set heuristics disabled

# ╔═══════════════════════════════════════════════════════════╗
# ║  ส่วนที่ 2: MTU 1492 ทุก interface                       ║
# ║  1492 = ค่าสำหรับ PPPoE (ADSL/Fiber แบบแท็ก 8 ไบต์)      ║
# ║  ลด fragmentation → packet ไม่ถูกแบ่งระหว่างทาง          ║
# ╚═══════════════════════════════════════════════════════════╝

netsh interface ipv4 set subinterface "Ethernet" mtu=1492 store=persistent 2>$null
netsh interface ipv4 set subinterface "Wi-Fi" mtu=1492 store=persistent 2>$null

# ไล่ตั้งทุก IPv4 interface ที่เหลือด้วย
Get-NetIPInterface -AddressFamily IPv4 -ErrorAction SilentlyContinue |
    ForEach-Object { netsh interface ipv4 set subinterface $_.InterfaceIndex mtu=1492 store=persistent 2>$null }

# ╔═══════════════════════════════════════════════════════════╗
# ║  ส่วนที่ 3: Latency Registry (Low Latency Keys)          ║
# ╚═══════════════════════════════════════════════════════════╝

$tcpipPath = 'HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters'

# TcpAckFrequency = 1 → ACK ทุก packet ทันที (ปกติ ACK ทุก 2 packet = delayed ACK)
#   → ลด latency ฝั่ง upload เพราะฝั่งรับไม่ต้องรอ
reg add "$tcpipPath" /v TcpAckFrequency /t REG_DWORD /d 1 /f 2>$null

# TCPNoDelay = 1 → ปิด Nagle's Algorithm
#   Nagle = รวม packet เล็กๆ ก่อนส่ง (ประหยัด bandwidth แต่เพิ่ม delay)
#   ปิดแล้วเกมส่ง packet ทันที → latency ต่ำ
reg add "$tcpipPath" /v TCPNoDelay /t REG_DWORD /d 1 /f 2>$null

# TcpDelAckTicks = 0 → ปิด delayed-ACK timer สนิท (เสริมจาก TcpAckFrequency)
reg add "$tcpipPath" /v TcpDelAckTicks /t REG_DWORD /d 0 /f 2>$null

# DisableTaskOffload = 0 → เปิดให้ NIC ช่วยทำ checksum/offload (ค่าปกติ)
reg add "$tcpipPath" /v DisableTaskOffload /t REG_DWORD /d 0 /f 2>$null

# ─── ตั้งค่าเดียวกันกับ "ทุก network interface" ที่มีอยู่ ───
Get-ChildItem "HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces" -ErrorAction SilentlyContinue |
    ForEach-Object {
        Set-ItemProperty -Path $_.PSPath -Name "TcpAckFrequency" -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue
        Set-ItemProperty -Path $_.PSPath -Name "TCPNoDelay"       -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue
        Set-ItemProperty -Path $_.PSPath -Name "TcpDelAckTicks"   -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
    }

# ─── MSMQ: ปิด Nagle ของ Microsoft Message Queue ด้วย ───
reg add "HKLM\SOFTWARE\Microsoft\MSMQ\Parameters" /v TCPNoDelay /t REG_DWORD /d 1 /f 2>$null

# ─── Psched: NonBestEffortLimit = 0 ───
# ปกติ Windows สงวน bandwidth 20% ให้ QoS (QoS Packet Scheduler)
# ตั้ง 0 = ไม่สงวนเลย → app ใช้ bandwidth ได้เต็ม 100%
reg add "HKLM\SOFTWARE\Policies\Microsoft\Windows\Psched" /v NonBestEffortLimit /t REG_DWORD /d 0 /f 2>$null

# ─── Dnscache: ปรับ TTL ของ DNS cache ───
# MaxCacheTtl = 86400 (cache ผล DNS นาน 1 วัน → resolve น้อยลง)
# MaxNegativeCacheTtl = 5 (ผลล้มเหลว cache แค่ 5 วินาที)
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Dnscache\Parameters" /v MaxCacheTtl        /t REG_DWORD /d 86400 /f 2>$null
reg add "HKLM\SYSTEM\CurrentControlSet\Services\Dnscache\Parameters" /v MaxNegativeCacheTtl /t REG_DWORD /d 5     /f 2>$null

Write-Host "`n✅ Netsh TCP + MTU + Latency Registry เสร็จสมบูรณ์!" -ForegroundColor Green
Write-Host "⚠️ แนะนำรีสตาร์ทเครื่องเพื่อให้ค่า TCP ทั้งหมดมีผลครบ" -ForegroundColor Yellow