# ============================================================
#  R9 Settings - Network Adapter Optimization
#  สคริปต์ปรับค่า Network Adapter (ทุกตัวที่เชื่อมต่ออยู่)
#  แกะมาจาก R9 Settings.exe
# ============================================================
#  ⚠️ ต้องรัน PowerShell ด้วยสิทธิ์ Admin
#  คลิกขวา PowerShell > Run as Administrator
# ============================================================

$adapters = Get-NetAdapter | Where-Object Status -eq 'Up'

foreach ($adapter in $adapters) {
    $n = $adapter.Name
    Write-Host "=== กำลังปรับค่า: $n ($($adapter.InterfaceDescription)) ===" -ForegroundColor Cyan

    # ─── กลุ่ม 1: ปิดฟีเจอร์ประหยัดพลังงาน ───
    # เหตุผล: ฟีเจอร์พวกนี้ทำให้การ์ดเน็ต "หลับ" เพื่อประหยัดไฟ แต่ทำให้ latency สูงขึ้น

    # Advanced EEE (Energy Efficient Ethernet ขั้นสูง) → ปิด
    # ลด latency โดยไม่ให้การ์ดเน็ตเข้า power saving mode
    Set-NetAdapterAdvancedProperty -Name $n -DisplayName 'Advanced EEE' -DisplayValue 'Disabled' -EA SilentlyContinue

    # Energy-Efficient Ethernet → ปิด
    # เหมือน Advanced EEE — ป้องกันการ์ดเน็ตลดความเร็วเพื่อประหยัดไฟ
    Set-NetAdapterAdvancedProperty -Name $n -DisplayName 'Energy-Efficient Ethernet' -DisplayValue 'Disabled' -EA SilentlyContinue

    # Gigabit Lite → ปิด
    # บังคับใช้ความเร็วเต็มแทนที่จะลดลงเพื่อประหยัดไฟ
    Set-NetAdapterAdvancedProperty -Name $n -DisplayName 'Gigabit Lite' -DisplayValue 'Disabled' -EA SilentlyContinue

    # Green Ethernet → ปิด
    # ปิดโหมดประหยัดพลังงานของสายแลน
    Set-NetAdapterAdvancedProperty -Name $n -DisplayName 'Green Ethernet' -DisplayValue 'Disabled' -EA SilentlyContinue

    # Power Saving Mode → ปิด
    # ห้ามการ์ดเน็ตเข้าโหมดประหยัดไฟ
    Set-NetAdapterAdvancedProperty -Name $n -DisplayName 'Power Saving Mode' -DisplayValue 'Disabled' -EA SilentlyContinue

    # ─── กลุ่ม 2: ปิด Wake-on-LAN (WoL) ───
    # เหตุผล: WoL ทำให้การ์ดเน็ตคอยฟัง magic packet ตลอด → เปลืองทรัพยากร

    # Wake on Magic Packet → ปิด
    Set-NetAdapterAdvancedProperty -Name $n -DisplayName 'Wake on Magic Packet' -DisplayValue 'Disabled' -EA SilentlyContinue

    # Wake on magic packet (S0ix) → ปิด
    Set-NetAdapterAdvancedProperty -Name $n -DisplayName 'Wake on magic packet when system is in the S0ix power state' -DisplayValue 'Disabled' -EA SilentlyContinue

    # Wake on pattern match → ปิด
    Set-NetAdapterAdvancedProperty -Name $n -DisplayName 'Wake on pattern match' -DisplayValue 'Disabled' -EA SilentlyContinue

    # Shutdown Wake-On-Lan → ปิด
    Set-NetAdapterAdvancedProperty -Name $n -DisplayName 'Shutdown Wake-On-Lan' -DisplayValue 'Disabled' -EA SilentlyContinue

    # WOL & Shutdown Link Speed → ไม่ลดความเร็ว
    # ป้องกันการ์ดเน็ตลดความเร็วตอนปิดเครื่อง
    Set-NetAdapterAdvancedProperty -Name $n -DisplayName 'WOL & Shutdown Link Speed' -DisplayValue 'Not Speed Down' -EA SilentlyContinue

    # ─── กลุ่ม 3: ปิด Offload ที่ไม่จำเป็น ───
    # เหตุผล: Offload ให้ NIC ทำแทน CPU ฟังดูดี แต่บางกรณีมันเพิ่ม latency
    # เพราะ NIC ต้อง "รอสะสม" packet ก่อนส่ง — สำหรับเกมเรา้ต้องการ low-latency

    # ARP Offload → ปิด
    # ปกติ NIC จะตอบ ARP เอง → ปิดให้ OS จัดการ
    Set-NetAdapterAdvancedProperty -Name $n -DisplayName 'ARP Offload' -DisplayValue 'Disabled' -EA SilentlyContinue

    # NS Offload → ปิด
    # เหมือน ARP แต่สำหรับ IPv6 Neighbor Solicitation
    Set-NetAdapterAdvancedProperty -Name $n -DisplayName 'NS Offload' -DisplayValue 'Disabled' -EA SilentlyContinue

    # IPv4 Checksum Offload → ปิด
    # ให้ CPU คำนวณ checksum แทน NIC → ลด latency
    Set-NetAdapterAdvancedProperty -Name $n -DisplayName 'IPv4 Checksum Offload' -DisplayValue 'Disabled' -EA SilentlyContinue

    # Large Send Offload v2 (IPv4) → ปิด
    # LSO ให้ NIC แบ่ง packet ใหญ่ → ปิดเพื่อลด latency (packet ถูกส่งทันที)
    Set-NetAdapterAdvancedProperty -Name $n -DisplayName 'Large Send Offload v2 (IPv4)' -DisplayValue 'Disabled' -EA SilentlyContinue

    # Large Send Offload v2 (IPv6) → ปิด
    Set-NetAdapterAdvancedProperty -Name $n -DisplayName 'Large Send Offload v2 (IPv6)' -DisplayValue 'Disabled' -EA SilentlyContinue

    # Jumbo Frame → ปิด
    # Jumbo = packet ใหญ่กว่าปกติ (>1500 bytes) → ไม่จำเป็นสำหรับเกม, อาจทำให้ packet loss
    Set-NetAdapterAdvancedProperty -Name $n -DisplayName 'Jumbo Frame' -DisplayValue 'Disabled' -EA SilentlyContinue

    # ─── กลุ่ม 4: ปิด Flow Control ───
    # เหตุผล: Flow Control ให้อีกฝั่งสั่ง "หยุดส่ง" ได้ → เพิ่ม latency

    # Flow Control → ปิด
    Set-NetAdapterAdvancedProperty -Name $n -DisplayName 'Flow Control' -DisplayValue 'Disabled' -EA SilentlyContinue

    # ─── กลุ่ม 5: เปิดฟีเจอร์ที่ช่วย performance ───

    # Interrupt Moderation → เปิด
    # รวม interrupt หลายตัวเป็น 1 → ลดภาระ CPU (แต่เพิ่ม latency นิดหน่อย)
    # สำหรับเกมส่วนใหญ่ "เปิด" ดีกว่าเพราะ CPU ไม่ต้องรับ interrupt ทุก packet
    Set-NetAdapterAdvancedProperty -Name $n -DisplayName 'Interrupt Moderation' -DisplayValue 'Enabled' -EA SilentlyContinue

    # Receive Side Scaling (RSS) → เปิด
    # กระจายการรับ packet ไปหลาย CPU core → เพิ่ม throughput
    Set-NetAdapterAdvancedProperty -Name $n -DisplayName 'Receive Side Scaling' -DisplayValue 'Enabled' -EA SilentlyContinue

    # Maximum Number of RSS Queues → 4 Queues
    # ใช้ 4 core ในการรับ packet — เหมาะกับ CPU 4+ core
    Set-NetAdapterAdvancedProperty -Name $n -DisplayName 'Maximum Number of RSS Queues' -DisplayValue '4 Queues' -EA SilentlyContinue

    # Priority & VLAN → เปิด
    # ให้รองรับ QoS (ส่ง DSCP marking ได้) + VLAN tagging
    Set-NetAdapterAdvancedProperty -Name $n -DisplayName 'Priority & VLAN' -DisplayValue 'Priority & VLAN Enabled' -EA SilentlyContinue

    # ─── กลุ่ม 6: ปรับ Buffer ───

    # Receive Buffers → 4096
    # เพิ่ม buffer สำหรับรับ packet → ลด packet drop ตอนโหลดหนัก
    # ค่าเดิมปกติ 256-512, เพิ่มเป็น 4096
    Set-NetAdapterAdvancedProperty -Name $n -DisplayName 'Receive Buffers' -DisplayValue '4096' -EA SilentlyContinue

    # Transmit Buffers → 128
    # ลด buffer ส่ง → บังคับส่ง packet เร็วขึ้น ลด latency
    Set-NetAdapterAdvancedProperty -Name $n -DisplayName 'Transmit Buffers' -DisplayValue '128' -EA SilentlyContinue

    # ─── กลุ่ม 7: เปิด TCP/UDP Checksum (Rx & Tx) ───
    # เหตุผล: ให้ NIC ช่วยตรวจ checksum สำหรับ TCP/UDP → ดีกว่าปิดหมด

    Set-NetAdapterAdvancedProperty -Name $n -DisplayName 'TCP Checksum Offload (IPv4)' -DisplayValue 'Rx & Tx Enabled' -EA SilentlyContinue
    Set-NetAdapterAdvancedProperty -Name $n -DisplayName 'TCP Checksum Offload (IPv6)' -DisplayValue 'Rx & Tx Enabled' -EA SilentlyContinue
    Set-NetAdapterAdvancedProperty -Name $n -DisplayName 'UDP Checksum Offload (IPv4)' -DisplayValue 'Rx & Tx Enabled' -EA SilentlyContinue
    Set-NetAdapterAdvancedProperty -Name $n -DisplayName 'UDP Checksum Offload (IPv6)' -DisplayValue 'Rx & Tx Enabled' -EA SilentlyContinue

    # ─── กลุ่ม 8: ตั้งความเร็วเต็ม ───

    # Speed & Duplex → 2.5 Gbps Full Duplex
    # บังคับความเร็วเต็ม (ถ้าการ์ดรองรับ) — ถ้าไม่รองรับ 2.5G จะ error ได้เฉยๆ
    Set-NetAdapterAdvancedProperty -Name $n -DisplayName 'Speed & Duplex' -DisplayValue '2.5 Gbps Full Duplex' -EA SilentlyContinue

    # EEE Max Support Speed → 2.5 Gbps Full Duplex
    Set-NetAdapterAdvancedProperty -Name $n -DisplayName 'EEE Max Support Speed' -DisplayValue '2.5 Gbps Full Duplex' -EA SilentlyContinue

    Write-Host "✓ ปรับค่าเสร็จสำหรับ $n" -ForegroundColor Green
}

# ─── ตั้งค่า TCP Auto-Tuning Level ───
# normal = ให้ Windows ปรับ TCP Window Size อัตโนมัติ
netsh int tcp set global autotuninglevel=normal | Out-Null

# ─── เปิด Ultimate Performance Power Plan ───
# e9a42b02-... = GUID ของ Ultimate Performance plan
powercfg -duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61 | Out-Null

# ─── บังคับ Group Policy Update ───
Start-Process gpupdate -ArgumentList '/force' -WindowStyle Hidden -ErrorAction SilentlyContinue

Write-Host "`n✅ Network Adapter Optimization เสร็จสมบูรณ์!" -ForegroundColor Green
