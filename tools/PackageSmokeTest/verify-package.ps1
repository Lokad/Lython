param([string] $OutputDirectory)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repoRoot 'artifacts/package-smoke' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
$project = Join-Path $repoRoot 'src/Lokad.Lython/Lokad.Lython.csproj'
[xml] $projectXml = Get-Content -Raw -LiteralPath $project
$version = [string] ($projectXml.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1)
$commit = (git -C $repoRoot rev-parse HEAD).Trim()

dotnet pack $project -c Release --nologo -o $OutputDirectory
if ($LASTEXITCODE -ne 0) { throw 'Release pack failed.' }
$package = Join-Path $OutputDirectory "Lokad.Lython.$version.nupkg"
$symbols = Join-Path $OutputDirectory "Lokad.Lython.$version.snupkg"
$archive = [IO.Compression.ZipFile]::OpenRead($package)
try {
    $entryNames = @($archive.Entries.FullName)
    foreach ($required in @('lib/net10.0/Lokad.Lython.dll', 'README.md', 'CHANGELOG.md', 'SUPPORTED_SYNTAX.md', 'SPEC.md', 'LICENSE.txt', 'THIRD_PARTY_NOTICES.md', 'third_party/Python.LICENSE.txt', 'icon.png')) {
        if ($required -notin $entryNames) { throw "Package is missing $required." }
    }
    $reader = [IO.StreamReader]::new($archive.GetEntry('Lokad.Lython.nuspec').Open())
    try { [xml] $nuspec = $reader.ReadToEnd() } finally { $reader.Dispose() }
    if ($nuspec.package.metadata.version -ne $version) { throw 'Package version mismatch.' }
    if ($nuspec.package.metadata.repository.commit -ne $commit) { throw 'Package repository commit mismatch.' }
    $dependencies = @($nuspec.package.metadata.dependencies.group.dependency.id | Sort-Object)
    if (($dependencies -join ',') -ne 'Lokad.Parsing,Lokad.Utf8Regex.PythonRe') { throw 'Unexpected package dependencies.' }
    if (@($entryNames | Where-Object { $_ -like 'lib/*/*.dll' }).Count -ne 1) { throw 'Unexpected package assemblies.' }
} finally { $archive.Dispose() }
$symbolArchive = [IO.Compression.ZipFile]::OpenRead($symbols)
try {
    if ('lib/net10.0/Lokad.Lython.pdb' -notin @($symbolArchive.Entries.FullName)) { throw 'Symbol package is missing the portable PDB.' }
} finally { $symbolArchive.Dispose() }

# Keep the consumer outside the checkout, with its own sources, project,
# NuGet configuration and package cache. Restore the exact local artifact.
$consumer = Join-Path ([IO.Path]::GetTempPath()) ('lython-package-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($consumer) | Out-Null
$feed = [Security.SecurityElement]::Escape($OutputDirectory)
@"
<configuration>
  <packageSources><clear /><add key="candidate" value="$feed" /><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources>
  <packageSourceMapping><packageSource key="candidate"><package pattern="Lokad.Lython" /></packageSource><packageSource key="nuget.org"><package pattern="*" /></packageSource></packageSourceMapping>
</configuration>
"@ | Set-Content -LiteralPath (Join-Path $consumer 'NuGet.Config') -Encoding utf8
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors><ImportDirectoryBuildProps>false</ImportDirectoryBuildProps><ImportDirectoryBuildTargets>false</ImportDirectoryBuildTargets><ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally></PropertyGroup>
  <ItemGroup><PackageReference Include="Lokad.Lython" Version="$version" /></ItemGroup>
</Project>
"@ | Set-Content -LiteralPath (Join-Path $consumer 'Consumer.csproj') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Program.cs') -Destination (Join-Path $consumer 'Program.cs')
$consumerProject = Join-Path $consumer 'Consumer.csproj'
$cache = Join-Path $consumer 'packages'
dotnet restore $consumerProject --configfile (Join-Path $consumer 'NuGet.Config') --packages $cache --nologo
if ($LASTEXITCODE -ne 0) { throw 'Isolated package restore failed.' }
$restoredPackage = Join-Path $cache "lokad.lython/$version/lokad.lython.$version.nupkg"
if ((Get-FileHash -LiteralPath $package -Algorithm SHA512).Hash -ne (Get-FileHash -LiteralPath $restoredPackage -Algorithm SHA512).Hash) { throw 'Restored package hash mismatch.' }
dotnet run --project $consumerProject -c Release --no-restore -- $version
if ($LASTEXITCODE -ne 0) { throw 'Isolated package consumer failed.' }
Write-Host "Verified package $package (SHA512 $((Get-FileHash -LiteralPath $package -Algorithm SHA512).Hash)). Consumer: $consumer"
