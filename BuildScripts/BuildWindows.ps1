param(
    [string]$ProjectPath = $env:ELDEN_PROJECT_PATH,
    [string]$OutputDirectory = $env:RUNNER_TEMP
)
$ErrorActionPreference = 'Stop'
if (!$ProjectPath -or !(Test-Path (Join-Path $ProjectPath 'ProjectSettings/ProjectVersion.txt'))) {
    throw 'ELDEN_PROJECT_PATH must point to the complete local Unity project.'
}
$actualCommit = (git -C $ProjectPath rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $actualCommit -ne $env:GITHUB_SHA) {
    throw 'The local project must be on the workflow commit before building.'
}
$dirty = git -C $ProjectPath status --porcelain -- Assets/_Game/Scripts Assets/_Game/Editor Packages ProjectSettings
if ($dirty) { throw 'Commit the local gameplay, build scripts and project settings before building.' }
$registryPath = Join-Path $env:USERPROFILE '.unity_skills/registry.json'
$registry = Get-Content -LiteralPath $registryPath -Raw | ConvertFrom-Json
$instance = $registry.PSObject.Properties | Where-Object { $_.Name -eq $ProjectPath } | Select-Object -ExpandProperty Value
if (!$instance) { throw 'Open the complete project in Unity with UnitySkills enabled.' }
$server = 'http://127.0.0.1:{0}' -f $instance.port
Write-Host 'Waiting for the local Unity editor to finish imports and maintenance.'
$deadline = [DateTime]::UtcNow.AddMinutes(20)
$ready = $false
while ([DateTime]::UtcNow -lt $deadline) {
    try {
        $health = Invoke-RestMethod "$server/health" -TimeoutSec 5
        $ready = $health.projectName -eq 'Elden' -and !$health.compilation.isCompiling -and $health.mainThreadIdleMs -lt 3000
        if ($ready) { break }
    } catch { }
    Start-Sleep -Seconds 5
}
if (!$ready) { throw 'The local Unity editor did not become available.' }
$body = @{ menuPath = 'Tools/ZZ/Build/Windows Player' } | ConvertTo-Json
$preview = Invoke-RestMethod -Method Post "$server/skill/editor_execute_menu?expectProject=Elden&mode=dryRun" -ContentType application/json -Body $body -TimeoutSec 30
if (!$preview.valid) { throw 'Unity build command validation failed.' }
$started = [DateTime]::UtcNow
$result = Invoke-RestMethod -Method Post "$server/skill/editor_execute_menu?expectProject=Elden" -ContentType application/json -Body $body -TimeoutSec 5400
$reportPath = Join-Path $ProjectPath '.utmp/windows-build.txt'
$report = Get-Content -LiteralPath $reportPath -Raw
if ((Get-Item $reportPath).LastWriteTimeUtc -lt $started -or $report -notmatch '^Succeeded' -or $report -notmatch '(?m)^Errors: 0\r?$') {
    throw 'Unity build failed. Review the local build report.'
}
if ($report -notmatch '(?m)^Path: (.+)\r?$') { throw 'Build output was not reported.' }
$executable = $Matches[1].Trim()
if (!(Test-Path -LiteralPath $executable)) { throw 'The player executable is missing.' }
$buildDirectory = Split-Path $executable
if (!(Test-Path (Join-Path $buildDirectory 'Elden_Data/globalgamemanagers'))) { throw 'The player data is missing.' }
$startupLog = Join-Path $OutputDirectory 'player-startup.log'
$playerArguments = '-screen-width 960 -screen-height 540 -screen-fullscreen 0 -force-d3d11 -logFile "{0}"' -f $startupLog
$player = Start-Process -FilePath $executable -ArgumentList $playerArguments -WindowStyle Hidden -PassThru
try {
    Start-Sleep -Seconds 15
    if ($player.HasExited) { throw 'The Windows player exited during the startup check.' }
    if (!(Test-Path $startupLog)) { throw 'The Windows player did not produce a startup log.' }
    $startup = Get-Content -LiteralPath $startupLog -Raw
    if ($startup -match 'NullReferenceException|MissingMethodException|TypeLoadException|Crash!!!|Failed to load PlayerSettings') {
        throw 'The Windows player reported a startup failure; inspect the local startup log.'
    }
} finally {
    if (!$player.HasExited) {
        $player.CloseMainWindow() | Out-Null
        if (!$player.WaitForExit(3000)) { Stop-Process -Id $player.Id }
    }
}
$zip = Join-Path $OutputDirectory 'Elden-Windows.zip'
if (Test-Path -LiteralPath $zip) { throw 'Use a fresh runner output directory.' }
[IO.Compression.ZipFile]::CreateFromDirectory($buildDirectory, $zip, [IO.Compression.CompressionLevel]::Optimal, $false)
if ((Get-Item $zip).Length -ge 2GB) { throw 'Build archive exceeds the GitHub Release asset limit.' }
$hashFile = $zip + '.sha256'
'{0}  Elden-Windows.zip' -f (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant() | Set-Content $hashFile -Encoding ascii
"zip=$zip" | Add-Content $env:GITHUB_OUTPUT
"checksum=$hashFile" | Add-Content $env:GITHUB_OUTPUT
Write-Host 'Windows player built and packaged successfully.'
