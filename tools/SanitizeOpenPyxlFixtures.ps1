<#
Intent: Rewrite openpyxl fixture workbooks to remove local paths, personal metadata, and unstable timestamps.
Usage: powershell -ExecutionPolicy Bypass -File tools/SanitizeOpenPyxlFixtures.ps1 [-FixtureRoot path] [-WhatIf]
#>

param(
    [string] $FixtureRoot = "",
    [switch] $WhatIf
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($FixtureRoot)) {
    $FixtureRoot = Join-Path $repoRoot "tests/Fixtures/openpyxl"
}

$caseRoot = Join-Path $FixtureRoot "cases"
$publicCreator = "Lython Fixture Generator"
$publicCommentAuthor = "Fixture Author"
$stableTimestamp = "2026-06-08T00:00:00Z"

Add-Type -AssemblyName System.IO.Compression.FileSystem

function Convert-XmlText([string] $path, [string] $text) {
    $options = [System.Text.RegularExpressions.RegexOptions]::Singleline
    $text = [regex]::Replace(
        $text,
        '<mc:AlternateContent\b(?:(?!</mc:AlternateContent>).)*\babsPath\b(?:(?!</mc:AlternateContent>).)*</mc:AlternateContent>',
        '',
        $options)
    $text = [regex]::Replace($text, '<[^>]*\babsPath\b[^>]*/>', '', $options)
    $text = [regex]::Replace($text, '<dc:creator>.*?</dc:creator>', "<dc:creator>$publicCreator</dc:creator>", $options)
    $text = [regex]::Replace($text, '<cp:lastModifiedBy>.*?</cp:lastModifiedBy>', "<cp:lastModifiedBy>$publicCreator</cp:lastModifiedBy>", $options)
    $text = [regex]::Replace(
        $text,
        '<dcterms:created[^>]*>.*?</dcterms:created>',
        "<dcterms:created xsi:type=`"dcterms:W3CDTF`">$stableTimestamp</dcterms:created>",
        $options)
    $text = [regex]::Replace(
        $text,
        '<dcterms:modified[^>]*>.*?</dcterms:modified>',
        "<dcterms:modified xsi:type=`"dcterms:W3CDTF`">$stableTimestamp</dcterms:modified>",
        $options)

    if ($path -like "xl/comments*.xml") {
        $text = [regex]::Replace($text, '<author>.*?</author>', "<author>$publicCommentAuthor</author>", $options)
    }

    return $text
}

function Copy-Entry([System.IO.Compression.ZipArchive] $source, [System.IO.Compression.ZipArchive] $target, [System.IO.Compression.ZipArchiveEntry] $entry) {
    $targetEntry = $target.CreateEntry($entry.FullName, [System.IO.Compression.CompressionLevel]::Optimal)
    if ($entry.FullName -like "*.xml" -or $entry.FullName -like "*.rels") {
        $reader = [System.IO.StreamReader]::new($entry.Open())
        try {
            $text = $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }

        $text = Convert-XmlText $entry.FullName $text
        $writer = [System.IO.StreamWriter]::new($targetEntry.Open(), [System.Text.UTF8Encoding]::new($false))
        try {
            $writer.Write($text)
        }
        finally {
            $writer.Dispose()
        }
        return
    }

    $sourceStream = $entry.Open()
    $targetStream = $targetEntry.Open()
    try {
        $sourceStream.CopyTo($targetStream)
    }
    finally {
        $targetStream.Dispose()
        $sourceStream.Dispose()
    }
}

$inputs = @(Get-ChildItem -LiteralPath $caseRoot -Recurse -File |
    Where-Object { $_.Extension -in @(".xlsx", ".xlsm") } |
    Sort-Object FullName)
foreach ($input in $inputs) {
    $temporaryPath = $input.FullName + ".tmp"
    if (Test-Path -LiteralPath $temporaryPath) {
        Remove-Item -LiteralPath $temporaryPath -Force
    }

    $source = [System.IO.Compression.ZipFile]::OpenRead($input.FullName)
    $target = [System.IO.Compression.ZipFile]::Open($temporaryPath, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($entry in $source.Entries) {
            Copy-Entry $source $target $entry
        }
    }
    finally {
        $target.Dispose()
        $source.Dispose()
    }

    if ($WhatIf) {
        Remove-Item -LiteralPath $temporaryPath -Force
        Write-Host "Would sanitize $($input.FullName)"
        continue
    }

    Move-Item -LiteralPath $temporaryPath -Destination $input.FullName -Force
    Write-Host "Sanitized $($input.FullName)"
}
