$ErrorActionPreference = 'Stop'
$env:DOTNET_ROOT = 'C:/Users/JoannesVermorel/.dotnet11'
$env:DOTNET_HOST_PATH = 'C:/Users/JoannesVermorel/.dotnet11/dotnet.exe'
$env:PATH = $env:DOTNET_ROOT + ';' + $env:PATH
$probeRoot = 'D:/code/Lython/.git/agent-notes/review-20261031/native-mutation-dispatch-b228'
$initialPath = Join-Path $probeRoot 'set-net11.0-False.json'
if ((Test-Path -LiteralPath $initialPath) -and -not (Test-Path -LiteralPath (Join-Path $probeRoot 'initial-probe-launch-error.txt'))) {
    Copy-Item -LiteralPath $initialPath -Destination (Join-Path $probeRoot 'initial-probe-launch-error.txt')
}
foreach ($framework in 'net11.0','net10.0') {
    $probeDll = 'D:/code/Lython/tools/LythonProbe/bin/Release/' + $framework + '/LythonProbe.dll'
    foreach ($asynchronous in $false,$true) {
        foreach ($operation in 'set','delete') {
            $sourcePath = Join-Path $probeRoot ($operation + '-immutable.py')
            $logPath = Join-Path $probeRoot ($operation + '-' + $framework + '-' + $asynchronous + '.json')
            $probeArgs = @($probeDll, $sourcePath, '--compare-python', '--python', 'C:/Python313/python.exe', '--json')
            if ($asynchronous) { $probeArgs += '--async' }
            dotnet @probeArgs > $logPath
            if ($LASTEXITCODE -ne 0) { Get-Content -LiteralPath $logPath -Encoding utf8; exit $LASTEXITCODE }
        }
    }
}
Write-Output 'All eight isolated index-order probes match CPython; both APIs/actual runtimes.'
