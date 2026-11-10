# Combined R184/R185 CPython checkpoint

2026-11-10. Qualified production3a413c59-equivalent, core26d43e66 from the existing
precommit nonincremental freeze. Not a postcommit rebuild or arbitrary laterHEAD.
All564 production sources match the qualified revision after explicit Git checkout
EOL normalization. Exact frozen hashes and [equivalence proof](source-equivalence.json)
are retained. Full Windows/Ubuntu, actual11/10, original Stress and installed Release
consumers are [GREEN](delivery-ci-status.json); publication remains postponed.

QuietF4as_v7, Ubuntu24.04.4, .NET11RC3, stock isolatedCPython3.13.16. Four processes,
two per engine;5swarmup/role,7rotated150msrounds, ordinaryGC/tiering. Ninejobs:
compile/warm/compile-to-result on original CSV/text and useful keyword-default file
search. Its input/output match original search, but sourceSHA differs: it is a
separate workload, not a replacement observation for the original literal-prefix case.

| Complete script | Lane | Lython ms | CPython ms | Ratio | Status |
| --- | --- | ---: | ---: | ---: | --- |
| scripts.csv-validated-records.large | compile | 0.0695–0.0705 | 0.0652–0.0654 | 1.06–1.08× | Diagnostic |
| scripts.csv-validated-records.large | warm | 4.6903–4.7681 | 4.2773–4.3012 | 1.09–1.11× | Diagnostic |
| scripts.csv-validated-records.large | compile-run | 4.9267–5.0185 | 4.4465–4.4783 | 1.10–1.13× | Diagnostic |
| scripts.text-summary-helpers.large | compile | 0.0449–0.0449 | 0.0415–0.0416 | 1.08–1.08× | Diagnostic |
| scripts.text-summary-helpers.large | warm | 1.9336–1.9779 | 1.1540–1.1774 | 1.64–1.71× | Variable |
| scripts.text-summary-helpers.large | compile-run | 2.0114–2.0886 | 1.2716–1.2811 | 1.57–1.64× | Variable |
| scripts.file-search.large | compile | 0.0301–0.0301 | 0.0322–0.0322 | 0.93–0.94× | Diagnostic |
| scripts.file-search.large | warm | 0.2431–0.2448 | 0.1615–0.1615 | 1.51–1.52× | Diagnostic |
| scripts.file-search.large | compile-run | 0.3269–0.3411 | 0.2150–0.2179 | 1.50–1.59× | Variable |

Ratio is Lython elapsed / CPython elapsed. Ranges span independent process medians;
all four pairings are retained, not confidence intervals. Variable controls remain
visible: text warmLython2.29%/Python1.99%, text completeLython3.84%, search complete
Lython4.35%. CSV/warmsearch are tighter; no equivalence/significance or causal gain
from one change is claimed. Earlier source pairs and all adversity remain valid
historical evidence. Original report/config/literal-search and other older rows
are deliberately not retimed.

Compile includes code construction/disposal, no execution. Warm reuses compiled
code; complete adds compilation. Execution includes mediatedRAMI/O, full stdout/
file checks and prior-result cleanup. Startup, staging/reset/protocol and final
cache teardown are excluded. Wider100Hz processCPU is separate in raw/summary.
Independent phase medians need not add up. Lython host isolation/capability checks
are included; CPython uses builtin files. RSS/tail latency/pendingI/O are unmeasured.

All9jobs finish247.199665119seconds. Identity/output/budget/quiet/join/unmount/
archive-under-owner/localSHA checks pass, no remaining workers/RAMfixtures.
Existing core/worker/catalog images were reused. One redundant B533 compressed
archive was retired only after exact tracked-copy/receipt/hardlink proofs under an
idlelease; raw trees/frozen cores and canonical evidence survive. First local
equivalence script assumed gitarchiveLF; Windows archiveCRLF required explicit
normalization. Original failed assumption/correction is recorded before launch,
with no changed source or favorable observation replacement.

[Summary](summary.json), [owned raw observations and core](observations.tgz),
[commands and setup](development.tar.xz), [previous guide](prior-BENCHMARKS.md).
