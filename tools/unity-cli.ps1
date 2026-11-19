<#
.SYNOPSIS
  Unity CLI bridge for the Echo project.

.DESCRIPTION
  Runs Unity against the Echo project without disturbing an open Editor.

  Routing:
    Editor closed  -> batch mode on the real project (fast, authoritative).
    Editor open    -> the running Editor handles ping/info/compile/validate/exec
                      through a file queue (Temp/echo-cli/queue).
    Editor busy    -> falls back to a shadow copy of the project on D:, so
                      compile/validate/test/build still work. Nothing in the
                      open Editor is touched.

.EXAMPLES
  .\tools\unity-cli.cmd ping
  .\tools\unity-cli.cmd info
  .\tools\unity-cli.cmd compile
  .\tools\unity-cli.cmd validate
  .\tools\unity-cli.cmd test edit
  .\tools\unity-cli.cmd build win
  .\tools\unity-cli.cmd refresh
  .\tools\unity-cli.cmd shadow reset
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$Command = 'help',

    [Parameter(Position = 1, ValueFromRemainingArguments = $true)]
    [string[]]$Rest = @(),

    [string]$ProjectPath = '',
    [switch]$StopEditor,
    [switch]$Shadow,
    [switch]$NoGraphics,
    [switch]$Yes
)

$ErrorActionPreference = 'Stop'

# ------------------------------------------------------------------ paths

$RepoRoot = Split-Path -Parent $PSScriptRoot
if (-not $ProjectPath) { $ProjectPath = Join-Path $RepoRoot 'Echo' }
if (-not (Test-Path $ProjectPath)) { throw "Unity project not found: $ProjectPath" }
$ProjectPath = (Resolve-Path $ProjectPath).Path

$versionFile = Join-Path $ProjectPath 'ProjectSettings\ProjectVersion.txt'
if (-not (Test-Path $versionFile)) { throw "Not a Unity project (no ProjectSettings\ProjectVersion.txt): $ProjectPath" }
$unityVersion = ((Select-String -Path $versionFile -Pattern '^m_EditorVersion:\s*(.+)$').Matches[0].Groups[1].Value).Trim()

$logDir = Join-Path $ProjectPath 'Logs\cli'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$logFile = Join-Path $logDir "$stamp-$Command.log"
$script:EditorLogPath = Join-Path $env:LOCALAPPDATA 'Unity\Editor\Editor.log'

$ShadowProject = if ($env:ECHO_CLI_SHADOW) { $env:ECHO_CLI_SHADOW } else { 'D:\Unity\.echo-cli-shadow\Echo' }

# ------------------------------------------------------------- utilities

function Write-Step($Message) { Write-Host ">> $Message" -ForegroundColor Cyan }
function Write-Ok($Message) { Write-Host "OK  $Message" -ForegroundColor Green }
function Write-Warn($Message) { Write-Host "!   $Message" -ForegroundColor Yellow }
function Write-Fail($Message) { Write-Host "X   $Message" -ForegroundColor Red }

function Resolve-UnityExe {
    if ($env:UNITY_EDITOR -and (Test-Path $env:UNITY_EDITOR)) { return $env:UNITY_EDITOR }

    $hub = 'C:\Program Files\Unity\Hub\Editor'
    $candidate = Join-Path $hub "$unityVersion\Editor\Unity.exe"
    if (Test-Path $candidate) { return $candidate }

    $installed = Get-ChildItem $hub -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending
    foreach ($dir in $installed) {
        $exe = Join-Path $dir.FullName 'Editor\Unity.exe'
        if (Test-Path $exe) {
            Write-Warn "Unity $unityVersion not installed; using $($dir.Name) instead."
            return $exe
        }
    }
    throw "Unity $unityVersion not found. Install it via Unity Hub or set `$env:UNITY_EDITOR."
}

$script:UnityExe = Resolve-UnityExe

