<#
Intent: Validate the openpyxl fixture catalog, manifest coverage, and public-workbook hygiene.
Usage: powershell -ExecutionPolicy Bypass -File tools/ValidateOpenPyxlFixtures.ps1 [-FixtureRoot path] [-AllowUncovered]
#>

param(
    [string] $FixtureRoot = "",
    [switch] $AllowUncovered
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($FixtureRoot)) {
    $FixtureRoot = Join-Path $repoRoot "tests/Fixtures/openpyxl"
}

$caseRoot = Join-Path $FixtureRoot "cases"
$familyRoot = Join-Path $FixtureRoot "families"
$idPattern = "^[a-z0-9]+(-[a-z0-9]+)*$"
$publicCreator = "Lython Fixture Generator"
$publicCommentAuthor = "Fixture Author"

Add-Type -AssemblyName System.IO.Compression.FileSystem

function Add-Issue([System.Collections.Generic.List[string]] $issues, [string] $message) {
    $issues.Add($message) | Out-Null
}

function Read-JsonFile([string] $path) {
    try {
        return Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
    }
    catch {
        throw "Invalid JSON in $path. $($_.Exception.Message)"
    }
}

function Read-ZipEntryText([System.IO.Compression.ZipArchiveEntry] $entry) {
    $stream = $entry.Open()
    try {
        $buffer = [byte[]]::new($entry.Length)
        $offset = 0
        while ($offset -lt $buffer.Length) {
            $read = $stream.Read($buffer, $offset, $buffer.Length - $offset)
            if ($read -eq 0) { break }
            $offset += $read
        }

        $utf8 = [System.Text.Encoding]::UTF8.GetString($buffer)
        $unicode = [System.Text.Encoding]::Unicode.GetString($buffer)
        return $utf8 + "`n" + $unicode
    }
    finally {
        $stream.Dispose()
    }
}

function Validate-WorkbookHygiene(
    [string] $caseId,
    [string] $inputPath,
    [System.Collections.Generic.List[string]] $issues
) {
    $archive = [System.IO.Compression.ZipFile]::OpenRead($inputPath)
    try {
        foreach ($entry in $archive.Entries) {
            if ($entry.Length -eq 0 -or $entry.Length -gt 2MB) {
                continue
            }

            $text = Read-ZipEntryText $entry
            if ($text -match "[A-Za-z]:\\Users\\" -or
                $text -match "file:///[A-Za-z]:/Users/" -or
                $text -match "x15ac:absPath" -or
                $text -match "\babsPath\b") {
                Add-Issue $issues "Case '$caseId' contains local path metadata in '$($entry.FullName)'."
            }

            if ($text -match "Joannes|Vermorel") {
                Add-Issue $issues "Case '$caseId' contains personal author metadata in '$($entry.FullName)'."
            }

            if ($text -match "(?i)\bconfidential\b|\bcustomer\b|\bprivate document\b") {
                Add-Issue $issues "Case '$caseId' contains private-looking text in '$($entry.FullName)'."
            }

            if ($entry.FullName -eq "docProps/core.xml") {
                $creatorMatch = [regex]::Match($text, "<dc:creator>(?<value>[^<]*)</dc:creator>")
                if ($creatorMatch.Success -and $creatorMatch.Groups["value"].Value -ne $publicCreator) {
                    Add-Issue $issues "Case '$caseId' core creator must be '$publicCreator'."
                }

                $lastModifiedMatch = [regex]::Match($text, "<cp:lastModifiedBy>(?<value>[^<]*)</cp:lastModifiedBy>")
                if ($lastModifiedMatch.Success -and $lastModifiedMatch.Groups["value"].Value -ne $publicCreator) {
                    Add-Issue $issues "Case '$caseId' core lastModifiedBy must be '$publicCreator'."
                }
            }

            if ($entry.FullName -like "xl/comments*.xml") {
                foreach ($match in [regex]::Matches($text, "<author>(?<value>[^<]*)</author>")) {
                    $author = $match.Groups["value"].Value
                    if ($author -ne $publicCommentAuthor -and $author -ne $publicCreator) {
                        Add-Issue $issues "Case '$caseId' comment author '$author' is not public-normalized."
                    }
                }
            }
        }
    }
    catch {
        Add-Issue $issues "Case '$caseId' is not a readable OOXML ZIP package. $($_.Exception.Message)"
    }
    finally {
        $archive.Dispose()
    }
}

