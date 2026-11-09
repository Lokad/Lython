# Coherent CPython checkpoint for6d73

R136/B496, 2026-11-09. Committed6d73c742, fresh post-commit Release coreea65554e,
CPython3.13.16, quiet AzureF4as_v7/Ubuntu24.04.4/.NET11RC3. Same original complete
validatedCSV/text/report scripts, external catalog, RAM-backed inputs/output goldens
and command-v2 timing boundary. Nine jobs188.968s, normalGC/tiering, three independent
process roles (two Lython/Python),5swarmup perrole/job,7rotated150msrounds.

| Script / boundary | Lython/CPython | Status |
| --- | ---: | --- |
| scripts.csv-validated-records.large /compile | 1.05–1.08× | Variable; control2.98%, maxIQR4.82% |
| scripts.csv-validated-records.large /warm | 1.16–1.18× | Diagnostic; control1.39%, maxIQR2.02% |
| scripts.csv-validated-records.large /compile-run | 1.15–1.16× | Diagnostic; control1.46%, maxIQR0.83% |
| scripts.text-summary-helpers.large /compile | 1.05–1.07× | Variable; control1.47%, maxIQR4.31% |
| scripts.text-summary-helpers.large /warm | 1.55–1.57× | Diagnostic; control1.42%, maxIQR0.76% |
| scripts.text-summary-helpers.large /compile-run | 1.44–1.45× | Diagnostic; control0.67%, maxIQR0.66% |
| scripts.report-render.large /compile | 1.08–1.08× | Variable; control0.07%, maxIQR4.20% |
| scripts.report-render.large /warm | 1.38–1.40× | Variable; control1.53%, maxIQR3.65% |
| scripts.report-render.large /compile-run | 1.35–1.38× | Variable; control2.29%, maxIQR3.77% |

Ratios use elapsed execution clocks, not wider processCPU. Above1 means Lython
slower; process ranges are not confidence intervals. Variable compile/report rows
stay visible. This refresh does not attribute changes to individual optimizations
or compare arbitrary newer HEAD. Both frameworks' focused semantic/cancellation/
ownership checks pass. Full Ubuntu delivery checks pass. Windows failed the
existing native-hash cancellation test at its pre-cancellation IsCompleted
assertion; later stages were skipped. That qualification gap is distinct from
these measurements. See [cached delivery observation](delivery-ci.json).

Compile includes code construction/disposal. Warm reuses compilation. Complete
includes compilation+one execution. Guest mediated RAM-file reads/writes, full stdout/
file validation and prior-result cleanup are inside warm/complete clocks; reset/
staging/protocol/startup/final cache teardown are outside. CPython retains imported
modules; Lython isolates each execution. No startup/RSS/pending-I/O/embedding-tail
measurement, source-pair CPU attribution or regex qualification.

Explicit nonincremental post-commit build uses the pinned SDK. All24818 runtime IL
bodies/signatures/implementation policies match the focused Boolean candidate76d;
assembly/PDB metadata differs and IL identity is not JIT-code identity. Fresh worker
22331 replaces prior7cf; worker source changes from3604 only default catalog
construction. The supplied external catalog, host, protocol and execution boundary
are unchanged. The added validatedCSV default fixture equals the actual external
source; every executed source/input/output hash is independently gated.

All current core/auxiliary identities, quiet/output/ownership/Closed/join/RAM-unmount/
archive-under-lease/local-SHA gates pass. Archive
dc0cbcd05f63b1e90338da00df955a336a61e16d387dd96ddf575bb4f263c563.
Terminal cached, no users/fixtures. Fresh idle exact-byte deduplication recovered
14,020,608B from two Roslyn duplicates; all original paths/bytes/inode aliases/
ownership/modes/writable-parent proofs and pre-mutation plan are retained.
Floors/headroom unchanged; worker/catalog/evidence remain persistent outside RAM.

The guide updates only these three main cases. Earlier459 config/search/two-file
manifest and6f76 ordinaryCSV/JSON rows remain historical; prior displaced rows stay
in the immutable guide snapshot. No unchanged main-five qualification or new
benchmark framework. Publication postponed and broader performance work remains open.

[Summary/all medians/variability](summary.json), [exact identity](identity.json),
[fresh runtime IL equivalence](fresh-il-proof.json), [worker source diff](worker-source-diff.patch),
[fresh worker file differences](fresh-worker-differences.json), [core source](core-source.json),
[archive receipt](archive-receipt.json), [original raw/current cores/worker updates](observations.tgz),
[selection](selection.md), [all commands/build logs/proofs](preparation.tar.xz),
[space proof](space.json), [previous maintained guide](prior-BENCHMARKS.md).
