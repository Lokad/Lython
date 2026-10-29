[CmdletBinding(PositionalBinding = $false)]
param(
    [ValidateSet('net11.0', 'net10.0')]
    [string] $Framework = 'net11.0',
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $ProbeArguments,
    # Enable pipeline binding; $input below retains every line until invocation.
    [Parameter(ValueFromPipeline = $true)]
    [AllowEmptyString()]
    [string] $InputText
)

$ErrorActionPreference = "Stop"

$toolRoot = $PSScriptRoot
$project = Join-Path $toolRoot "LythonProbe.csproj"
$configuration = "Debug"
$dll = Join-Path $toolRoot "bin/$configuration/$Framework/LythonProbe.dll"
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $toolRoot "../.."))
$inputs = @(
    Get-ChildItem -LiteralPath $toolRoot -File |
        Where-Object { $_.Extension -in ".cs", ".csproj" }
    foreach ($library in @('Lokad.Lython', 'Lokad.Lython.Utf8Regex')) {
        Get-ChildItem -LiteralPath (Join-Path $repoRoot "src/$library") -Recurse -File |
            Where-Object { $_.Extension -in ".cs", ".csproj" -and $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }
    }
    Get-ChildItem -LiteralPath $repoRoot -File |
        Where-Object { $_.Name -like "Directory.Build.*" -or $_.Name -like "Directory.Packages.*" -or $_.Name -eq 'global.json' }
)

$needsBuild = -not (Test-Path -LiteralPath $dll)
if (-not $needsBuild) {
    $builtAt = (Get-Item -LiteralPath $dll).LastWriteTimeUtc
    $needsBuild = $inputs | Where-Object {
        $_.LastWriteTimeUtc -gt $builtAt
    } | Select-Object -First 1
}

if ($needsBuild) {
    $buildOutput = & dotnet build $project -f $Framework --nologo --verbosity quiet 2>&1
    if ($LASTEXITCODE -ne 0) {
        $buildOutput | Write-Error
        exit $LASTEXITCODE
    }
}

if ($MyInvocation.ExpectingInput) {
    @($input) -join [Environment]::NewLine | & dotnet $dll @ProbeArguments
}
else {
    & dotnet $dll @ProbeArguments
}
exit $LASTEXITCODE
