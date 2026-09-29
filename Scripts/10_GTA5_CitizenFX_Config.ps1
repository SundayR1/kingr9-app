# ============================================================
#  R9 Settings - GTA5 In-Game Settings + CitizenFX.ini
#  แท็บ FiveM: In-Game Settings + CitizenFX.ini
#  แกะมาจาก R9 Settings.exe
# ============================================================
#  ⚠️ ต้องรัน PowerShell ด้วยสิทธิ์ Admin
#  ⚠️ ปิด FiveM/GTA5 ก่อนรัน (ไม่งั้นไฟล์จะถูกเขียนทับกลับ)
# ============================================================

Write-Host "=== GTA5 settings.xml + CitizenFX.ini ===" -ForegroundColor Cyan

# ╔═══════════════════════════════════════════════════════════╗
# ║  ส่วนที่ 1: gta5_settings.xml (ค่ากราฟิกในเกม GTA V)     ║
# ║  เขียนไป 2 ที่:                                          ║
# ║   1) %APPDATA%\CitizenFX\gta5_settings.xml (ที่ FiveM ใช้)║
# ║   2) Documents\Rockstar Games\GTA V\settings.xml (ถ้ามี) ║
# ║  ค่าหลัก: 1920x1080@144Hz, Windowed=1 (borderless),      ║
# ║  VSync=0, PauseOnFocusLoss=1, Audio3d=false,             ║
# ║  เงา/สะท้อน/พืชหญ้า/อนุภาค ต่ำสุด, Texture สูง (2),       ║
# ║  Anisotropic 16x, จำกัด replay memory                    ║
# ╚═══════════════════════════════════════════════════════════╝

$settingsXml = @'
<?xml version="1.0" encoding="UTF-8"?>

<Settings>
  <version value="27" />
  <configSource>SMC_AUTO</configSource>
  <graphics>
    <Tessellation value="0" />
    <LodScale value="0.000000" />
    <PedLodBias value="0.200000" />
    <VehicleLodBias value="0.000000" />
    <ShadowQuality value="0" />
    <ReflectionQuality value="0" />
    <ReflectionMSAA value="8" />
    <SSAO value="0" />
    <AnisotropicFiltering value="16" />
    <MSAA value="0" />
    <MSAAFragments value="0" />
    <MSAAQuality value="0" />
    <SamplingMode value="0" />
    <TextureQuality value="2" />
    <ParticleQuality value="0" />
    <WaterQuality value="0" />
    <GrassQuality value="0" />
    <ShaderQuality value="0" />
    <Shadow_SoftShadows value="1" />
    <UltraShadows_Enabled value="false" />
    <Shadow_ParticleShadows value="true" />
    <Shadow_Distance value="1.000000" />
    <Shadow_LongShadows value="false" />
    <Shadow_SplitZStart value="0.930000" />
    <Shadow_SplitZEnd value="0.890000" />
    <Shadow_aircraftExpWeight value="0.990000" />
    <Shadow_DisableScreenSizeCheck value="false" />
    <Reflection_MipBlur value="true" />
    <FXAA_Enabled value="false" />
    <TXAA_Enabled value="false" />
    <Lighting_FogVolumes value="true" />
    <Shader_SSA value="true" />
    <DX_Version value="2" />
    <CityDensity value="0.000000" />
    <PedVarietyMultiplier value="0.000000" />
    <VehicleVarietyMultiplier value="0.000000" />
    <MacadamEdit value="0" />
    <HalfResResolution value="0" />
    <StepItUp value="false" />
    <Tessellation_ti value="0" />
    <Tessellation_vl value="false" />
    <Tessellation_vh value="false" />
  </graphics>
  <system>
    <numBytesPerReplayBlock value="9000000" />
    <numReplayBlocks value="36" />
    <maxSizeOfUSC value="104857600" />
    <maxBlocksInReplay value="0" />
  </system>
  <audio>
    <Audio3d value="false" />
  </audio>
  <video>
    <AdapterIndex value="0" />
    <OutputIndex value="0" />
    <ScreenWidth value="1920" />
    <ScreenHeight value="1080" />
    <RefreshRate value="144" />
    <Windowed value="1" />
    <VSync value="0" />
    <Stereo value="0" />
    <Convergence value="0.100000" />
    <Separation value="0.000000" />
    <PauseOnFocusLoss value="1" />
    <AspectRatio value="0" />
  </video>
  <VideoCardDescription>__GPU__</VideoCardDescription>
