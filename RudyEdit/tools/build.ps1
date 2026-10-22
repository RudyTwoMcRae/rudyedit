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
$testExit = $LASTEXITCODE
if ($Rid -eq 'win-x64') {
    python tests/release_gate.py "dist/$Rid" --allow-test-failures
} else {
    python tests/release_gate.py "dist/$Rid"
}
if ($LASTEXITCODE) { throw 'Payload integrity/native test-attempt verification failed' }
if ($testExit) {
    Write-Warning 'Tests failed or blocked. The Windows payload is available for packaging; see tests/results/release-tests.log. This build is NOT RELEASE-READY.'
} else {
    Write-Host 'All required native executable tests passed.'
}
# Keep the test exit status accurate. CI packages the verified payload before
# reporting this failure as the final job outcome.
exit $testExit
