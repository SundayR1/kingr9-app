# ============================================================
#  KingR9 Tools - NIC Latency Tuning (15)
#  ปรับค่า NIC registry สำหรับลด interrupt latency
#  เพิ่มจาก 04_NIC_Registry_Properties.ps1 ต้นฉบับ (Aspas)
# ============================================================
#  ⚠️ ต้องรัน PowerShell ด้วยสิทธิ์ Admin
# ============================================================

Write-Host "=== NIC Latency Tuning ===" -ForegroundColor Cyan
Write-Host "  (ปรับค่า interrupt rate + TX delay สำหรับเกม)" -ForegroundColor DarkGray

$nicRoot = 'HKLM:\SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002bE10318}'
$adapters = Get-ChildItem $nicRoot -ErrorAction SilentlyContinue |
    Where-Object { $_.PSChildName -match '^\d{4}$' -and $_.GetValue('DriverDesc') -ne $null }

if (!$adapters -or $adapters.Count -eq 0) {
    Write-Host "  ⚠️ ไม่พบ NIC adapter ใน registry" -ForegroundColor Yellow
    exit 0
}

Write-Host "  พบ NIC $($adapters.Count) ตัว" -ForegroundColor DarkGray

foreach ($adapter in $adapters) {
    $desc = $adapter.GetValue('DriverDesc')
    $path = $adapter.PSPath
    Write-Host ""
    Write-Host "  NIC: $desc" -ForegroundColor Cyan

    # ──────────────────────────────────────────────────────────
    # ITR — Interrupt Throttle Rate (interrupts/sec)
    # ──────────────────────────────────────────────────────────
    # ยิ่งสูง = ตอบสนองเร็วแต่ CPU load สูงขึ้น
    # 976 = ~1ms ต่อ interrupt → ลด CPU overhead แต่ยังเร็วพอสำหรับเกม
    # 0   = Adaptive (ให้ NIC ตัดสินเอง) — บางไดรเวอร์รองรับ
    try {
        Set-ItemProperty -Path $path -Name 'ITR' -Value '976' -ErrorAction Stop
        Write-Host "    ✓ ITR = 976 (interrupt ทุก ~1ms)" -ForegroundColor Green
    } catch {
        Write-Host "    ~ ITR: ไดรเวอร์นี้ไม่รองรับ property นี้" -ForegroundColor DarkGray
    }

    # ──────────────────────────────────────────────────────────
    # TxIntDelay — Transmit Interrupt Delay (microseconds)
    # ──────────────────────────────────────────────────────────
    # delay ก่อนส่ง interrupt หลัง transmit เสร็จ
    # 0 = ไม่ delay → interrupt ทันที → latency ต่ำสุด
    try {
        Set-ItemProperty -Path $path -Name 'TxIntDelay' -Value '0' -ErrorAction Stop
        Write-Host "    ✓ TxIntDelay = 0 (interrupt ทันทีหลัง TX เสร็จ)" -ForegroundColor Green
    } catch {
        Write-Host "    ~ TxIntDelay: ไดรเวอร์นี้ไม่รองรับ property นี้" -ForegroundColor DarkGray
    }

    # ──────────────────────────────────────────────────────────
    # RxIntDelay — Receive Interrupt Delay (microseconds)
    # ──────────────────────────────────────────────────────────
    # 0 = interrupt ทันทีเมื่อรับ packet → latency ต่ำสุด (FiveM ใช้ UDP)
    try {
        Set-ItemProperty -Path $path -Name 'RxIntDelay' -Value '0' -ErrorAction Stop
        Write-Host "    ✓ RxIntDelay = 0 (interrupt ทันทีเมื่อรับ packet)" -ForegroundColor Green
    } catch {
        Write-Host "    ~ RxIntDelay: ไดรเวอร์นี้ไม่รองรับ property นี้" -ForegroundColor DarkGray
    }

    # ──────────────────────────────────────────────────────────
    # CoalesceBufferSize — ขนาด buffer ที่ใช้รวม interrupt
    # ──────────────────────────────────────────────────────────
    # เล็ก = interrupt ถี่ขึ้น → ลด latency (เหมาะกับ game)
    # ใหญ่ = throughput สูงแต่ latency สูงตาม
    try {
        Set-ItemProperty -Path $path -Name 'CoalesceBufferSize' -Value '1024' -ErrorAction Stop
        Write-Host "    ✓ CoalesceBufferSize = 1024 (interrupt buffer เล็ก)" -ForegroundColor Green
    } catch {
        Write-Host "    ~ CoalesceBufferSize: ไดรเวอร์นี้ไม่รองรับ property นี้" -ForegroundColor DarkGray
    }

    Write-Host "    ✓ ปรับค่า NIC เสร็จ" -ForegroundColor Green
}

Write-Host ""
Write-Host "✅ NIC Latency Tuning เสร็จสมบูรณ์!" -ForegroundColor Green
Write-Host "   หมายเหตุ: ค่าจะมีผลทันที (ไม่ต้อง restart เครื่อง)" -ForegroundColor DarkGray
Write-Host "   แต่ถ้าต้องการเปิดผล driver restart ด้วย Disable/Enable NIC" -ForegroundColor DarkGray