function Get-UnityProcessForProject {
    $normalized = $ProjectPath -replace '\\', '/'
    Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" -ErrorAction SilentlyContinue |
        Where-Object {
            if (-not $_.CommandLine) { return $false }
            $cmd = $_.CommandLine -replace '\\', '/'
            $cmd.Contains($normalized)
        }
}

function Stop-UnityEditor {
    $procs = @(Get-UnityProcessForProject)
    if ($procs.Count -eq 0) { Write-Ok 'No Unity Editor is running for this project.'; return }

    if (-not $Yes -and -not $StopEditor) { throw 'Refusing to close the Editor without -Yes.' }

    Write-Warn "Closing Unity Editor (PID $($procs.ProcessId -join ', ')). Unsaved changes will be lost."
    foreach ($p in $procs) {
        $gp = Get-Process -Id $p.ProcessId -ErrorAction SilentlyContinue
        if ($gp) { [void]$gp.CloseMainWindow() }
    }

    $deadline = (Get-Date).AddSeconds(45)
    while ((Get-Date) -lt $deadline) {
        if (-not (Get-UnityProcessForProject)) { Write-Ok 'Editor closed.'; return }
        Start-Sleep -Milliseconds 500
    }

    foreach ($p in @(Get-UnityProcessForProject)) {
        Write-Warn "Force-killing Unity PID $($p.ProcessId)."
        Stop-Process -Id $p.ProcessId -Force -ErrorAction SilentlyContinue
    }
    Start-Sleep -Seconds 2
    Write-Ok 'Editor stopped.'
}

function Assert-ProjectUnlocked {
    $lockFile = Join-Path $ProjectPath 'Temp\UnityLockfile'
    if (-not (Test-Path $lockFile)) { return }

    if (Get-UnityProcessForProject) {
        if ($StopEditor) { Stop-UnityEditor; return }
        throw "The Unity Editor has this project open."
    }

    if (Get-Process Unity -ErrorAction SilentlyContinue) {
        throw "Temp\UnityLockfile exists and a Unity process is running, but it could not be matched to this project."
    }

    Remove-Item $lockFile -Force -ErrorAction SilentlyContinue
    if (Test-Path $lockFile) { throw "Project is locked by another process: $lockFile" }
    Write-Warn 'Cleared a stale UnityLockfile.'
}

# ---------------------------------------------------------- live bridge

$script:LiveAllowedMethods = @('Ping', 'Info', 'ValidateLive', 'Refresh', 'EchoSceneBuilder.AddColliders')

function Test-LiveEditor { [bool](Get-UnityProcessForProject) }

function Send-LiveCommand {
    param([string]$Method, [int]$TimeoutSec = 180)

    $queue = Join-Path $ProjectPath 'Temp\echo-cli\queue'
    New-Item -ItemType Directory -Force -Path $queue | Out-Null
    $id = [guid]::NewGuid().ToString('N')
    $reqPath = Join-Path $queue "$id.req.json"
    $resPath = Join-Path $queue "$id.res.json"

    $payload = @{ id = $id; method = $Method; args = '' } | ConvertTo-Json -Compress
    Set-Content -Path $reqPath -Value $payload -Encoding UTF8

    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while (-not (Test-Path $resPath)) {
        if ((Get-Date) -gt $deadline) {
            Remove-Item $reqPath -Force -ErrorAction SilentlyContinue
            throw "No response from the running Editor after $TimeoutSec s (it may be compiling, importing, or in Play Mode)."
        }
        Start-Sleep -Milliseconds 150
    }

    $response = Get-Content $resPath -Raw | ConvertFrom-Json
    Remove-Item $reqPath, $resPath -Force -ErrorAction SilentlyContinue
    return $response
}

function Select-OutputLines {
    param($Response)
    @($Response.output) |
        Where-Object { $_ -match '^\[EchoCli\]\s*' } |
        ForEach-Object { $_ -replace '^\[EchoCli\]\s*', '' }
}

