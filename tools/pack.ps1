$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))

dotnet pack (Join-Path $repoRoot 'src/Lokad.Lython/Lokad.Lython.csproj') -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Release core pack failed.' }

dotnet pack (Join-Path $repoRoot 'src/Lokad.Lython.Utf8Regex/Lokad.Lython.Utf8Regex.csproj') -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Release adapter pack failed.' }
