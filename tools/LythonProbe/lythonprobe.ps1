$ErrorActionPreference = "Stop"

$toolRoot = $PSScriptRoot
$project = Join-Path $toolRoot "LythonProbe.csproj"
$configuration = "Debug"
$dll = Join-Path $toolRoot "bin/$configuration/net10.0/LythonProbe.dll"
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $toolRoot "../.."))
$inputs = @(
    Get-ChildItem -LiteralPath $toolRoot -File |
        Where-Object { $_.Extension -in ".cs", ".csproj" }
    Get-ChildItem -LiteralPath (Join-Path $repoRoot "src/Lokad.Lython") -Recurse -File |
        Where-Object { $_.Extension -in ".cs", ".csproj" }
    Get-ChildItem -LiteralPath $repoRoot -File |
        Where-Object { $_.Name -like "Directory.Build.*" -or $_.Name -like "Directory.Packages.*" }
)

$needsBuild = -not (Test-Path -LiteralPath $dll)
if (-not $needsBuild) {
    $builtAt = (Get-Item -LiteralPath $dll).LastWriteTimeUtc
    $needsBuild = $inputs | Where-Object {
        $_.LastWriteTimeUtc -gt $builtAt
    } | Select-Object -First 1
}

if ($needsBuild) {
    $buildOutput = & dotnet build $project --nologo --verbosity quiet 2>&1
    if ($LASTEXITCODE -ne 0) {
        $buildOutput | Write-Error
        exit $LASTEXITCODE
    }
}

if ($MyInvocation.ExpectingInput) {
    @($input) -join [Environment]::NewLine | & dotnet $dll @args
}
else {
    & dotnet $dll @args
}
exit $LASTEXITCODE
