param([ValidateSet('win-x64','linux-x64')][string]$Rid = 'win-x64')
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
dotnet build tests/RudyEdit.Tests -c Release --nologo
if ($LASTEXITCODE) { throw 'Build failed' }
dotnet publish src/RudyEdit -c Release -r $Rid --self-contained true -p:RuntimeFrameworkVersion=10.0.13 -o "dist/$Rid" --nologo
if ($LASTEXITCODE) { throw 'Publish failed' }
python tools/stage_release_tests.py "dist/$Rid"
if ($LASTEXITCODE) { throw 'Staging test scripts failed' }
$exe = if ($Rid -eq 'win-x64') { "dist/$Rid/rudyedit.exe" } else { "dist/$Rid/rudyedit" }
python tests/release_runner.py --executable $exe
if ($LASTEXITCODE) { throw 'Executable tests failed or blocked; installers will not be created. See tests/results/release-tests.log in the published folder.' }
python tests/release_gate.py "dist/$Rid"
if ($LASTEXITCODE) { throw 'Pre-installer gate rejected this payload' }
Write-Host 'Native executable passed all required tests. Packaging entry points will verify this receipt again.'
Write-Host 'See packaging/README.md for installer tooling. No foreign-platform build may be packaged before its native test run.'
