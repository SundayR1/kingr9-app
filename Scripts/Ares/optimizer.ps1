$ErrorActionPreference='SilentlyContinue'
$ProgressPreference='SilentlyContinue'
try{ $OutputEncoding=[Text.Encoding]::UTF8 }catch{}
try{ [Console]::OutputEncoding=[Text.Encoding]::UTF8 }catch{}

# ---- helpers -------------------------------------------------------------
function Svc($n){ try{ Stop-Service -Name $n -Force -ErrorAction SilentlyContinue; Set-Service -Name $n -StartupType Disabled -ErrorAction Stop }catch{} }
function SvcReg($n){ Svc $n; reg add ("HKLM\SYSTEM\CurrentControlSet\Services\"+$n) /v Start /t REG_DWORD /d 4 /f *>$null }
function App($p){ Get-AppxPackage -AllUsers -Name $p -ErrorAction SilentlyContinue | Remove-AppxPackage -ErrorAction SilentlyContinue }
function Rg($path,$name,$type,$val){ reg add $path /v $name /t $type /d $val /f *>$null }
function RgBin($path,$name,$hex){ reg add $path /v $name /t REG_BINARY /d $hex /f *>$null }
function Tsk($tn){ schtasks /Change /TN $tn /DISABLE *>$null }
function ActiveAdapters{ Get-NetAdapter -ErrorAction SilentlyContinue | Where-Object Status -eq 'Up' | ForEach-Object { $_.Name } }
# Fire-and-forget: launch a slow external cleaner (cleanmgr, DISM) in the
# background so the optimizer never blocks on it — it finishes on its own.
function RunBG($file,$args){
  try{ Start-Process -FilePath $file -ArgumentList $args -WindowStyle Hidden -ErrorAction SilentlyContinue | Out-Null }catch{}
}
# bcdedit setter (boot/kernel level tweaks).
function BcdSet($kv){ try{ cmd /c ("bcdedit /set " + $kv) *>$null }catch{} }
# Idempotency: true when a registry value already equals the wanted data, so
# the main loop can SKIP steps that are already applied (faster re-runs).
function IsSet($path,$name,$want){
  try{
    $p = $path -replace '^HKLM\\','HKLM:\' -replace '^HKCU\\','HKCU:\'
    $v = (Get-ItemProperty -Path $p -Name $name -ErrorAction Stop).$name
    return ("$v" -eq "$want")
  }catch{ return $false }
}

# ---- OS / hardware detection (Win10 + Win11 aware) -----------------------
$script:Build = 0
try{ $script:Build = [int]((Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion' -ErrorAction Stop).CurrentBuildNumber) }catch{}
$script:Win11 = ($script:Build -ge 22000)
$script:RamMB = 8192
try{ $script:RamMB = [int]((Get-CimInstance Win32_ComputerSystem -ErrorAction Stop).TotalPhysicalMemory/1MB) }catch{}
$script:GpuName = ''
try{ $script:GpuName = ((Get-CimInstance Win32_VideoController -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Name) -join ' ') }catch{}
# UBR = the revision after the dot (26100.<UBR>); decides if the .8655 LCU is needed.
$script:Ubr = 0
try{ $script:Ubr = [int]((Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion' -Name UBR -ErrorAction Stop).UBR) }catch{}
$script:LcuStaged = $false

# ---- KB5094126 (June 2026 cumulative) pinned offline download ---------------
# Bumps the build to 26100.8655 (24H2) / 26200.8655 (25H2) so the ViVeTool
# feature below becomes supported. Works even when Windows Update is disabled
# (DISM installs the .msu directly). 24H2/25H2 LCUs are "checkpoint" packages, so
# the checkpoint/SSU (KB5043080) is installed FIRST, then the LCU (KB5094126).
# Both x64 builds share the same two .msu files. Links are CDN URLs tied to the
# file GUID; if a download ever fails, refresh them from catalog.update.microsoft.com.
$script:LcuUrl = 'https://catalog.sf.dl.delivery.mp.microsoft.com/filestreamingservice/files/8ab7b9b4-fb40-4820-afb3-cbaf4cc0a701/public/windows11.0-kb5094126-x64_1b7fae967f9781d27e8ee3f848fba51b7cd62e88.msu'
$script:ChkUrl = 'https://catalog.sf.dl.delivery.mp.microsoft.com/filestreamingservice/files/d8b7f92b-bd35-4b4c-96e5-46ce984b31e0/public/windows11.0-kb5043080-x64_953449672073f8fb99badb4cc6d5d7849b9c83e8.msu'
$script:Kb = @{
  26100 = @{ chk=$script:ChkUrl; chkSha=''; lcu=$script:LcuUrl; lcuSha='' }  # 24H2 x64 -> 26100.8655
  26200 = @{ chk=$script:ChkUrl; chkSha=''; lcu=$script:LcuUrl; lcuSha='' }  # 25H2 x64 -> 26200.8655
}

# Download a file with up to 3 attempts (BITS first, then Invoke-WebRequest);
# verifies SHA256 when provided and deletes a bad/partial file before retrying.
# Stream a URL to disk, emitting JX|{t:'dl',...} progress (bytes + MB/s) as it goes.
function DlStream($url,$dst,$head){
  $req = [System.Net.HttpWebRequest]::Create($url)
  $req.UserAgent = 'AresStore'
  $req.Timeout = 30000
  $req.ReadWriteTimeout = 30000
  $resp = $req.GetResponse()
  $total = [double]$resp.ContentLength
  $in = $resp.GetResponseStream()
  $out = [System.IO.File]::Create($dst)
  try{
    $buf = New-Object byte[] 1048576           # 1 MB chunks
    $done = [double]0
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $lastT = [double]0; $lastB = [double]0; $lastEmit = [double]-1
    while(($n = $in.Read($buf,0,$buf.Length)) -gt 0){
      $out.Write($buf,0,$n)
      $done += $n
      $now = $sw.Elapsed.TotalSeconds
      if(($now - $lastEmit) -ge 0.2){
        $dt = $now - $lastT
        $speed = if($dt -gt 0){ (($done - $lastB)/1048576.0)/$dt } else { 0 }
        $totMB = if($total -gt 0){ $total/1048576.0 } else { 0 }
        $pct   = if($total -gt 0){ [int](($done/$total)*100) } else { 0 }
        Emit @{ t='dl'; head=$head; pct=$pct; doneMB=[math]::Round($done/1048576.0,2); totalMB=[math]::Round($totMB,2); speed=[math]::Round($speed,1) }
        $lastT=$now; $lastB=$done; $lastEmit=$now
      }
    }
  } finally {
    $out.Close(); $in.Close(); $resp.Close()
  }
}

function DlFile($url,$dst,$sha,$head){
  for($i=0;$i -lt 3;$i++){
    try{ DlStream $url $dst $head }
    catch{
      try{ Start-BitsTransfer -Source $url -Destination $dst -ErrorAction Stop }
      catch{ try{ Invoke-WebRequest -Uri $url -OutFile $dst -UseBasicParsing -ErrorAction Stop }catch{} }
    }
    if(Test-Path $dst){
      if(-not $sha){ return $true }
      try{ if((Get-FileHash -Path $dst -Algorithm SHA256 -ErrorAction Stop).Hash -ieq $sha){ return $true } }catch{}
      try{ Remove-Item $dst -Force -ErrorAction SilentlyContinue }catch{}
    }
  }
  return $false
}

# ---- step groups (merged from JX TWEAK + JX OPTIMIZER, deduped) ----------
function StepsUI{ @(
 @{m='Hide Copilot button + Edge sidebar AI';a={ Rg 'HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' 'ShowCopilotButton' 'REG_DWORD' 0; Rg 'HKCU\Software\Policies\Microsoft\Edge' 'HubsSidebarEnabled' 'REG_DWORD' 0 }}
 @{m='Disable web/Bing suggestions in Start search';a={ Rg 'HKCU\Software\Policies\Microsoft\Windows\Explorer' 'DisableSearchBoxSuggestions' 'REG_DWORD' 1; Rg 'HKCU\Software\Microsoft\Windows\CurrentVersion\SearchSettings' 'IsDynamicSearchBoxEnabled' 'REG_DWORD' 0 }}
 @{m='Set visual effects to best performance';a={ Rg 'HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects' 'VisualFXSetting' 'REG_DWORD' 2 }}
 @{m='Disable transparency + window animations';a={ Rg 'HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize' 'EnableTransparency' 'REG_DWORD' 0; Rg 'HKCU\Control Panel\Desktop\WindowMetrics' 'MinAnimate' 'REG_SZ' 0 }}
 @{m='Instant menus (MenuShowDelay = 0)';a={ Rg 'HKCU\Control Panel\Desktop' 'MenuShowDelay' 'REG_SZ' 0 }}
 @{m='Enable Storage Sense';a={ Rg 'HKCU\Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy' '01' 'REG_DWORD' 1 }}
 @{m='Turn off ads, suggestions and tips';a={ $c='HKCU\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager'; foreach($v in 'SilentInstalledAppsEnabled','SystemPaneSuggestionsEnabled','SubscribedContent-338388Enabled','SubscribedContent-338389Enabled','SubscribedContent-310093Enabled'){ Rg $c $v 'REG_DWORD' 0 } }}
 @{m='Extra visual fx off (Aero peek, shadows, drag)';a={ Rg 'HKCU\Control Panel\Desktop' 'DragFullWindows' 'REG_SZ' 0; $A='HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced'; Rg $A 'ListviewAlphaSelect' 'REG_DWORD' 0; Rg $A 'ListviewShadow' 'REG_DWORD' 0; Rg $A 'TaskbarAnimations' 'REG_DWORD' 0; Rg 'HKCU\Software\Microsoft\Windows\DWM' 'EnableAeroPeek' 'REG_DWORD' 0; Rg 'HKCU\Software\Microsoft\Windows\DWM' 'AlwaysHibernateThumbnails' 'REG_DWORD' 0 }}
 @{m='Restore classic right-click menu';a={ reg add 'HKCU\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32' /f /ve *>$null }}
)}

function StepsAI{ @(
 @{m='Disable Copilot (policy) + remove app';a={ Rg 'HKCU\Software\Policies\Microsoft\Windows\WindowsCopilot' 'TurnOffWindowsCopilot' 'REG_DWORD' 1; App '*Copilot*' }}
 @{m='Disable Recall + Click to Do';a={ Rg 'HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsAI' 'DisableAIDataAnalysis' 'REG_DWORD' 1; Rg 'HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsAI' 'DisableClickToDo' 'REG_DWORD' 1 }}
 @{m='Trim Edge (background, startup boost, sidebar AI, quiet visuals)';a={ Rg 'HKLM\SOFTWARE\Policies\Microsoft\Edge' 'StartupBoostEnabled' 'REG_DWORD' 0; Rg 'HKLM\SOFTWARE\Policies\Microsoft\Edge' 'BackgroundModeEnabled' 'REG_DWORD' 0; Rg 'HKLM\SOFTWARE\Policies\Microsoft\Edge' 'HubsSidebarEnabled' 'REG_DWORD' 0; Rg 'HKLM\SOFTWARE\Policies\Microsoft\Edge' 'QuietVisuals' 'REG_DWORD' 1 }}
)}

function StepsBloat{ @(
 @{m='Remove bloatware apps';a={ foreach($p in '*Clipchamp*','*BingNews*','*BingWeather*','*GetHelp*','*Getstarted*','*SolitaireCollection*','*Microsoft.Todos*','*ZuneMusic*','*ZuneVideo*','*PowerAutomateDesktop*','*MicrosoftTeams*'){ App $p } }}
)}

function StepsPrivacy{ @(
 @{m='Disable telemetry';a={ Rg 'HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection' 'AllowTelemetry' 'REG_DWORD' 0; Svc 'DiagTrack'; Svc 'dmwappushservice' }}
 @{m='Privacy: ad ID, activity, location, feedback off';a={ Rg 'HKCU\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo' 'Enabled' 'REG_DWORD' 0; Rg 'HKLM\SOFTWARE\Policies\Microsoft\Windows\System' 'EnableActivityFeed' 'REG_DWORD' 0; Rg 'HKLM\SOFTWARE\Policies\Microsoft\Windows\System' 'PublishUserActivities' 'REG_DWORD' 0; Rg 'HKLM\SOFTWARE\Policies\Microsoft\Windows\System' 'UploadUserActivities' 'REG_DWORD' 0; Rg 'HKCU\Software\Microsoft\Windows\CurrentVersion\Privacy' 'TailoredExperiencesWithDiagnosticDataEnabled' 'REG_DWORD' 0; Rg 'HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location' 'Value' 'REG_SZ' 'Deny'; Rg 'HKLM\SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors' 'DisableLocation' 'REG_DWORD' 1; Rg 'HKCU\Software\Microsoft\Siuf\Rules' 'NumberOfSIUFInPeriod' 'REG_DWORD' 0 }}
 @{m='Disable telemetry scheduled tasks';a={ foreach($tn in '\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser','\Microsoft\Windows\Application Experience\ProgramDataUpdater','\Microsoft\Windows\Customer Experience Improvement Program\Consolidator','\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip','\Microsoft\Windows\Feedback\Siuf\DmClient','\Microsoft\Windows\Feedback\Siuf\DmClientOnScenarioDownload','\Microsoft\Windows\Windows Error Reporting\QueueReporting'){ Tsk $tn } }}
)}

function StepsServices{ @(
 @{m='Disable unneeded services';a={ Svc 'SysMain'; Svc 'Fax'; Svc 'MapsBroker'; Svc 'RemoteRegistry' }}
 @{m='Disable search indexing + Error Reporting svc';a={ Svc 'WSearch'; Svc 'WerSvc' }}
 @{m='Disable extra services (gaming desktop)';a={ foreach($n in 'Spooler','WbioSrvc','lfsvc','SCardSvr','PhoneSvc','TapiSrv','wisvc','PcaSvc','WpcMonSvc','AJRouter','SensorService','WalletService','WMPNetworkSvc'){ Svc $n } }}
 @{m='Disable Xbox services';a={ foreach($n in 'XboxGipSvc','XblAuthManager','XblGameSave','XboxNetApiSvc'){ Svc $n } }}
 @{m='Disable more services (unused features)';a={ foreach($n in 'icssvc','SharedAccess','lmhosts','CertPropSvc','ScDeviceEnum','AssignedAccessManagerSvc','RetailDemo','SEMgrSvc','TrkWks','CscService','DusmSvc'){ Svc $n } }}
 @{m='Disable Diagnostic services (no troubleshooters)';a={ foreach($n in 'DPS','WdiServiceHost','WdiSystemHost','diagsvc'){ Svc $n } }}
 @{m='Disable touch keyboard service';a={ foreach($n in 'TabletInputService'){ SvcReg $n } }}
)}

function StepsGame{ @(
 @{m='Disable Game DVR';a={ Rg 'HKCU\System\GameConfigStore' 'GameDVR_Enabled' 'REG_DWORD' 0; Rg 'HKLM\SOFTWARE\Policies\Microsoft\Windows\GameDVR' 'AllowGameDVR' 'REG_DWORD' 0 }}
 @{m='Enable GPU scheduling (HAGS) + Game Mode';a={ Rg 'HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers' 'HwSchMode' 'REG_DWORD' 2; Rg 'HKCU\Software\Microsoft\GameBar' 'AutoGameModeEnabled' 'REG_DWORD' 1 }}
 @{m='Fullscreen exclusive (FSE) + GameBar/DVR capture off';a={ $g='HKCU\System\GameConfigStore'; Rg $g 'GameDVR_FSEBehavior' 'REG_DWORD' 2; Rg $g 'GameDVR_FSEBehaviorMode' 'REG_DWORD' 2; Rg $g 'GameDVR_HonorUserFSEBehaviorMode' 'REG_DWORD' 1; Rg $g 'GameDVR_DXGIHonorFSEWindowsCompatible' 'REG_DWORD' 1; Rg 'HKCU\Software\Microsoft\Windows\CurrentVersion\GameDVR' 'AppCaptureEnabled' 'REG_DWORD' 0; Rg 'HKCU\Software\Microsoft\GameBar' 'ShowGameBar' 'REG_DWORD' 0 }}
 @{m='Game priority (MMCSS) + Power Throttling off';a={ $t='HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games'; Rg $t 'GPU Priority' 'REG_DWORD' 8; Rg $t 'Priority' 'REG_DWORD' 6; Rg $t 'Scheduling Category' 'REG_SZ' 'High'; Rg $t 'SFIO Priority' 'REG_SZ' 'High'; Rg 'HKLM\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling' 'PowerThrottlingOff' 'REG_DWORD' 1 }}
 @{m='CPU priority to foreground apps';a={ Rg 'HKLM\SYSTEM\CurrentControlSet\Control\PriorityControl' 'Win32PrioritySeparation' 'REG_DWORD' 38; Rg 'HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile' 'SystemResponsiveness' 'REG_DWORD' 0 }}
 @{m='FiveM/GTA5 process priority (IFEO High)';a={ $ife='HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options'; foreach($exe in 'FiveM.exe','GTA5.exe'){ Rg ($ife+'\'+$exe+'\PerfOptions') 'CpuPriorityClass' 'REG_DWORD' 3; Rg ($ife+'\'+$exe+'\PerfOptions') 'IoPriority' 'REG_DWORD' 3 } }}
 @{m='Force DISABLEDXMAXIMIZEDWINDOWEDMODE (FiveM/GTA5)';a={ $L='HKCU\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers'; foreach($exe in 'FiveM.exe','GTA5.exe','fivem_web_b2060.exe'){ Rg $L ('C:\Program Files\Rockstar Games\Grand Theft Auto V\'+$exe) 'REG_SZ' '~ DISABLEDXMAXIMIZEDWINDOWEDMODE' } }}
 @{m='FiveM: Defender folder exclusion + clear cache';a={ $fm="$env:LOCALAPPDATA\FiveM"; if(Test-Path $fm){ try{ Add-MpPreference -ExclusionPath $fm -ErrorAction SilentlyContinue }catch{}; foreach($cp in "$fm\FiveM.app\data\cache","$fm\FiveM.app\logs"){ if(Test-Path $cp){ Get-ChildItem -Path $cp -Recurse -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue } } } }}
)}

function StepsPower{ @(
 @{m='Remove old ARESSTORE power plans (use KingR9 plan)';a={
   # KingR9Tools has its own power plan (Gaming Hub) — do NOT create an Ares plan.
   # Just clean up plans left by the original Ares script; if one is active,
   # switch back to the R9 plan when present, otherwise Balanced.
   try{
     $active=(powercfg /getactivescheme)
     foreach($ln in (powercfg /list)){
       if($ln -match '([0-9a-fA-F-]{36})\s*\((.*)\)'){
         $oguid=$Matches[1]; $oname=$Matches[2]
         if($oname -match 'Ares Power Setting' -or $oname -match 'ARESSTORE'){
           if($active -match $oguid){
             $r9=(powercfg /list | Where-Object { $_ -match 'R9\(' } | Select-Object -First 1)
             if("$r9" -match '([0-9a-fA-F-]{36})'){ powercfg /setactive $Matches[1] *>$null }
             else{ powercfg /setactive 381b4222-f694-41f0-9685-ff5bb260df2e *>$null }   # Balanced fallback
           }
           powercfg /delete $oguid *>$null
         }
       }
     }
   }catch{}
 }}
 @{m='Delivery Optimization off + hibernate off';a={ Rg 'HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\DeliveryOptimization\Config' 'DODownloadMode' 'REG_DWORD' 0; powercfg -h off *>$null }}
 @{m='Disable USB selective suspend';a={ powercfg /setacvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0 *>$null; powercfg /setdcvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0 *>$null; powercfg /setactive SCHEME_CURRENT *>$null }}
)}

function StepsSecurity{ @(
 @{m='Disable Memory Integrity (HVCI)';a={ Rg 'HKLM\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity' 'Enabled' 'REG_DWORD' 0 }}
 @{m='Disable VBS (reboot required)';a={ Rg 'HKLM\SYSTEM\CurrentControlSet\Control\DeviceGuard' 'EnableVirtualizationBasedSecurity' 'REG_DWORD' 0; cmd /c 'bcdedit /set hypervisorlaunchtype off' *>$null }}
 @{m='Disable Reserved Storage';a={ try{ Set-WindowsReservedStorageState -State Disabled -ErrorAction Stop }catch{ cmd /c 'dism /Online /Set-ReservedStorageState /State:Disabled' *>$null } }}
)}

function StepsMemory{ @(
 @{m='Keep kernel in RAM (DisablePagingExecutive)';a={ Rg 'HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management' 'DisablePagingExecutive' 'REG_DWORD' 1 }}
 @{m='Memory: large system cache off + IO page lock';a={ $M='HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management'; Rg $M 'LargeSystemCache' 'REG_DWORD' 0; Rg $M 'IoPageLockLimit' 'REG_DWORD' 0 }}
 @{m='Prefetch tuning (prefetcher on, superfetch off)';a={ $p='HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters'; Rg $p 'EnablePrefetcher' 'REG_DWORD' 2; Rg $p 'EnableSuperfetch' 'REG_DWORD' 0; Rg $p 'EnableBootTrace' 'REG_DWORD' 0 }}
)}

function StepsTimer{ @(
 @{m='Timer resolution + disable HPET / platform clock';a={ Rg 'HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\kernel' 'GlobalTimerResolutionRequests' 'REG_DWORD' 1; cmd /c 'bcdedit /deletevalue useplatformclock' *>$null; $h=Get-ChildItem 'HKLM:\SYSTEM\CurrentControlSet\Enum\ACPI\PNP0103' -ErrorAction SilentlyContinue | Select-Object -First 1; if($h){ $hr=($h.Name -replace 'HKEY_LOCAL_MACHINE','HKLM')+'\Device Parameters'; Rg $hr 'DisableHPET' 'REG_DWORD' 1 } }}
)}

function StepsInput{ @(
 @{m='Disable mouse acceleration (raw aim)';a={ Rg 'HKCU\Control Panel\Mouse' 'MouseSpeed' 'REG_SZ' 0; Rg 'HKCU\Control Panel\Mouse' 'MouseThreshold1' 'REG_SZ' 0; Rg 'HKCU\Control Panel\Mouse' 'MouseThreshold2' 'REG_SZ' 0 }}
 @{m='MarkC 1:1 mouse fix (no acceleration curve)';a={ $M='HKCU\Control Panel\Mouse'; RgBin $M 'SmoothMouseXCurve' '000000000000000000A0000000000000004001000000000000800200000000000000050000000000'; RgBin $M 'SmoothMouseYCurve' ('00'*40) }}
 @{m='Fastest keyboard repeat';a={ Rg 'HKCU\Control Panel\Keyboard' 'KeyboardDelay' 'REG_SZ' 0; Rg 'HKCU\Control Panel\Keyboard' 'KeyboardSpeed' 'REG_SZ' 31 }}
 @{m='Disable Sticky / Filter keys';a={ Rg 'HKCU\Control Panel\Accessibility\StickyKeys' 'Flags' 'REG_SZ' 506; Rg 'HKCU\Control Panel\Accessibility\Keyboard Response' 'Flags' 'REG_SZ' 122; Rg 'HKCU\Control Panel\Accessibility\ToggleKeys' 'Flags' 'REG_SZ' 58 }}
 @{m='Input latency: keyboard queue + slate mode + IRQ8';a={ $kb='HKLM\SYSTEM\CurrentControlSet\Services\kbdclass\Parameters'; Rg $kb 'ConnectMultiplePorts' 'REG_DWORD' 0; Rg $kb 'KeyboardDataQueueSize' 'REG_DWORD' 20; Rg $kb 'KeyboardDeviceBaseName' 'REG_SZ' 'KeyboardClass'; Rg $kb 'MaximumPortsServiced' 'REG_DWORD' 3; Rg $kb 'SendOutputToAllPorts' 'REG_DWORD' 1; Rg 'HKLM\SYSTEM\CurrentControlSet\Control\PriorityControl' 'ConvertibleSlateMode' 'REG_DWORD' 0; Rg 'HKLM\SYSTEM\CurrentControlSet\Control\PriorityControl' 'IRQ8Priority' 'REG_DWORD' 1 }}
)}

function StepsNetwork{ @(
 @{m='Tune network (Cloudflare DNS, throttle off)';a={ try{ Get-NetAdapter -Physical -ErrorAction Stop | Where-Object Status -eq 'Up' | Set-DnsClientServerAddress -ServerAddresses 1.1.1.1,1.0.0.1 -ErrorAction Stop }catch{}; Rg 'HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile' 'NetworkThrottlingIndex' 'REG_DWORD' 4294967295 }}
 @{m='Disable Nagle + zero startup delay';a={ try{ Get-ChildItem 'HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces' -ErrorAction Stop | ForEach-Object { New-ItemProperty -Path $_.PSPath -Name 'TcpAckFrequency' -PropertyType DWord -Value 1 -Force | Out-Null; New-ItemProperty -Path $_.PSPath -Name 'TCPNoDelay' -PropertyType DWord -Value 1 -Force | Out-Null } }catch{}; Rg 'HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize' 'StartupDelayInMSec' 'REG_DWORD' 0 }}
 @{m='Network: DNS cache TTL + QoS reserve + TcpDelAck';a={ $D='HKLM\SYSTEM\CurrentControlSet\Services\Dnscache\Parameters'; Rg $D 'MaxCacheTtl' 'REG_DWORD' 3600; Rg $D 'MaxNegativeCacheTtl' 'REG_DWORD' 0; Rg 'HKLM\SOFTWARE\Policies\Microsoft\Windows\Psched' 'NonBestEffortLimit' 'REG_DWORD' 0; try{ Get-ChildItem 'HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces' -ErrorAction Stop | ForEach-Object { New-ItemProperty -Path $_.PSPath -Name 'TcpDelAckTicks' -PropertyType DWord -Value 0 -Force | Out-Null } }catch{} }}
 @{m='TCP autotuning = normal';a={ netsh int tcp set global autotuninglevel=normal *>$null }}
 @{m='Enable RSS + disable interrupt moderation';a={ netsh int tcp set global rss=enabled *>$null; foreach($ad in (ActiveAdapters)){ Set-NetAdapterAdvancedProperty -Name $ad -DisplayName 'Interrupt Moderation' -DisplayValue 'Disabled' -NoRestart -ErrorAction SilentlyContinue } }}
 @{m='Disable LSO + flow control';a={ Disable-NetAdapterLso -Name * -ErrorAction SilentlyContinue; foreach($ad in (ActiveAdapters)){ Set-NetAdapterAdvancedProperty -Name $ad -DisplayName 'Flow Control' -DisplayValue 'Disabled' -NoRestart -ErrorAction SilentlyContinue } }}
 @{m='Tune MTU (probe, reduce fragmentation)';a={ $lo=1200;$hi=1472;$best=$null; while($lo -le $hi){ $mid=[int](($lo+$hi)/2); $r=ping 1.1.1.1 -n 1 -f -l $mid -w 1200; if($LASTEXITCODE -eq 0 -and ($r -join ' ') -notmatch 'fragment' -and ($r -join ' ') -match 'TTL='){ $best=$mid; $lo=$mid+1 } else { $hi=$mid-1 } }; if($best){ $mtu=$best+28; foreach($ad in (ActiveAdapters)){ netsh interface ipv4 set subinterface "$ad" mtu=$mtu store=persistent *>$null } } }}
 @{m='NIC interrupts: MSI mode + interrupt affinity';a={ $devs = Get-WmiObject Win32_PnPEntity -ErrorAction SilentlyContinue | Where-Object { $_.Name -match 'Ethernet|Wi-Fi|Wireless|Network' -and $_.DeviceID -match 'PCI' } | Select-Object -First 3; foreach($dev in $devs){ $b='HKLM\SYSTEM\CurrentControlSet\Enum\'+$dev.DeviceID+'\Device Parameters\Interrupt Management'; Rg ($b+'\MessageSignaledInterruptProperties') 'MSISupported' 'REG_DWORD' 1; Rg ($b+'\Affinity Policy') 'DevicePolicy' 'REG_DWORD' 4; Rg ($b+'\Affinity Policy') 'DevicePriority' 'REG_DWORD' 3 } }}
)}

function StepsGPU{ @(
 @{m='GPU TDR delay (prevent driver resets)';a={ Rg 'HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers' 'TdrDelay' 'REG_DWORD' 10; Rg 'HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers' 'TdrDdiDelay' 'REG_DWORD' 20 }}
 @{m='GPU driver tweaks (NVIDIA PowerMizer/ULL · AMD)';a={ $gpu=(Get-CimInstance Win32_VideoController -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Name) -join ' '; if($gpu -match 'NVIDIA'){ $nv='HKLM\SYSTEM\CurrentControlSet\Services\nvlddmkm\Global\NVTweak'; Rg $nv 'PowerMizerEnable' 'REG_DWORD' 1; Rg $nv 'PowerMizerLevel' 'REG_DWORD' 1; Rg $nv 'PowerMizerLevelAC' 'REG_DWORD' 1; Rg $nv 'UllModeEnabled' 'REG_DWORD' 1; Rg $nv 'UllModeLevel' 'REG_DWORD' 2; Rg 'HKLM\SOFTWARE\NVIDIA Corporation\Global\PowerProfiles\PowerProfile_0' 'PowerProfile' 'REG_DWORD' 1; $ng='HKLM\SOFTWARE\NVIDIA Corporation\Global\NVTweak'; Rg $ng 'ShaderCacheSize' 'REG_DWORD' 4294967295; Rg $ng 'MaxFramesAllowed' 'REG_DWORD' 1 } elseif($gpu -match 'AMD|Radeon'){ Rg 'HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000' 'KMD_EnableComputePreemption' 'REG_DWORD' 0 } }}
)}

function StepsBranding{ @(
 @{m='[SKIP] Rename PC (KingR9Tools — ไม่เปลี่ยนชื่อเครื่อง)';d={ $true };a={}}
 @{m='[SKIP] Set display name (KingR9Tools — ไม่เปลี่ยนชื่อ account)';d={ $true };a={}}
 @{m='Set account picture to the Ares wallpaper';a={
   $wp=$env:ARES_WALLPAPER
   if($wp -and (Test-Path $wp)){
     try{
       $sid=(New-Object System.Security.Principal.NTAccount($env:USERNAME)).Translate([System.Security.Principal.SecurityIdentifier]).Value
       $dir="$env:PUBLIC\AccountPictures\$sid"
       New-Item -ItemType Directory -Path $dir -Force -ErrorAction SilentlyContinue | Out-Null
       Add-Type -AssemblyName System.Drawing
       $src=[System.Drawing.Image]::FromFile($wp)
       $key='HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\AccountPicture\Users\'+$sid
       foreach($sz in 32,40,48,64,96,192,208,240,424,448,1080){
         $out="$dir\Image$sz.jpg"
         $bmp=New-Object System.Drawing.Bitmap $sz,$sz
         $g=[System.Drawing.Graphics]::FromImage($bmp)
         $g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
         $g.DrawImage($src,0,0,$sz,$sz)
         $g.Dispose()
         $bmp.Save($out,[System.Drawing.Imaging.ImageFormat]::Jpeg)
         $bmp.Dispose()
         Rg $key ('Image'+$sz) 'REG_SZ' $out
       }
       $src.Dispose()
     }catch{}
   }
 }}
 @{m='[SKIP] OEM info (KingR9Tools — ไม่ตั้งค่า Ares Store OEM)';d={ $true };a={}}
 @{m='Set desktop wallpaper (Ares Store)';a={
   $wp=$env:ARES_WALLPAPER
   if($wp -and (Test-Path $wp)){
     $dest="$env:WINDIR\Web\Wallpaper\ares_store_wallpaper.png"
     try{ Copy-Item $wp $dest -Force -ErrorAction Stop }catch{ $dest=$wp }
     Rg 'HKCU\Control Panel\Desktop' 'Wallpaper' 'REG_SZ' $dest
     Rg 'HKCU\Control Panel\Desktop' 'WallpaperStyle' 'REG_SZ' 10
     Rg 'HKCU\Control Panel\Desktop' 'TileWallpaper' 'REG_SZ' 0
     try{
       Add-Type -TypeDefinition 'using System;using System.Runtime.InteropServices;public class AresWP{[DllImport("user32.dll",CharSet=CharSet.Auto)]public static extern int SystemParametersInfo(int a,int u,string p,int f);}' -ErrorAction SilentlyContinue
       [AresWP]::SystemParametersInfo(20,0,$dest,3) | Out-Null
     }catch{}
   }
 }}
)}

function Wipe($path){ if(Test-Path $path){ Get-ChildItem -Path $path -Recurse -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue } }

function StepsClean{ @(
 @{m='Empty Recycle Bin (all drives)';a={ try{ Clear-RecycleBin -Force -ErrorAction SilentlyContinue }catch{} }}
 @{m='Delete temp files (user + Windows + prefetch)';a={ Wipe $env:TEMP; Wipe "$env:LOCALAPPDATA\Temp"; Wipe 'C:\Windows\Temp'; Wipe 'C:\Windows\Prefetch'; Wipe 'C:\Windows\SoftwareDistribution\DataStore\Logs' }}
 @{m='Clear Windows Update cache';a={ Stop-Service wuauserv -Force -ErrorAction SilentlyContinue; Stop-Service bits -Force -ErrorAction SilentlyContinue; Wipe 'C:\Windows\SoftwareDistribution\Download'; Start-Service bits -ErrorAction SilentlyContinue; Start-Service wuauserv -ErrorAction SilentlyContinue }}
 @{m='Clear Delivery Optimization + crash dumps + WER';a={ try{ Delete-DeliveryOptimizationCache -Force -ErrorAction SilentlyContinue }catch{}; Wipe "$env:LOCALAPPDATA\CrashDumps"; Wipe "$env:ProgramData\Microsoft\Windows\WER"; Wipe 'C:\Windows\LiveKernelReports'; Wipe 'C:\Windows\Minidump' }}
 @{m='Clear thumbnail + icon + font + INetCache';a={ Wipe "$env:LOCALAPPDATA\Microsoft\Windows\Explorer"; Wipe "$env:LOCALAPPDATA\Microsoft\Windows\INetCache"; Wipe "$env:LOCALAPPDATA\Microsoft\Windows\WebCache"; Wipe "$env:LOCALAPPDATA\Microsoft\Windows\Caches"; Wipe "$env:WINDIR\ServiceProfiles\LocalService\AppData\Local\FontCache" }}
 @{m='Flush DNS + clear all event logs';a={ ipconfig /flushdns *>$null; wevtutil el | ForEach-Object { wevtutil cl "$_" *>$null } }}
 @{m='Deep clean: DISM component store (/ResetBase, background)';a={ RunBG 'Dism.exe' '/Online /Cleanup-Image /StartComponentCleanup /ResetBase /Quiet' }}
 @{m='Deep clean: Disk Cleanup on C: (background)';a={ RunBG 'cleanmgr.exe' '/verylowdisk /d C:' }}
)}

# ==========================================================================
#  EXTRA PERFORMANCE GROUPS (added set — Win10 + Win11)
# ==========================================================================

function StepsBoot{ @(
 @{m='Boot/kernel: dynamic tick = no (default, prevents stutter)';a={ BcdSet 'disabledynamictick no' }}
 @{m='Boot/kernel: TSC sync policy = Enhanced';a={ BcdSet 'tscsyncpolicy Enhanced' }}
 @{m='Boot/kernel: x2APIC on, legacy APIC off';a={ BcdSet 'x2apicpolicy enable'; BcdSet 'uselegacyapicmode no' }}
 @{m='Boot/kernel: config access policy Default';a={ BcdSet 'configaccesspolicy Default' }}
 @{m='Boot: quiet boot, standard menu, no recovery, fast timeout';a={ BcdSet 'quietboot yes'; BcdSet 'bootmenupolicy Standard'; BcdSet 'recoveryenabled No'; BcdSet 'tpmbootentropy ForceDisable'; cmd /c 'bcdedit /timeout 0' *>$null }}
)}

function StepsCPU2{ @(
 @{m='Timer: DistributeTimers + serialize expiration + thread DPC';a={ $k='HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\kernel'; Rg $k 'DistributeTimers' 'REG_DWORD' 1; Rg $k 'SerializeTimerExpiration' 'REG_DWORD' 1; Rg $k 'ThreadDpcEnable' 'REG_DWORD' 1; Rg $k 'DpcWatchdogProfileOffset' 'REG_DWORD' 10000; Rg $k 'InterruptSteeringFlags' 'REG_DWORD' 1 }}
 @{m='CPU power: EPP=0, boost aggressive, fast ramp, latency hint, no throttle';a={
   $g='SCHEME_CURRENT'; $sub='54533251-82be-4824-96c1-47b60b740d00'
   $pairs=@(
     @('36687f9e-571e-4153-8c54-9e8a59c1c5f1',0),   # EPP
     @('45bcc044-d885-43e2-8605-ee0ec6e96b59',0),   # EPP (perf %)
     @('be337238-0d82-4146-a960-4f3749d470c7',2),   # boost mode aggressive
     @('06cadf0e-64ed-448a-8927-ce7bf90eb35d',10),  # perf increase threshold
     @('12a0ab44-fe28-4fa9-b3bd-4b64f44960a6',100), # perf decrease threshold
     @('619b7505-003b-4e82-b7a6-4dd29c300971',99),  # latency sensitivity hint
     @('3b04d4fd-1cc7-4f23-ab1c-d1337819c4bb',0)    # allow throttle states off
   )
   foreach($p in $pairs){ powercfg -setacvalueindex $g $sub $p[0] $p[1] *>$null; powercfg -setdcvalueindex $g $sub $p[0] $p[1] *>$null }
   powercfg -setactive $g *>$null
 }}
 @{m='Scheduler: foreground boost (Win32PrioritySeparation)';d={ IsSet 'HKLM\SYSTEM\CurrentControlSet\Control\PriorityControl' 'Win32PrioritySeparation' 38 };a={ Rg 'HKLM\SYSTEM\CurrentControlSet\Control\PriorityControl' 'Win32PrioritySeparation' 'REG_DWORD' 38 }}
)}

function StepsMem2{ @(
 @{m='Memory: ClearPageFileAtShutdown off, pool max, paged pool unlimited';a={ $M='HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management'; Rg $M 'ClearPageFileAtShutdown' 'REG_DWORD' 0; Rg $M 'PoolUsageMaximum' 'REG_DWORD' 96; Rg $M 'PagedPoolSize' 'REG_DWORD' 4294967295 }}
 @{m='Memory: combine svchost (SvcHostSplitThresholdInKB = RAM)';a={ Rg 'HKLM\SYSTEM\CurrentControlSet\Control' 'SvcHostSplitThresholdInKB' 'REG_DWORD' ([int]($script:RamMB*1024)) }}
 @{m='Memory: fixed pagefile (no auto-managed, = RAM size)';a={
   try{
     $cs=Get-CimInstance Win32_ComputerSystem -ErrorAction Stop
     if($cs.AutomaticManagedPagefile){ $cs | Set-CimInstance -Property @{AutomaticManagedPagefile=$false} -ErrorAction SilentlyContinue }
     $sz=$script:RamMB
     $pf=Get-CimInstance Win32_PageFileSetting -ErrorAction SilentlyContinue
     if($pf){ $pf | Set-CimInstance -Property @{InitialSize=$sz;MaximumSize=$sz} -ErrorAction SilentlyContinue }
     else{ Set-WmiInstance -Class Win32_PageFileSetting -Arguments @{Name='C:\pagefile.sys';InitialSize=$sz;MaximumSize=$sz} -ErrorAction SilentlyContinue | Out-Null }
   }catch{}
 }}
)}

function StepsFS{ @(
 @{m='Filesystem: disable last-access + 8.3 names, NTFS tuning';a={ cmd /c 'fsutil behavior set disablelastaccess 1' *>$null; cmd /c 'fsutil behavior set disable8dot3 1' *>$null; $F='HKLM\SYSTEM\CurrentControlSet\Control\FileSystem'; Rg $F 'NtfsDisableLastAccessUpdate' 'REG_DWORD' 1; Rg $F 'NtfsMemoryUsage' 'REG_DWORD' 2; Rg $F 'NtfsDisable8dot3NameCreation' 'REG_DWORD' 1 }}
 @{m='SSD: enable TRIM + disable scheduled defrag';a={ cmd /c 'fsutil behavior set disabledeletenotify 0' *>$null; schtasks /Change /TN '\Microsoft\Windows\Defrag\ScheduledDefrag' /DISABLE *>$null }}
)}

function StepsGPU2{ @(
 @{m='GPU: AMD ULPS off (keep clocks up)';a={ foreach($i in '0000','0001','0002','0003'){ $c='HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\'+$i; Rg $c 'EnableULPS' 'REG_DWORD' 0 } }}
 @{m='GPU: MSI mode + high interrupt priority';a={ $devs=Get-WmiObject Win32_PnPEntity -ErrorAction SilentlyContinue | Where-Object { $_.Name -match 'NVIDIA|Radeon|AMD|Intel.*Graphics|GeForce' -and $_.DeviceID -match 'PCI' } | Select-Object -First 2; foreach($dev in $devs){ $b='HKLM\SYSTEM\CurrentControlSet\Enum\'+$dev.DeviceID+'\Device Parameters\Interrupt Management'; Rg ($b+'\MessageSignaledInterruptProperties') 'MSISupported' 'REG_DWORD' 1; Rg ($b+'\Affinity Policy') 'DevicePriority' 'REG_DWORD' 3 } }}
 @{m='GPU: unlimited shader cache + NVIDIA DPC spread + telemetry off';a={ if($script:GpuName -match 'NVIDIA|GeForce'){ $ng='HKLM\SOFTWARE\NVIDIA Corporation\Global\NVTweak'; Rg $ng 'ShaderCacheSize' 'REG_DWORD' 4294967295; $nv='HKLM\SYSTEM\CurrentControlSet\Services\nvlddmkm\Global\NVTweak'; Rg $nv 'RmGpsPsEnablePerCpuCoreDpc' 'REG_DWORD' 1; Svc 'NvTelemetryContainer'; Rg 'HKLM\SOFTWARE\NVIDIA Corporation\NvControlPanel2\Client' 'OptInOrOutPreference' 'REG_DWORD' 0 } Rg 'HKCU\Software\Microsoft\DirectX\UserGpuPreferences' 'DirectXUserGlobalSettings' 'REG_SZ' 'VRROptimizeEnable=1;' }}
 @{m='DWM: reduce desktop input lag';a={ $d='HKCU\Software\Microsoft\Windows\DWM'; Rg $d 'MaxQueuedBuffers' 'REG_DWORD' 1; Rg $d 'DwmInputUsesIoCompletionPort' 'REG_DWORD' 1 }}
)}

function StepsNet2{ @(
 @{m='TCP global: disable ECN/timestamps/RSC/chimney, heuristics off, CTCP, ICW=10';a={ foreach($c in 'set global ecncapability=disabled','set global timestamps=disabled','set global rsc=disabled','set global nonsackrttresiliency=disabled','set global initialRto=2000','set global maxsynretransmissions=2','set global chimney=disabled','set heuristics disabled','set supplemental internet congestionprovider=ctcp','set supplemental template=internet icw=10'){ cmd /c ("netsh int tcp "+$c) *>$null } }}
 @{m='TCP params: TTL, window scaling, port range, time-wait, SACK';a={ $T='HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters'; Rg $T 'DefaultTTL' 'REG_DWORD' 64; Rg $T 'Tcp1323Opts' 'REG_DWORD' 1; Rg $T 'EnablePMTUDiscovery' 'REG_DWORD' 1; Rg $T 'MaxUserPort' 'REG_DWORD' 65534; Rg $T 'TcpTimedWaitDelay' 'REG_DWORD' 30; Rg $T 'SackOpts' 'REG_DWORD' 1; Rg $T 'TcpMaxDataRetransmissions' 'REG_DWORD' 3 }}
 @{m='LAN: NetBIOS-over-TCP off + bigger IRP stack';a={ Get-ChildItem 'HKLM:\SYSTEM\CurrentControlSet\Services\NetBT\Parameters\Interfaces' -ErrorAction SilentlyContinue | ForEach-Object { New-ItemProperty -Path $_.PSPath -Name 'NetbiosOptions' -PropertyType DWord -Value 2 -Force -ErrorAction SilentlyContinue | Out-Null }; Rg 'HKLM\SYSTEM\CurrentControlSet\Services\LanmanServer\Parameters' 'IRPStackSize' 'REG_DWORD' 32 }}
 @{m='NIC: never power down adapter + keep task offload';a={ foreach($i in '0000','0001','0002','0003','0004'){ $c='HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}\'+$i; if(Test-Path ($c -replace '^HKLM\\','HKLM:\')){ Rg $c 'PnPCapabilities' 'REG_DWORD' 24 } }; Rg 'HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters' 'DisableTaskOffload' 'REG_DWORD' 0 }}
)}

function StepsNet3{ @(
 @{m='TCP Fast Open: cut handshake round-trips';a={ cmd /c 'netsh int tcp set global fastopen=enabled' *>$null; cmd /c 'netsh int tcp set global fastopenfallback=enabled' *>$null }}
 @{m='Disable IPv6 tunneling: Teredo / ISATAP / 6to4';a={ foreach($t in 'teredo','isatap','6to4'){ cmd /c ("netsh int "+$t+" set state disabled") *>$null } }}
 @{m='DNS: disable LLMNR / mDNS multicast name resolution';d={ IsSet 'HKLM\SOFTWARE\Policies\Microsoft\Windows NT\DNSClient' 'EnableMulticast' 0 };a={ $D='HKLM\SOFTWARE\Policies\Microsoft\Windows NT\DNSClient'; Rg $D 'EnableMulticast' 'REG_DWORD' 0; Rg $D 'DisableSmartNameResolution' 'REG_DWORD' 1 }}
 @{m='DNS cache: larger table + longer TTL retention';a={ $P='HKLM\SYSTEM\CurrentControlSet\Services\Dnscache\Parameters'; Rg $P 'CacheHashTableBucketSize' 'REG_DWORD' 1; Rg $P 'CacheHashTableSize' 'REG_DWORD' 384; Rg $P 'MaxCacheEntryTtlLimit' 'REG_DWORD' 86400; Rg $P 'MaxSOACacheEntryTtlLimit' 'REG_DWORD' 300 }}
 @{m='TCP: disable ICMP redirect + dead-gateway + PMTU black-hole detect';a={ $T='HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters'; Rg $T 'EnableICMPRedirect' 'REG_DWORD' 0; Rg $T 'DeadGWDetectDefault' 'REG_DWORD' 0; Rg $T 'EnablePMTUBHDetect' 'REG_DWORD' 0 }}
 @{m='NIC: disable Energy Efficient Ethernet / Green Ethernet / power saving';a={ foreach($n in (ActiveAdapters)){ try{ Set-NetAdapterPowerManagement -Name $n -AllowComputerToTurnOffDevice Disabled -ErrorAction SilentlyContinue }catch{}; foreach($k in 'Energy Efficient Ethernet','Green Ethernet','Power Saving Mode','Ultra Low Power Mode','Gigabit Lite','Advanced EEE','EEE'){ try{ Set-NetAdapterAdvancedProperty -Name $n -DisplayName $k -DisplayValue 'Disabled' -ErrorAction SilentlyContinue }catch{} } } }}
 @{m='NIC: maximize Receive/Transmit buffers + enable RSS';a={ foreach($n in (ActiveAdapters)){ try{ Enable-NetAdapterRss -Name $n -ErrorAction SilentlyContinue }catch{}; foreach($pair in @(@('Receive Buffers','2048'),@('Transmit Buffers','2048'),@('Receive Side Scaling','Enabled'))){ try{ Set-NetAdapterAdvancedProperty -Name $n -DisplayName $pair[0] -DisplayValue $pair[1] -ErrorAction SilentlyContinue }catch{} } } }}
 @{m='Winsock/AFD: bigger socket send/receive windows';a={ $A='HKLM\SYSTEM\CurrentControlSet\Services\AFD\Parameters'; Rg $A 'DefaultReceiveWindow' 'REG_DWORD' 262144; Rg $A 'DefaultSendWindow' 'REG_DWORD' 262144; Rg $A 'FastSendDatagramThreshold' 'REG_DWORD' 1500; Rg $A 'DynamicSendBufferDisable' 'REG_DWORD' 0 }}
 @{m='TCP KeepAlive: faster dead-connection detection';a={ $T='HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters'; Rg $T 'KeepAliveTime' 'REG_DWORD' 300000; Rg $T 'KeepAliveInterval' 'REG_DWORD' 1000 }}
)}

# Write a per-application QoS policy that tags the exe's packets with DSCP 46
# (Expedited Forwarding) so routers/NICs prioritize them; no throttling.
function Qos($name,$exe){ $p='HKLM\SOFTWARE\Policies\Microsoft\Windows\QoS\'+$name; Rg $p 'Version' 'REG_SZ' '1.0'; Rg $p 'Application Name' 'REG_SZ' $exe; Rg $p 'Protocol' 'REG_SZ' '*'; Rg $p 'Local Port' 'REG_SZ' '*'; Rg $p 'Local IP' 'REG_SZ' '*'; Rg $p 'Local IP Prefix Length' 'REG_SZ' '*'; Rg $p 'Remote Port' 'REG_SZ' '*'; Rg $p 'Remote IP' 'REG_SZ' '*'; Rg $p 'Remote IP Prefix Length' 'REG_SZ' '*'; Rg $p 'DSCP Value' 'REG_SZ' '46'; Rg $p 'Throttle Rate' 'REG_SZ' '-1' }

function StepsQoS{ @(
 @{m='QoS: prioritize game traffic (DSCP 46, per game exe)';a={
   Rg 'HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\QoS' 'Do not use NLA' 'REG_SZ' '1'
   $games=@{ 'Valorant'='valorant.exe';'ValorantShip'='VALORANT-Win64-Shipping.exe';'CSGO'='csgo.exe';'CS2'='cs2.exe';'Fortnite'='FortniteClient-Win64-Shipping.exe';'ApexLegends'='r5apex.exe';'Overwatch'='Overwatch.exe';'RocketLeague'='RocketLeague.exe';'CallOfDuty'='cod.exe';'ModernWarfare'='ModernWarfare.exe';'LeagueOfLegends'='League of Legends.exe';'Dota2'='dota2.exe';'PUBG'='TslGame.exe';'RainbowSix'='RainbowSix.exe';'RainbowSixGame'='RainbowSixGame.exe';'FiveM'='FiveM_GTAProcess.exe';'GTA5'='GTA5.exe' }
   foreach($k in $games.Keys){ Qos $k $games[$k] }
 }}
)}

# Force the build up to .8655 by installing KB5094126 offline (DISM), so the
# ViVeTool feature below is supported even on machines with Windows Update off.
# Only runs on a 24H2/25H2 base (26100/26200) whose UBR is still below 8655 and
# when a download URL is configured; reboots once at the very end.
function StepsUpdate{ @(
 @{m='Windows: install KB5094126 to reach build .8655 (offline LCU)';
   d={
     if($env:ARES_DO_UPDATE -ne '1'){ return $true }       # toggle off in UI
     $b=$script:Build
     if($b -ne 26100 -and $b -ne 26200){ return $true }   # not a 24H2/25H2 base
     if($script:Ubr -ge 8655){ return $true }              # already at/above .8655
     $k=$script:Kb[$b]
     if(-not $k -or [string]::IsNullOrEmpty($k.lcu)){ return $true }  # not configured
     return $false
   };
   a={
     $b=$script:Build; $k=$script:Kb[$b]
     $chk=Join-Path $env:TEMP ('KB5043080_'+$b+'.msu')
     $lcu=Join-Path $env:TEMP ('KB5094126_'+$b+'.msu')
     # Checkpoint/SSU first, then the LCU; DISM treats 3010 as "done, reboot needed".
     $okChk=$true
     if($k.chk){
       $okChk=$false
       if(DlFile $k.chk $chk $k.chkSha 'Downloading checkpoint (KB5043080)…'){
         Emit @{ t='install' }
         try{ $p=Start-Process dism.exe -ArgumentList @('/Online','/Add-Package',('/PackagePath:'+$chk),'/Quiet','/NoRestart') -Wait -PassThru -WindowStyle Hidden -ErrorAction Stop
              if($p.ExitCode -eq 0 -or $p.ExitCode -eq 3010){ $okChk=$true } }catch{}
         Emit @{ t='installdone' }
       }
       try{ Remove-Item $chk -Force -ErrorAction SilentlyContinue }catch{}
     }
     if($okChk -and (DlFile $k.lcu $lcu $k.lcuSha 'Downloading update (KB5094126)…')){
       Emit @{ t='install' }
       try{ $p=Start-Process dism.exe -ArgumentList @('/Online','/Add-Package',('/PackagePath:'+$lcu),'/Quiet','/NoRestart') -Wait -PassThru -WindowStyle Hidden -ErrorAction Stop
            if($p.ExitCode -eq 0 -or $p.ExitCode -eq 3010){ $script:LcuStaged=$true } }catch{}
       Emit @{ t='installdone' }
       try{ Remove-Item $lcu -Force -ErrorAction SilentlyContinue }catch{}
     }
     Emit @{ t='dldone' }
   }
 }
)}

# Enable a Windows feature via bundled ViVeTool. Gated to Win11 24H2/25H2 at
# build 26100.8655+ / 26200.8655+ (UBR = the number after the dot). Also runs
# when StepsUpdate just staged the LCU this session ($script:LcuStaged), because
# the build will meet the threshold after the single reboot at the end.
function StepsVive{ @(
 @{m='ViVeTool: enable feature 58989092 (Win11 24H2/25H2 26100.8655+)';
   d={
     $b=$script:Build
     $ok=($script:Ubr -ge 8655 -or $script:LcuStaged)
     $okver = ($b -eq 26100 -and $ok) -or ($b -eq 26200 -and $ok) -or ($b -gt 26200)
     $vt=$env:ARES_VIVETOOL
     -not ($okver -and $vt -and (Test-Path $vt))
   };
   a={ try{ & $env:ARES_VIVETOOL /enable /id:58989092 *>$null }catch{} }
 }
)}

function StepsInput2{ @(
 @{m='Mouse: bigger data queue + class thread priority';a={ $m='HKLM\SYSTEM\CurrentControlSet\Services\mouclass\Parameters'; Rg $m 'MouseDataQueueSize' 'REG_DWORD' 20; Rg $m 'ThreadPriority' 'REG_DWORD' 31 }}
)}

function StepsResp{ @(
 @{m='Snappy UI: fast app/hook timeouts, auto-end hung tasks';a={ $D='HKCU\Control Panel\Desktop'; Rg $D 'WaitToKillAppTimeout' 'REG_SZ' 2000; Rg $D 'HungAppTimeout' 'REG_SZ' 1000; Rg $D 'AutoEndTasks' 'REG_SZ' 1; Rg $D 'LowLevelHooksTimeout' 'REG_DWORD' 1000; Rg $D 'ForegroundLockTimeout' 'REG_DWORD' 0; Rg 'HKLM\SYSTEM\CurrentControlSet\Control' 'WaitToKillServiceTimeout' 'REG_SZ' 2000 }}
 @{m='Faster logon: no lock screen, no startup sound, no Aero shake';a={ Rg 'HKLM\SOFTWARE\Policies\Microsoft\Windows\Personalization' 'NoLockScreen' 'REG_DWORD' 1; Rg 'HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System' 'DisableStartupSound' 'REG_DWORD' 1; $A='HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced'; Rg $A 'DisallowShaking' 'REG_DWORD' 1; Rg $A 'ExtendedUIHoverTime' 'REG_DWORD' 1 }}
 @{m='Background apps off (per-user + policy)';d={ IsSet 'HKCU\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications' 'GlobalUserDisabled' 1 };a={ Rg 'HKCU\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications' 'GlobalUserDisabled' 'REG_DWORD' 1; Rg 'HKLM\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy' 'LetAppsRunInBackground' 'REG_DWORD' 2 }}
)}

function StepsDebloat2{ @(
 @{m='Remove provisioned (preinstalled) bloatware for new users';a={ $pat='Clipchamp|BingNews|BingWeather|GetHelp|Getstarted|SolitaireCollection|Microsoft.Todos|ZuneMusic|ZuneVideo|PowerAutomateDesktop|MicrosoftTeams|Bing|QuickAssist|Family|Copilot|People|Wallet'; try{ Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -match $pat } | ForEach-Object { Remove-AppxProvisionedPackage -Online -PackageName $_.PackageName -ErrorAction SilentlyContinue | Out-Null } }catch{} }}
 @{m='Uninstall OneDrive';a={ cmd /c 'taskkill /f /im OneDrive.exe' *>$null; $od="$env:SystemRoot\System32\OneDriveSetup.exe"; if(-not (Test-Path $od)){ $od="$env:SystemRoot\SysWOW64\OneDriveSetup.exe" }; if(Test-Path $od){ RunBG $od '/uninstall' } }}
 @{m='Taskbar: hide Widgets / Chat / Task View (Win10+11)';a={ $A='HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced'; Rg $A 'TaskbarDa' 'REG_DWORD' 0; Rg $A 'TaskbarMn' 'REG_DWORD' 0; Rg $A 'ShowTaskViewButton' 'REG_DWORD' 0; Rg 'HKLM\SOFTWARE\Policies\Microsoft\Dsh' 'AllowNewsAndInterests' 'REG_DWORD' 0; Rg 'HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Feeds' 'EnableFeeds' 'REG_DWORD' 0 }}
 @{m='Disable Cortana + Search highlights + web search';a={ Rg 'HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search' 'AllowCortana' 'REG_DWORD' 0; Rg 'HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search' 'EnableDynamicContentInWSB' 'REG_DWORD' 0; Rg 'HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search' 'DisableWebSearch' 'REG_DWORD' 1; Rg 'HKCU\Software\Microsoft\Windows\CurrentVersion\Search' 'BingSearchEnabled' 'REG_DWORD' 0 }}
)}

function StepsMaint{ @(
 @{m='Schedule weekly auto-clean (temp/prefetch/DNS)';a={
   $dir="$env:ProgramData\AresStore"; New-Item -ItemType Directory -Path $dir -Force -ErrorAction SilentlyContinue | Out-Null
   $cmdf="$dir\maint.cmd"
   $body=@'
@echo off
del /q /f /s "%TEMP%\*" >nul 2>&1
del /q /f /s "%SystemRoot%\Temp\*" >nul 2>&1
del /q /f /s "%SystemRoot%\Prefetch\*" >nul 2>&1
ipconfig /flushdns >nul 2>&1
'@
   Set-Content -Path $cmdf -Value $body -Encoding Ascii -ErrorAction SilentlyContinue
   schtasks /Create /TN 'AresStore Maintenance' /TR ('"'+$cmdf+'"') /SC WEEKLY /D SUN /ST 03:00 /RU SYSTEM /RL HIGHEST /F *>$null
 }}
)}

# ---- EXTREME group (security/thermal trade-offs, max smoothness) ----------
function StepsExtreme{ @(
 @{m='[EXTREME] Disable Spectre/Meltdown mitigations (faster CPU)';d={ IsSet 'HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management' 'FeatureSettingsOverride' 3 };a={ $M='HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management'; Rg $M 'FeatureSettingsOverride' 'REG_DWORD' 3; Rg $M 'FeatureSettingsOverrideMask' 'REG_DWORD' 3 }}
 @{m='[EXTREME] Disable memory compression + page combining';a={ try{ Disable-MMAgent -MemoryCompression -ErrorAction SilentlyContinue }catch{}; try{ Disable-MMAgent -PageCombining -ErrorAction SilentlyContinue }catch{} }}
 @{m='[EXTREME] Reduce DEP (nx OptIn) + disable VBS isolation';a={ BcdSet 'nx OptIn'; BcdSet 'vsmlaunchtype Off'; BcdSet 'isolatedcontext No'; BcdSet 'allowedinmemorysettings 0x0' }}
 @{m='[EXTREME] Disable Defender real-time protection';a={ try{ Set-MpPreference -DisableRealtimeMonitoring $true -ErrorAction SilentlyContinue }catch{}; Rg 'HKLM\SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection' 'DisableRealtimeMonitoring' 'REG_DWORD' 1 }}
 @{m='[EXTREME] Disable Windows Defender (antispyware)';a={ Rg 'HKLM\SOFTWARE\Policies\Microsoft\Windows Defender' 'DisableAntiSpyware' 'REG_DWORD' 1; try{ Set-MpPreference -DisableBehaviorMonitoring $true -DisableIOAVProtection $true -ErrorAction SilentlyContinue }catch{} }}
)}

function AllSteps{
 $s=@()
 $s+=StepsUI; $s+=StepsAI; $s+=StepsBloat; $s+=StepsDebloat2; $s+=StepsPrivacy
 $s+=StepsServices; $s+=StepsGame; $s+=StepsQoS; $s+=StepsPower; $s+=StepsCPU2; $s+=StepsSecurity
 $s+=StepsMemory; $s+=StepsMem2; $s+=StepsTimer; $s+=StepsInput; $s+=StepsInput2
 $s+=StepsNetwork; $s+=StepsNet2; $s+=StepsNet3; $s+=StepsGPU; $s+=StepsGPU2; $s+=StepsFS
 $s+=StepsResp; $s+=StepsBoot; $s+=StepsUpdate; $s+=StepsVive; $s+=StepsClean; $s+=StepsMaint
 $s+=StepsBranding
 return ,$s
}

function Emit($obj){
 $line = 'JX|' + ($obj | ConvertTo-Json -Compress)
 try{ [Console]::Out.WriteLine($line); [Console]::Out.Flush() }
 catch{ Write-Output $line }
}

$steps = AllSteps
$tot = $steps.Count; $ok=0; $fail=0; $skipped=0
for($s=0;$s -lt $tot;$s++){
 $pct=[int]((($s+1)/$tot)*100)
 $st=$steps[$s]
 $doSkip=$false
 if($st.ContainsKey('d')){ try{ if(& $st.d){ $doSkip=$true } }catch{} }
 Emit @{ t='step'; pct=$pct; idx=($s+1); total=$tot; msg=$st.m; skip=$doSkip }
 if($doSkip){ $skipped++ }
 else{ try{ & $st.a; $ok++ }catch{ $fail++ } }
 Start-Sleep -Milliseconds 120
}
Emit @{ t='done'; ok=$ok; fail=$fail; skipped=$skipped; reboot=$true }