function Get-WorkbookEntries([string] $inputPath) {
    $archive = [System.IO.Compression.ZipFile]::OpenRead($inputPath)
    try {
        return @($archive.Entries | ForEach-Object { $_.FullName })
    }
    finally {
        $archive.Dispose()
    }
}

$issues = [System.Collections.Generic.List[string]]::new()
if (-not (Test-Path -LiteralPath $caseRoot)) {
    Add-Issue $issues "Missing openpyxl fixture case root: $caseRoot"
}

if (-not (Test-Path -LiteralPath $familyRoot)) {
    Add-Issue $issues "Missing openpyxl fixture family root: $familyRoot"
}

$caseById = @{}
$caseDirectories = @(Get-ChildItem -LiteralPath $caseRoot -Directory -ErrorAction SilentlyContinue | Sort-Object Name)
foreach ($caseDirectory in $caseDirectories) {
    $manifestPath = Join-Path $caseDirectory.FullName "case.json"
    if (-not (Test-Path -LiteralPath $manifestPath)) {
        Add-Issue $issues "Case '$($caseDirectory.Name)' is missing case.json."
        continue
    }

    $manifest = Read-JsonFile $manifestPath
    if ([string]::IsNullOrWhiteSpace($manifest.id)) {
        Add-Issue $issues "Case '$($caseDirectory.Name)' has no id."
        continue
    }

    if ($manifest.id -ne $caseDirectory.Name) {
        Add-Issue $issues "Case '$($caseDirectory.Name)' has mismatched id '$($manifest.id)'."
    }

    if ($manifest.id -notmatch $idPattern) {
        Add-Issue $issues "Case '$($manifest.id)' is not normalized kebab-case."
    }

    if ($caseById.ContainsKey($manifest.id)) {
        Add-Issue $issues "Duplicate case id '$($manifest.id)'."
        continue
    }

    if ($manifest.kind -notin @("xlsx", "xlsm")) {
        Add-Issue $issues "Case '$($manifest.id)' has unsupported kind '$($manifest.kind)'."
    }

    foreach ($required in @("input", "producer", "producerVersion", "createdByTool", "sanitizedByTool")) {
        if ([string]::IsNullOrWhiteSpace($manifest.$required)) {
            Add-Issue $issues "Case '$($manifest.id)' is missing required field '$required'."
        }
    }

    foreach ($arrayField in @("tags", "features", "expectedParts", "allowedUnsupportedFeatures")) {
        if ($null -eq $manifest.$arrayField) {
            Add-Issue $issues "Case '$($manifest.id)' is missing array field '$arrayField'."
        }
    }

    if ($null -eq $manifest.expectedObjectModel) {
        Add-Issue $issues "Case '$($manifest.id)' has no expectedObjectModel object."
    }

    foreach ($tag in @($manifest.tags)) {
        if ($tag -is [string] -and $tag.Length -ne 0 -and $tag -notmatch $idPattern) {
            Add-Issue $issues "Case '$($manifest.id)' tag '$tag' is not normalized kebab-case."
        }
    }

    $caseTags = @($manifest.tags)
    $caseFeatures = @($manifest.features)
    $isComposition = $caseTags -contains "composition"
    if ($isComposition -and $caseTags -contains "focused") {
        Add-Issue $issues "Case '$($manifest.id)' cannot be both focused and composition."
    }

    if (-not $isComposition -and $caseFeatures.Count -gt 8) {
        Add-Issue $issues "Case '$($manifest.id)' has $($caseFeatures.Count) features; split focused fixtures or tag it as composition."
    }

    $inputPath = Join-Path $caseDirectory.FullName ([string]$manifest.input)
    if (-not (Test-Path -LiteralPath $inputPath)) {
        Add-Issue $issues "Case '$($manifest.id)' input does not exist: $($manifest.input)."
    }
    else {
        $expectedExtension = "." + [string]$manifest.kind
        if ([System.IO.Path]::GetExtension($inputPath).ToLowerInvariant() -ne $expectedExtension) {
            Add-Issue $issues "Case '$($manifest.id)' input extension does not match kind '$($manifest.kind)'."
        }

        $entries = @(Get-WorkbookEntries $inputPath)
        foreach ($part in @($manifest.expectedParts)) {
            if ($entries -notcontains $part) {
                Add-Issue $issues "Case '$($manifest.id)' expected package part is missing: $part"
            }
        }

        Validate-WorkbookHygiene $manifest.id $inputPath $issues
    }

    $caseById[$manifest.id] = [pscustomobject]@{
        Id = [string]$manifest.id
        Directory = $caseDirectory.FullName
        Manifest = $manifest
        InputPath = $inputPath
    }
}

