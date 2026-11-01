$ErrorActionPreference='Stop'
$dotnet='C:/Users/JoannesVermorel/.dotnet11/dotnet.exe'
$root=$PSScriptRoot
$driver=Join-Path $root 'out/diagnostic/Release/net11.0/diagnostic.dll'
foreach ($entry in @(@('before','dev6'),@('after','dev7'),@('before-control','dev6'))) {
    $label=$entry[0]; $image=$entry[1]
    $start=[Diagnostics.ProcessStartInfo]::new($dotnet)
    $start.UseShellExecute=$false; $start.CreateNoWindow=$true
    $start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true
    $start.ArgumentList.Add($driver)
    $start.ArgumentList.Add((Join-Path $root "images/$image/Lokad.Lython.Benchmarks.dll"))
    $start.ArgumentList.Add("4cae38f4 core source / d703767c adapter source / exact 0.5.0-$image")
    $start.ArgumentList.Add((Join-Path $root 'driver/cases.json'))
    $process=[Diagnostics.Process]::Start($start)
    try {
        $stdout=$process.StandardOutput.ReadToEndAsync(); $stderr=$process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(30000)) {
            $process.Kill($true); $process.WaitForExit()
            throw "$label exceeded fixed30s process bound; no rerun."
        }
        $output=$stdout.GetAwaiter().GetResult(); $errors=$stderr.GetAwaiter().GetResult()
        [IO.File]::WriteAllText((Join-Path $root "$label.json"),$output)
        [IO.File]::WriteAllText((Join-Path $root "$label.stderr"),$errors)
        if ($process.ExitCode -ne 0 -or $errors.Length -ne 0) { throw "$label failed ($($process.ExitCode)): $errors" }
        $record=$output|ConvertFrom-Json
        if ($record.observations.Count -ne 4) {throw 'Missing expected observations'}
        Write-Host "$label complete, PID $($process.Id) joined, exit $($process.ExitCode)"
    } finally { $process.Dispose() }
}
