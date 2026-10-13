param([Parameter(Mandatory)][string]$Stage,[Parameter(Mandatory)][string]$Executable,[Parameter(Mandatory)][string[]]$Arguments)
$ErrorActionPreference = 'Stop'
$dir = Join-Path $PSScriptRoot $Stage
if (Test-Path -LiteralPath $dir) { throw 'Preserve existing evidence' }
New-Item -ItemType Directory -Path $dir | Out-Null
$info = [Diagnostics.ProcessStartInfo]::new($Executable)
$info.UseShellExecute = $false; $info.CreateNoWindow = $true
$info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
$timer = [Diagnostics.Stopwatch]::StartNew()
$process = [Diagnostics.Process]::new(); $process.StartInfo = $info
[void]$process.Start(); $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
$timeout = -not $process.WaitForExit(28000)
if (-not $process.HasExited) { $process.Kill($true); if (-not $process.WaitForExit(1000)) { throw 'Cleanup failed' } }
[IO.File]::WriteAllText((Join-Path $dir 'stdout.txt'),$stdout.GetAwaiter().GetResult(),[Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $dir 'stderr.txt'),$stderr.GetAwaiter().GetResult(),[Text.UTF8Encoding]::new($false))
$timer.Stop()
$receipt = [ordered]@{ pid=$process.Id; exitCode=$process.ExitCode; hasExited=$process.HasExited; timedOut=$timeout; elapsedMillisecondsIncludingCleanup=$timer.ElapsedMilliseconds; executable=$Executable; executableSha256=(Get-FileHash -LiteralPath $Executable).Hash; arguments=$Arguments }
$receipt | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $dir 'process.receipt.json')
$receipt | ConvertTo-Json -Depth 5 -Compress
Get-Content -LiteralPath (Join-Path $dir 'stdout.txt') -Tail 3
if ($timeout -or $timer.ElapsedMilliseconds -ge 30000 -or $process.ExitCode -ne 0) { throw 'Owned command failed or exceeded bound' }