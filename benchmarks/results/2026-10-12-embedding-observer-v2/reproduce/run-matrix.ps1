param([Parameter(Mandatory)][string]$FixtureRoot, [Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
$stage = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $stage) { throw 'Preserve existing pilot' }
New-Item -ItemType Directory -Path $stage | Out-Null
$candidate = Join-Path $root 'benchmarks/results/2026-10-12-embedding-observer-v2/reproduce'
$binaryRoot = Join-Path $candidate 'bin/Release/net10.0'
$fixtures = [IO.Path]::GetFullPath($FixtureRoot)
$scripts = Join-Path $root 'benchmarks/scripts'
$identityPaths = @((Join-Path $candidate 'Program.cs'), (Join-Path $candidate 'Probe.csproj'))
$identityPaths += @(Get-ChildItem -LiteralPath $binaryRoot -File | Where-Object Extension -In '.dll','.json' | Select-Object -ExpandProperty FullName)
$identityPaths += @((Join-Path $root 'tests/Lokad.Lython.Tests/Harness/MockLythonHost.cs'), (Join-Path $root 'tests/Lokad.Lython.Tests/Harness/DelayedLythonHost.cs'))
$identities = @($identityPaths | ForEach-Object { [ordered]@{ path = [IO.Path]::GetRelativePath($root, $_).Replace('\','/'); sha256 = (Get-FileHash -LiteralPath $_).Hash } })
$manifest = [ordered]@{ source = (git rev-parse HEAD); production = (git rev-parse HEAD:src/Lokad.Lython); machine = [Environment]::MachineName; collectedUtc = [DateTimeOffset]::UtcNow.ToString('o'); qualification = 'shared-Windows probe feasibility only'; identities = $identities }
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $stage 'manifest.json') -Encoding utf8
$rows = @()
$helper = Join-Path $PSScriptRoot 'run-owned-command.ps1'
$helperRoot = $stage
$dotnet = (Get-Command dotnet).Source
$cases = @('control','csv-summary-async','file-manifest-async')
for ($session = 1; $session -le 3; $session++) {
    $order = @($cases)
    if ($session -eq 2) { [Array]::Reverse($order) }
    foreach ($name in $order) {
        $nameStage = "observer-s$session-$name"
        $errorText = $null
        try {
            & $helper -Stage (Join-Path $stage $nameStage) -Executable $dotnet -Arguments @((Join-Path $binaryRoot 'Probe.dll'), $name, $fixtures, $scripts) *> (Join-Path $stage "$nameStage.log")
        } catch { $errorText = $_.Exception.Message }
        $receiptDirectory = Join-Path $helperRoot $nameStage
        $receipt = Get-Content -LiteralPath (Join-Path $receiptDirectory 'process.receipt.json') -Raw | ConvertFrom-Json
        $row = [ordered]@{ session = $session; name = $name; receiptDirectory = [IO.Path]::GetRelativePath($root, $receiptDirectory).Replace('\','/'); process = $receipt; passed = $false; exclusion = $errorText }
        if (-not $errorText) {
            $data = Get-Content -LiteralPath (Join-Path $receiptDirectory 'stdout.txt') -Raw | ConvertFrom-Json
            if ($data.policy -ne 'embedding-v2-windows-feasibility' -or $data.samples -lt 1000 -or $data.hostGenerationsBefore[2] -ne $data.modeledHostObjects -or $data.peakBytes -gt 262144 -or ($data.asynchronous -and ($data.yieldedJobs -ne $data.completed -or $data.delayedHostCalls -lt 1))) { throw 'Successful probe violated frozen pilot gates' }
            $row.passed = $true
            $row.samples = $data.samples; $row.completed = $data.completed; $row.yieldedJobs = $data.yieldedJobs
            $row.elapsed = $data.observationSeconds; $row.hostGenerationsBefore = $data.hostGenerationsBefore
            $row.preparationCollections = $data.preparationCollections; $row.measuredCollections = $data.gcCollections
            $row.peakBytes = $data.peakBytes; $row.cpuCoreFraction = $data.cpuCoreFraction
            $row.subjectSha256 = $data.subjectDllSha256
        }
        $rows += $row
        $rows | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $stage 'progress.json') -Encoding utf8
        [ordered]@{ session=$session; name=$name; passed=$row.passed; samples=$row.samples; completed=$row.completed; elapsedIncludingCleanup=$receipt.elapsedMillisecondsIncludingCleanup; exclusion=$row.exclusion } | ConvertTo-Json -Compress
    }
}
foreach ($identity in $identities) {
    if ((Get-FileHash -LiteralPath (Join-Path $root $identity.path)).Hash -ne $identity.sha256) { throw 'Frozen producer changed' }
}
[ordered]@{ qualification='feasibility only; no quiet-machine eligibility'; rows=$rows; allChildrenExited=(@($rows | Where-Object { -not $_.process.hasExited }).Count -eq 0) } | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath (Join-Path $stage 'summary.json') -Encoding utf8
