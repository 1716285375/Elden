$ErrorActionPreference = 'Stop'
$tag = $env:RELEASE_TAG
if ($tag -notmatch '^windows-\d{8}-\d{6}$') { throw 'Unexpected release tag.' }
$release = gh release view $tag --json isDraft,targetCommitish | ConvertFrom-Json
if ($LASTEXITCODE -ne 0 -or !$release.isDraft -or $release.targetCommitish -ne $env:GITHUB_SHA) {
    throw 'Expected a draft release built from this workflow commit.'
}
$download = Join-Path $env:RUNNER_TEMP 'elden-package'
New-Item -ItemType Directory -Path $download -Force | Out-Null
gh release download $tag --pattern 'Elden-Windows.zip*' --dir $download
if ($LASTEXITCODE -ne 0) { throw 'Package download failed.' }
$zip = Join-Path $download 'Elden-Windows.zip'
$checksum = (Get-Content ($zip + '.sha256') -Raw).Trim()
if ($checksum -notmatch '^([a-fA-F0-9]{64})  Elden-Windows\.zip$') { throw 'Invalid checksum file.' }
if ((Get-FileHash $zip -Algorithm SHA256).Hash -ne $Matches[1]) { throw 'Package checksum mismatch.' }
$archive = [IO.Compression.ZipFile]::OpenRead($zip)
try {
    if (!$archive.GetEntry('Elden.exe') -or !$archive.GetEntry('Elden_Data/globalgamemanagers')) { throw 'Incomplete Windows player.' }
    foreach ($entry in $archive.Entries) {
        if ($entry.FullName -match '(^/|^[A-Za-z]:|(^|/)\.\.(/|$)|_BurstDebugInformation_DoNotShip|_BackUpThisFolder_ButDontShipItWithYourGame|\.(pdb|mdb|log)$)') {
            throw 'The archive contains an unsafe path or development-only output.'
        }
    }
} finally { $archive.Dispose() }
$playerFolder = Join-Path $download 'player'
Expand-Archive -LiteralPath $zip -DestinationPath $playerFolder
$startupLog = Join-Path $env:RUNNER_TEMP 'elden-startup.log'
$arguments = '-batchmode -nographics -noaudio -logFile "{0}"' -f $startupLog
$player = Start-Process (Join-Path $playerFolder 'Elden.exe') -ArgumentList $arguments -WindowStyle Hidden -PassThru
try {
    Start-Sleep -Seconds 15
    if ($player.HasExited) { throw 'The player exited during the startup check.' }
    if (!(Test-Path $startupLog)) { throw 'Player startup log is missing.' }
    $startup = Get-Content -LiteralPath $startupLog -Raw
    if ($startup -match 'NullReferenceException|MissingMethodException|TypeLoadException|Crash!!!|Failed to load PlayerSettings') {
        throw 'Player startup reported an exception.'
    }
} finally {
    if (!$player.HasExited) { Stop-Process -Id $player.Id }
}
gh release edit $tag --draft=false --latest
if ($LASTEXITCODE -ne 0) { throw 'Release publication failed.' }
Write-Host 'Windows package checksum, contents and headless startup checks passed. Release published.'