</Settings>
'@

# ─── ตรวจจับชื่อการ์ดจอจริง (เลือก dGPU ที่แรงที่สุด) ───
$gpus = @(Get-CimInstance Win32_VideoController -ErrorAction SilentlyContinue)
# ตัดการ์ด virtual /  onboard ออก
$filtered = $gpus | Where-Object { $_.Name -notmatch "Microsoft Basic|Virtual|Remote|Citrix|VMware" }
$discrete = $filtered | Where-Object { ($_.Name -match "NVIDIA|GeForce|RTX|GTX|Quadro|Radeon RX|Radeon Pro|Radeon HD|Arc") -and ($_.Name -notmatch "AMD Radeon\(TM\) Graphics|Intel.*HD|Intel.*UHD|Intel.*Iris") }
if ($discrete) {
    $gpu = ($discrete | Sort-Object -Property AdapterRAM -Descending | Select-Object -First 1).Name
} elseif ($filtered) {
    $gpu = ($filtered | Sort-Object -Property AdapterRAM -Descending | Select-Object -First 1).Name
} else {
    $gpu = "NVIDIA GeForce RTX 4060"   # ค่า fallback ของแอป
}

$xml = $settingsXml.Replace("__GPU__", $gpu)

# เขียนไฟล์ (ปลดล็อก read-only ก่อนถ้ามี)
$tf = "$env:APPDATA\CitizenFX\gta5_settings.xml"
if (-not (Test-Path "$env:APPDATA\CitizenFX")) {
    New-Item -ItemType Directory -Path "$env:APPDATA\CitizenFX" -Force | Out-Null
}
if (Test-Path $tf) { Set-ItemProperty $tf -Name IsReadOnly -Value $false -ErrorAction SilentlyContinue }
[System.IO.File]::WriteAllText($tf, $xml, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "  ✓ เขียน $tf" -ForegroundColor Green

# เขียนซ้ำลง Documents ด้วย (ถ้ามีโฟลเดอร์ GTA V)
$gtaDocs = "$env:USERPROFILE\Documents\Rockstar Games\GTA V\settings.xml"
if (Test-Path (Split-Path $gtaDocs -Parent)) {
    [System.IO.File]::WriteAllText($gtaDocs, $xml, (New-Object System.Text.UTF8Encoding($false)))
    Write-Host "  ✓ เขียน $gtaDocs" -ForegroundColor Green
}

# ╔═══════════════════════════════════════════════════════════╗
# ║  ส่วนที่ 2: CitizenFX.ini (ค่า network/renderer ของ FiveM)║
# ║  net_mtu 1472              = packet ไม่ fragment          ║
# ║  cl_interp_ratio 1 / cl_interp 0 = interpolation ต่ำสุด  ║
# ║  net_thread_priority 1     = network thread ได้ priority  ║
# ║  voice_sample_rate 24000   = voice chat เบาเครื่อง        ║
# ║  cl_forceStreamingPrefetch 0 / r_textureStreaming 0      ║
# ║  DisableLauncher=true      = ไม่เปิด Rockstar launcher    ║
# ║  ForceRenderAheadLimit=1   = render queue 1 frame        ║
# ║    (ลด input delay เห็นๆ แต่ FPS อาจลดนิดหน่อย)           ║
# ║  SwapChainUseWaitableSwapChain=true = จังหวะ present แม่น║
# ║  MaxStreamingRequests/Memory = จำกัด streaming           ║
# ╚═══════════════════════════════════════════════════════════╝

$iniContent = @'
net_mtu 1472
cl_interp_ratio 1
cl_interp 0
net_thread_priority 1
voice_sample_rate 24000
cl_forceStreamingPrefetch 0
r_textureStreaming 0

[Game]
DisableLauncher=true

[Renderer]
DisableShadowOptimizations=false
EnablePresentationOptimizations=true
ForceRenderAheadLimit=1
DisableNvLowLatency=false
SwapChainUseWaitableSwapChain=true

[Streaming]
MaxStreamingRequests=50
MaxStreamingMemory=2000
StreamerMode=0

DisableNVSP=0
'@

# ─── หาตำแหน่ง CitizenFX.ini อัตโนมัติ (4 ชั้น) ───
# 1) ตำแหน่งมาตรฐาน LOCALAPPDATA 2 แบบ + APPDATA
# 2) ถ้าไม่เจอ ใช้ IVPath จาก registry (HKCU:\SOFTWARE\CitizenFX\FiveM)
# 3) ยังไม่เจอ ไล่ไดรฟ์ C-G ตามโฟลเดอร์ติดตั้งยอดฮิต
# 4) สุดท้าย fallback ไป LOCALAPPDATA
$tf = ""
$tfCandidates = @(
    "$env:LOCALAPPDATA\FiveM\FiveM.app\CitizenFX.ini",
    "$env:LOCALAPPDATA\FiveM Application Data\CitizenFX.ini",
    "$env:APPDATA\CitizenFX\CitizenFX.ini"
)
foreach ($c in $tfCandidates) { if (Test-Path $c) { $tf = $c; break } }
if (-not $tf) {
    try {
        $rk = Get-ItemProperty -Path "HKCU:\SOFTWARE\CitizenFX\FiveM" -ErrorAction SilentlyContinue
        if ($rk -and $rk.IVPath) {
            $ivIni = Join-Path (Split-Path (Split-Path $rk.IVPath -Parent) -Parent) "FiveM.app\CitizenFX.ini"
            if (Test-Path $ivIni) { $tf = $ivIni }
        }
    } catch {}
}
if (-not $tf) {
    foreach ($d in @("C","D","E","F","G")) {
        foreach ($subDir in @("FiveM\FiveM.app","Games\FiveM\FiveM.app","Program Files\FiveM\FiveM.app")) {
            $p = "${d}:\${subDir}\CitizenFX.ini"
            if (Test-Path $p) { $tf = $p; break }
        }
        if ($tf) { break }
    }
}
if (-not $tf) { $tf = "$env:LOCALAPPDATA\FiveM\FiveM.app\CitizenFX.ini" }

# ─── เขียน ini โดย "เก็บค่าสำคัญเดิม" ไม่ทับ ───
# IVPath (ตำแหน่ง GTA), ReplaceExecutable, SavedBuildNumber, UpdateChannel
$ivPath = ""; $replaceExec = ""; $savedBuild = ""; $updateChannel = ""
if (Test-Path $tf) {
    Set-ItemProperty $tf -Name IsReadOnly -Value $false -ErrorAction SilentlyContinue
    foreach ($line in (Get-Content $tf -ErrorAction SilentlyContinue)) {
        if ($line -match "^IVPath=")                { $ivPath = $line }
        if ($line -match "^ReplaceExecutable=")     { $replaceExec = $line }
        if ($line -match "^SavedBuildNumber=")      { $savedBuild = $line }
        if ($line -match "^UpdateChannel=")         { $updateChannel = $line }
    }
}
$tfDir = Split-Path $tf -Parent
if (-not (Test-Path $tfDir)) { New-Item -ItemType Directory -Path $tfDir -ErrorAction SilentlyContinue | Out-Null }

$final = $iniContent
if ($updateChannel -ne "") { $final = $updateChannel + "`r`n" + $final }
if ($savedBuild    -ne "") { $final = $savedBuild    + "`r`n" + $final }
if ($replaceExec   -ne "") { $final = $replaceExec   + "`r`n" + $final }
if ($ivPath        -ne "") { $final = $ivPath        + "`r`n" + $final }

[System.IO.File]::WriteAllText($tf, $final, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "  ✓ เขียน $tf (คงค่า IVPath/build เดิม)" -ForegroundColor Green

Write-Host "`n✅ GTA5 + CitizenFX config เสร็จสมบูรณ์!" -ForegroundColor Green