$trackedInputs = @(Get-ChildItem -LiteralPath $caseRoot -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Extension -in @(".xlsx", ".xlsm") })
foreach ($input in $trackedInputs) {
    $manifestPath = Join-Path $input.DirectoryName "case.json"
    if (-not (Test-Path -LiteralPath $manifestPath)) {
        Add-Issue $issues "Workbook input is not next to a case manifest: $($input.FullName)"
    }
}

$rootInputs = @(Get-ChildItem -LiteralPath $FixtureRoot -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Extension -in @(".xlsx", ".xlsm") })
foreach ($input in $rootInputs) {
    Add-Issue $issues "Workbook input must live under cases/<id>/input.* instead of fixture root: $($input.Name)"
}

$coverage = @{}
$familyFiles = @(Get-ChildItem -LiteralPath $familyRoot -Filter "*.json" -File -ErrorAction SilentlyContinue | Sort-Object Name)
foreach ($familyFile in $familyFiles) {
    $family = Read-JsonFile $familyFile.FullName
    if ([string]::IsNullOrWhiteSpace($family.id)) {
        Add-Issue $issues "Family file '$($familyFile.Name)' has no id."
        continue
    }

    if ($familyFile.BaseName -ne $family.id) {
        Add-Issue $issues "Family file '$($familyFile.Name)' has mismatched id '$($family.id)'."
    }

    if ($family.id -notmatch $idPattern) {
        Add-Issue $issues "Family '$($family.id)' is not normalized kebab-case."
    }

    $caseIds = @($family.caseIds | Where-Object { $_ -is [string] -and $_.Length -ne 0 })
    if ($caseIds.Count -eq 0 -and $family.allowEmpty -ne $true) {
        Add-Issue $issues "Family '$($family.id)' has no caseIds and is not marked allowEmpty."
    }

    $seen = @{}
    $focusedCount = 0
    foreach ($caseId in $caseIds) {
        if ($seen.ContainsKey($caseId)) {
            Add-Issue $issues "Family '$($family.id)' lists case '$caseId' more than once."
            continue
        }

        $seen[$caseId] = $true
        if (-not $caseById.ContainsKey($caseId)) {
            Add-Issue $issues "Family '$($family.id)' references unknown case '$caseId'."
            continue
        }

        if (@($caseById[$caseId].Manifest.tags) -notcontains "composition") {
            $focusedCount++
        }

        if (-not $coverage.ContainsKey($caseId)) {
            $coverage[$caseId] = [System.Collections.Generic.List[string]]::new()
        }

        $coverage[$caseId].Add([string]$family.id) | Out-Null
    }

    if ($caseIds.Count -gt 0 -and $focusedCount -eq 0) {
        Add-Issue $issues "Family '$($family.id)' must include at least one focused case, not only composition fixtures."
    }
}

foreach ($caseId in $caseById.Keys) {
    if (-not $coverage.ContainsKey($caseId) -and -not $AllowUncovered) {
        Add-Issue $issues "Case '$caseId' is not covered by any openpyxl fixture family."
    }
}

$testFile = Join-Path $repoRoot "tests/Lokad.Lython.Tests/OpenPyxlModuleFunctionTests.cs"
if (Test-Path -LiteralPath $testFile) {
    $testText = Get-Content -Raw -LiteralPath $testFile
    foreach ($match in [regex]::Matches($testText, 'FixtureBytes\("(?<name>[^"]+)"\)')) {
        $name = $match.Groups["name"].Value
        $caseId = [System.IO.Path]::GetFileNameWithoutExtension($name)
        if (-not $caseById.ContainsKey($caseId)) {
            Add-Issue $issues "OpenPyxlModuleFunctionTests references missing fixture '$name'."
        }
    }
}

if ($issues.Count -ne 0) {
    $issues | ForEach-Object { Write-Error $_ }
    throw "OpenPyxl fixture validation failed with $($issues.Count) issue(s)."
}

Write-Host ("Validated {0} openpyxl fixture cases across {1} families." -f $caseById.Count, $familyFiles.Count)
