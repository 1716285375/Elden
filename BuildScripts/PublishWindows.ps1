param([string]$ProjectPath = (Split-Path $PSScriptRoot))
$ErrorActionPreference = 'Stop'
$ProjectPath = (Resolve-Path $ProjectPath).Path
$commit = (git -C $ProjectPath rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Unable to identify the project commit.' }
$stamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
$output = Join-Path $ProjectPath ('.utmp/windows-publish/' + $stamp)
New-Item -ItemType Directory -Path $output -Force | Out-Null
$env:GITHUB_SHA = $commit
$env:GITHUB_OUTPUT = Join-Path $output 'build-outputs.txt'
& (Join-Path $PSScriptRoot 'BuildWindows.ps1') -ProjectPath $ProjectPath -OutputDirectory $output
$zip = Join-Path $output 'Elden-Windows.zip'
$tag = 'windows-' + $stamp
gh release create $tag $zip ($zip + '.sha256') --repo 1716285375/Elden --draft --target $commit --title 'Windows build' --notes '下载 Elden-Windows.zip，完整解压后运行 Elden.exe。'
if ($LASTEXITCODE -ne 0) { throw 'Draft package upload failed.' }
gh workflow run windows-release.yml --repo 1716285375/Elden --ref main --field ('tag=' + $tag)
if ($LASTEXITCODE -ne 0) { throw 'Release verification workflow could not start.' }
Write-Host ('Release verification started: ' + $tag)