function Complete-Live {
    param($Response)

    Select-OutputLines $Response | ForEach-Object { Write-Host "  $_" }

    if (-not $Response.ok) {
        Write-Fail "Live Editor error: $($Response.error)"
        exit 1
    }

    if ([int]$Response.exitCode -ne 0) {
        Write-Fail "Live command reported exit code $($Response.exitCode)."
    }
    else {
        Write-Ok "Live Editor responded in $($Response.durationMs) ms."
    }
    exit [int]$Response.exitCode
}

function Get-EditorLogErrors {
    $path = $script:EditorLogPath
    if (-not (Test-Path $path)) { return @() }

    try {
        $stream = [System.IO.File]::Open($path, 'Open', 'Read', 'ReadWrite')
        $reader = New-Object System.IO.StreamReader($stream)
        $text = $reader.ReadToEnd()
        $reader.Close()
        $stream.Close()
    }
    catch { return @() }

    $lines = $text -split "`r?`n" | Select-Object -Last 400
    @($lines | Where-Object { $_ -match 'error CS\d+' } | Select-Object -Unique -First 30)
}

function Test-LiveBridge {
    param([int]$TimeoutSec = 6)
    try {
        $response = Send-LiveCommand 'Ping' -TimeoutSec $TimeoutSec
        return [bool]$response.ok
    }
    catch { return $false }
}

function Invoke-LiveCompileCheck {
    $response = Send-LiveCommand 'Ping'
    $lines = Select-OutputLines $response
    $lines | ForEach-Object { Write-Host "  $_" }

    if (-not $response.ok) {
        Write-Fail "Live Editor error: $($response.error)"
        exit 1
    }

    $compileFailed = $false
    foreach ($line in $lines) {
        if ($line -match '^compileFailed=(\w+)') { $compileFailed = ($matches[1] -eq 'True') }
    }

    $logErrors = Get-EditorLogErrors
    if ($compileFailed -or $logErrors.Count -gt 0) {
        Write-Fail 'Compilation failed (live check).'
        $logErrors | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
        Write-Host "  Editor log: $script:EditorLogPath" -ForegroundColor DarkGray
        exit 1
    }

    Write-Ok 'Scripts compile cleanly (live check).'
    exit 0
}

# -------------------------------------------------------- shadow project

