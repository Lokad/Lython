$ErrorActionPreference = 'Stop'
$dotnet = 'C:/Users/JoannesVermorel/.dotnet11/dotnet.exe'
$observations = @()
foreach ($framework in @('net11.0', 'net10.0')) {
    $fixture = Join-Path $PSScriptRoot "out/Lokad.Lython.WorkerFixture/Release/$framework/Lokad.Lython.WorkerFixture.dll"
    foreach ($stage in @('compile', 'match', 'cursor')) {
        $start = [Diagnostics.ProcessStartInfo]::new($dotnet)
        $start.UseShellExecute = $false
        $start.CreateNoWindow = $true
        $start.RedirectStandardOutput = $true
        $start.RedirectStandardError = $true
        $start.ArgumentList.Add($fixture)
        $start.ArgumentList.Add('regex-cancellation')
        $start.ArgumentList.Add($stage)
        $process = [Diagnostics.Process]::Start($start)
        try {
            $stdout = $process.StandardOutput.ReadToEndAsync()
            $stderr = $process.StandardError.ReadToEndAsync()
            if (-not $process.WaitForExit(25000)) {
                $process.Kill($true)
                $process.WaitForExit()
                throw "$framework $stage exceeded the existing 25-second bound."
            }
            $output = $stdout.GetAwaiter().GetResult()
            $errorOutput = $stderr.GetAwaiter().GetResult()
            if ($process.ExitCode -ne 0 -or $errorOutput.Length -ne 0) {
                throw "$framework $stage failed: $errorOutput"
            }
            $result = $output | ConvertFrom-Json
            if (-not $result.runningAtCancellation -or -not $result.cancelled -or
                -not $result.originalToken -or $result.balance -ne 0) {
                throw "Incomplete in-flight cancellation or unwind: $output"
            }
            $observations += @{ framework=$framework; fixture=$fixture; result=$result;
                adapterSHA256=(Get-FileHash (Join-Path (Split-Path $fixture) 'Lokad.Lython.Utf8Regex.dll')).Hash;
                producerSHA256=(Get-FileHash (Join-Path (Split-Path $fixture) 'Lokad.Utf8Regex.PythonRe.dll')).Hash }
            Write-Host "$framework ${stage}: in-flight cancellation, original token, zero balance."
        } finally { $process.Dispose() }
    }
}
$observations | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'native10-cancellation.json') -Encoding utf8NoBOM
