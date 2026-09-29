# ============================================================
#  KingR9 Tools - AFD Buffer Pool Tuning (14)
#  ปรับ Winsock/AFD kernel buffer pool
#  สำหรับ FiveM / เกม latency ต่ำ
# ============================================================
#  ⚠️ ต้องรัน PowerShell ด้วยสิทธิ์ Admin
# ============================================================

Write-Host "=== AFD Buffer Pool Tuning ===" -ForegroundColor Cyan

$afd = 'HKLM:\SYSTEM\CurrentControlSet\Services\AFD\Parameters'
if (!(Test-Path $afd)) { New-Item -Path $afd -Force | Out-Null }

# ──────────────────────────────────────────────────────────────
# Buffer Pool Sizes
# AFD สร้าง buffer pool 4 ระดับ (Small/Medium/Large)
# สำหรับ game (packet เล็ก ส่งถี่) ควรเพิ่ม Small/Medium pool
# ──────────────────────────────────────────────────────────────

# SmallBufferSize: packet เล็กเช่น ACK, game state update (~4KB)
Set-ItemProperty -Path $afd -Name 'SmallBufferSize'  -Type DWord -Value 4096
Write-Host "  ✓ SmallBufferSize  = 4096 bytes" -ForegroundColor Green

# MediumBufferSize: packet ทั่วไป (~16KB)
Set-ItemProperty -Path $afd -Name 'MediumBufferSize' -Type DWord -Value 16384
Write-Host "  ✓ MediumBufferSize = 16384 bytes (16 KB)" -ForegroundColor Green

# LargeBufferSize: data chunk ใหญ่ เช่น texture stream (128KB)
Set-ItemProperty -Path $afd -Name 'LargeBufferSize'  -Type DWord -Value 131072
Write-Host "  ✓ LargeBufferSize  = 131072 bytes (128 KB)" -ForegroundColor Green

# ──────────────────────────────────────────────────────────────
# Buffer Pool Depth (จำนวน buffer ที่เตรียมไว้ใน pool)
# เพิ่ม = ลด overhead การจัดสรร memory ขณะเกมรัน
# ──────────────────────────────────────────────────────────────

# จำนวน small buffer พร้อมใช้ตลอดเวลา (game ส่ง ACK เยอะมาก)
Set-ItemProperty -Path $afd -Name 'SmallBufferListDepth'  -Type DWord -Value 16
Write-Host "  ✓ SmallBufferListDepth  = 16" -ForegroundColor Green

Set-ItemProperty -Path $afd -Name 'MediumBufferListDepth' -Type DWord -Value 8
Write-Host "  ✓ MediumBufferListDepth = 8" -ForegroundColor Green

# หมายเหตุ: ชื่อ key จริงใน Windows เขียนผิดเป็น "LargBuffer" (ไม่มี e)
Set-ItemProperty -Path $afd -Name 'LargBufferListDepth'   -Type DWord -Value 4
Write-Host "  ✓ LargBufferListDepth   = 4" -ForegroundColor Green

# ──────────────────────────────────────────────────────────────
# Performance Flags
# ──────────────────────────────────────────────────────────────

# DoNotHoldNICBuffers: ปล่อย NIC buffer กลับทันทีหลังส่ง
# 1 = ไม่ยึด buffer → driver ส่ง interrupt ได้เร็วขึ้น → ลด latency
Set-ItemProperty -Path $afd -Name 'DoNotHoldNICBuffers' -Type DWord -Value 1
Write-Host "  ✓ DoNotHoldNICBuffers = 1 (ปล่อย NIC buffer ทันที)" -ForegroundColor Green

# TransmitWorker: จำนวน kernel threads สำหรับส่งข้อมูล
# 32 = เพิ่มจาก default (0=auto) → ส่ง data ได้พร้อมกันหลาย thread
Set-ItemProperty -Path $afd -Name 'TransmitWorker' -Type DWord -Value 32
Write-Host "  ✓ TransmitWorker = 32 threads" -ForegroundColor Green

# BufferMultiplier: ตัวคูณ buffer pool ทั้งหมด
# 2 = ขยาย buffer x2 → รองรับ packet burst จาก server ได้ดีขึ้น
Set-ItemProperty -Path $afd -Name 'BufferMultiplier' -Type DWord -Value 2
Write-Host "  ✓ BufferMultiplier = 2 (ขยาย pool x2)" -ForegroundColor Green

Write-Host ""
Write-Host "✅ AFD Buffer Pool Tuning เสร็จสมบูรณ์!" -ForegroundColor Green
Write-Host "   หมายเหตุ: ไม่ต้อง Restart เครื่อง — มีผลทันที" -ForegroundColor DarkGray