function Sync-Shadow {
    $shadow = $ShadowProject

    if (-not (Test-Path (Join-Path $shadow 'Assets'))) {
        Write-Step "Creating shadow project at $shadow (first run copies Library, ~2 GB)"
        New-Item -ItemType Directory -Force -Path $shadow | Out-Null
        robocopy $ProjectPath $shadow /E /MT:16 /NFL /NDL /NJH /NJS /R:1 /W:1 `
            /XD Temp Logs UserSettings obj .vs Builds /XF UnityLockfile | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE while creating the shadow project." }
        return
    }

    Write-Step "Syncing project into shadow copy..."
    foreach ($dir in 'Assets', 'Packages', 'ProjectSettings') {
        robocopy (Join-Path $ProjectPath $dir) (Join-Path $shadow $dir) /MIR /MT:8 /NFL /NDL /NJH /NJS /R:1 /W:1 | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE while syncing $dir." }
    }
}

function Reset-Shadow {
    if (Test-Path $ShadowProject) {
        Write-Step "Removing $ShadowProject"
        Remove-Item $ShadowProject -Recurse -Force
        Write-Ok 'Shadow project removed.'
    }
    else { Write-Ok 'No shadow project exists.' }
}

function Invoke-Unity {
    param([string]$Target, [string[]]$UnityArgs, [string]$TargetLog = $logFile)

    $script:ActiveLog = $TargetLog
    $all = @('-batchmode', '-projectPath', $Target, '-logFile', $TargetLog)
    if ($NoGraphics) { $all += '-nographics' }
    $all += $UnityArgs

    Write-Step "Unity $unityVersion"
    Write-Host "  $($all -join ' ')" -ForegroundColor DarkGray
    & $script:UnityExe @all 2>&1 | Out-Host
    return [int]$LASTEXITCODE
}

function Get-LogErrors {
    $path = if ($script:ActiveLog) { $script:ActiveLog } else { $logFile }
    if (-not (Test-Path $path)) { return @() }
    @(Select-String -Path $path -Pattern 'error CS\d+|Scripts have compiler errors|\[EchoCli\] PROBLEM|\[EchoCli\] EXCEPTION' -CaseSensitive:$false |
        ForEach-Object { $_.Line.Trim() } |
        Select-Object -Unique -First 40)
}

function Show-LogTail {
    param([int]$Lines = 40)
    $path = if ($script:ActiveLog) { $script:ActiveLog } else { $logFile }
    if (-not (Test-Path $path)) { return }
    Write-Host ''
    Write-Host "--- last $Lines lines of $path ---" -ForegroundColor DarkGray
    Get-Content $path -Tail $Lines
}

function Complete-Run {
    param([int]$Code)

    $errors = Get-LogErrors
    if ($errors.Count -gt 0) {
        Write-Host ''
        Write-Fail "$($errors.Count) error line(s) in the log:"
        $errors | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    }

    if ($Code -eq 0 -and $errors.Count -eq 0) { Write-Ok 'Success.' }
    elseif ($Code -eq 0) { Write-Warn 'Unity exited 0 but the log contains errors (see above).' }
    else {
        Write-Fail "Unity exited with code $Code."
        Show-LogTail -Lines 30
    }

    Write-Host "  log: $(if ($script:ActiveLog) { $script:ActiveLog } else { $logFile })" -ForegroundColor DarkGray
    exit $Code
}

function Invoke-Remote {
    # Runs a batch command against the shadow copy.
    param([string[]]$UnityArgs)
    Sync-Shadow
    $shadowLog = Join-Path $logDir "$stamp-$Command-shadow.log"
    Write-Step "Running against the shadow copy (your Editor stays untouched)."
    Invoke-Unity -Target $ShadowProject -UnityArgs $UnityArgs -TargetLog $shadowLog
}

# -------------------------------------------------------------- commands

function Show-Help {
    Write-Host @'
Unity CLI bridge for the Echo project

  unity-cli.cmd ping                 live Editor health check
  unity-cli.cmd info                 project + build settings summary
  unity-cli.cmd compile              script compile check
  unity-cli.cmd validate             compile + scene/build-settings checks
  unity-cli.cmd exec <Class.Method>  run a static Editor method
  unity-cli.cmd test [edit|play]     run EditMode or PlayMode tests
  unity-cli.cmd build [win|android]  build a player into Builds/
  unity-cli.cmd refresh              force asset reimport in the live Editor
  unity-cli.cmd logs [n]             tail the newest CLI log
  unity-cli.cmd shadow reset         delete the background copy
  unity-cli.cmd open                 open the project in the Editor
  unity-cli.cmd close                close the Editor for this project

Routing
  Editor closed : batch mode on the real project
  Editor open   : ping/info/compile/validate/exec run inside it (file queue)
  Editor busy   : compile/validate/test/build/exec use a shadow copy on D:
                  (D:\Unity\.echo-cli-shadow\Echo, override with ECHO_CLI_SHADOW)

Options
  -ProjectPath <dir>   override the project (default: <repo>\Echo)
  -Shadow              force the shadow copy even when the Editor is idle
  -StopEditor          close the running Editor instead of using it
  -Yes                 skip the close confirmation
  -NoGraphics          add -nographics to batch runs
'@
}

function Resolve-LiveOr {
    param([string]$LiveMethod, [string[]]$BatchMethod, [switch]$AllowShadow = $true)

    if ($Shadow) { Complete-Run (Invoke-Remote $BatchMethod) }

    if ((Test-LiveEditor) -and -not $StopEditor) {
        if (Test-LiveBridge -TimeoutSec 6) {
            Write-Step 'Using the running Editor (live bridge).'
            Complete-Live (Send-LiveCommand $LiveMethod)
        }
        if ($AllowShadow) {
            Write-Warn 'The running Editor did not answer (compiling, importing, or Play Mode). Using the shadow copy.'
            Complete-Run (Invoke-Remote $BatchMethod)
        }
        throw 'The running Editor is busy and cannot answer bridge commands right now.'
    }

    if ($Shadow) {
        Complete-Run (Invoke-Remote $BatchMethod)
    }

    Assert-ProjectUnlocked
    Complete-Run (Invoke-Unity -Target $ProjectPath -UnityArgs $BatchMethod)
}

switch ($Command.ToLowerInvariant()) {
    'help' { Show-Help; exit 0 }

    'ping' {
        if (-not (Test-LiveEditor)) { throw 'No Unity Editor is running for this project. Start it with: unity-cli.cmd open' }
        Complete-Live (Send-LiveCommand 'Ping')
    }

    'info' {
        Resolve-LiveOr -LiveMethod 'Info' -BatchMethod @('-executeMethod', 'EchoCli.Info', '-quit')
    }

    'compile' {
        if ((Test-LiveEditor) -and -not $StopEditor -and -not $Shadow) {
            if (Test-LiveBridge -TimeoutSec 6) { Invoke-LiveCompileCheck }
            Write-Warn 'The running Editor did not answer. Compiling against the shadow copy.'
            Complete-Run (Invoke-Remote @('-quit'))
        }
        if ($Shadow) { Complete-Run (Invoke-Remote @('-quit')) }
        Assert-ProjectUnlocked
        Complete-Run (Invoke-Unity -Target $ProjectPath -UnityArgs @('-quit'))
    }

    'validate' {
        Resolve-LiveOr -LiveMethod 'ValidateLive' -BatchMethod @('-executeMethod', 'EchoCli.Validate', '-quit')
    }

    'exec' {
        if ($Rest.Count -eq 0) { throw 'Usage: unity-cli.cmd exec <Namespace.Class.Method> [args...]' }
        $method = $Rest[0]
        $shortName = $method -replace '^EchoCli\.', ''
        $methodArgs = @()
        if ($Rest.Count -gt 1) { $methodArgs = $Rest[1..($Rest.Count - 1)] }

        if ((Test-LiveEditor) -and -not $StopEditor -and -not $Shadow -and ($script:LiveAllowedMethods -contains $shortName)) {
            if (Test-LiveBridge -TimeoutSec 6) {
                Write-Step "Executing $method in the running Editor."
                Complete-Live (Send-LiveCommand $shortName)
            }
        }

        $batch = @('-executeMethod', $method, '-quit') + $methodArgs
        if ($Shadow -or ((Test-LiveEditor) -and -not $StopEditor)) {
            Complete-Run (Invoke-Remote $batch)
        }
        Assert-ProjectUnlocked
        Complete-Run (Invoke-Unity -Target $ProjectPath -UnityArgs $batch)
    }

    'refresh' {
        if ((Test-LiveEditor) -and -not $StopEditor) {
            if (-not (Test-LiveBridge -TimeoutSec 6)) {
                throw 'The running Editor did not answer. Asset refresh must run inside it; wait for it to finish compiling/importing, then retry.'
            }
            Complete-Live (Send-LiveCommand 'Refresh' -TimeoutSec 900)
        }
        if ($Shadow) { throw 'Refresh only applies to the live Editor; the shadow copy is synced from disk automatically.' }
        Assert-ProjectUnlocked
        Complete-Run (Invoke-Unity -Target $ProjectPath -UnityArgs @('-executeMethod', 'EchoCli.Reimport', '-quit'))
    }

    'reimport' {
        if ((Test-LiveEditor) -and -not $StopEditor) {
            if (-not (Test-LiveBridge -TimeoutSec 6)) {
                throw 'The running Editor did not answer. Asset refresh must run inside it; wait for it to finish compiling/importing, then retry.'
            }
            Complete-Live (Send-LiveCommand 'Refresh' -TimeoutSec 900)
        }
        Assert-ProjectUnlocked
        Complete-Run (Invoke-Unity -Target $ProjectPath -UnityArgs @('-executeMethod', 'EchoCli.Reimport', '-quit'))
    }

    'test' {
        $platform = 'EditMode'
        if ($Rest.Count -gt 0 -and $Rest[0] -match '^(play|playmode)$') { $platform = 'PlayMode' }
        $results = Join-Path $logDir "$stamp-tests-$platform.xml"

        if ($Shadow -or ((Test-LiveEditor) -and -not $StopEditor)) {
            $code = Invoke-Remote @('-runTests', '-testPlatform', $platform, '-testResults', $results)
            if (Test-Path $results) {
                try {
                    [xml]$xml = Get-Content $results -Raw
                    $run = $xml.'test-run'
                    Write-Host "tests total=$($run.total) passed=$($run.passed) failed=$($run.failed) skipped=$($run.skipped) duration=$($run.duration)s"
                }
                catch { Write-Warn "Could not parse test results: $results" }
            }
            Complete-Run $code
        }

        Assert-ProjectUnlocked
        $code = Invoke-Unity -Target $ProjectPath -UnityArgs @('-runTests', '-testPlatform', $platform, '-testResults', $results)
        if (Test-Path $results) {
            try {
                [xml]$xml = Get-Content $results -Raw
                $run = $xml.'test-run'
                Write-Host "tests total=$($run.total) passed=$($run.passed) failed=$($run.failed) skipped=$($run.skipped) duration=$($run.duration)s"
            }
            catch { Write-Warn "Could not parse test results: $results" }
        }
        Complete-Run $code
    }

    'build' {
        $flavor = 'win'
        if ($Rest.Count -gt 0) { $flavor = $Rest[0].ToLowerInvariant() }
        $target = switch ($flavor) {
            'android' { 'Android' }
            'win' { 'Win64' }
            'windows' { 'Win64' }
            default { throw "Unknown build flavor '$flavor'. Use win or android." }
        }
        $output = if ($Rest.Count -gt 1) {
            $Rest[1]
        }
        elseif ($target -eq 'Android') {
            Join-Path $ProjectPath 'Builds\Android\Echo.apk'
        }
        else {
            Join-Path $ProjectPath 'Builds\Windows\Echo.exe'
        }
        $buildArgs = @('-buildTarget', $target, '-executeMethod', 'EchoCli.Build', '-cliOut', $output, '-quit')

        if ($Shadow -or ((Test-LiveEditor) -and -not $StopEditor)) {
            Complete-Run (Invoke-Remote $buildArgs)
        }

        Assert-ProjectUnlocked
        Complete-Run (Invoke-Unity -Target $ProjectPath -UnityArgs $buildArgs)
    }

    'logs' {
        $lines = 60
        if ($Rest.Count -gt 0) { [void][int]::TryParse($Rest[0], [ref]$lines) }
        $latest = Get-ChildItem $logDir -Filter '*.log' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if (-not $latest) { Write-Warn "No CLI logs yet in $logDir"; exit 0 }
        Write-Host $latest.FullName -ForegroundColor DarkGray
        Get-Content $latest.FullName -Tail $lines
        exit 0
    }

    'shadow' {
        if ($Rest.Count -gt 0 -and $Rest[0] -eq 'reset') { Reset-Shadow; exit 0 }
        Write-Host "Shadow project: $ShadowProject"
        Write-Host "Exists: $(Test-Path $ShadowProject)"
        exit 0
    }

    'open' {
        if (Get-UnityProcessForProject) { throw 'The Editor is already open for this project.' }
        Write-Step "Opening $ProjectPath"
        Start-Process -FilePath $script:UnityExe -ArgumentList "-projectPath `"$ProjectPath`""
        exit 0
    }

    'close' {
        Stop-UnityEditor
        exit 0
    }

    default {
        Write-Fail "Unknown command '$Command'."
        Show-Help
        exit 2
    }
}




