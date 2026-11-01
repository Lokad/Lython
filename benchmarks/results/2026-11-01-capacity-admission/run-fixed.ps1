param([string]$Variant,[string]$Output)
$ErrorActionPreference='Stop'
$root=Join-Path $PSScriptRoot $Variant
$driver=Join-Path $PSScriptRoot 'bin/Release/net11.0/ordered-dictionary.dll'
$assembly=Join-Path $root 'Lokad.Lython.Benchmarks.dll'
$catalog=Join-Path $PSScriptRoot 'catalog.json'
$stdout=Join-Path $PSScriptRoot ($Output+'.json')
$stderr=Join-Path $PSScriptRoot ($Output+'.stderr')
if ((Test-Path -LiteralPath $stdout) -or (Test-Path -LiteralPath $stderr)) { throw 'Refusing to overwrite a fixed observation.' }
$watch=[Diagnostics.Stopwatch]::StartNew()
$process=Start-Process -FilePath "$env:DOTNET_ROOT/dotnet.exe" -ArgumentList @($driver,$assembly,$Variant,$catalog) -RedirectStandardOutput $stdout -RedirectStandardError $stderr -WindowStyle Hidden -PassThru
$expired=-not $process.WaitForExit(30000)
if ($expired) { $process.Kill($true) }
$process.WaitForExit()
$watch.Stop()
@{variant=$Variant;pid=$process.Id;elapsedSeconds=$watch.Elapsed.TotalSeconds;boundSeconds=30;expired=$expired;exitCode=$process.ExitCode;joined=$process.HasExited} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $PSScriptRoot ($Output+'-process.json')) -Encoding utf8
if ($expired -or $process.ExitCode -ne 0) { throw 'Fixed worker failed or exhausted its observation bound; no retiming.' }
