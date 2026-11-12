# Current CSV, JSON and guarded-search CPython checkpoint

2026-11-12, retained `da9e5066`, frozen a55202fb with precommit assembly metadata.
All 568 production sources match the exact fully qualified revision; later
34f2f325 changes evidence only. Fixed fec79 worker/optional nonpackable adapter0.4,
stock CPython3.13.16, quiet Azure F4as_v7 / Ubuntu24.04.4 / actual .NET11RC3,
ordinary GC/tiering. [Full delivery](../2026-11-12-conditional-helper-bodies/delivery/README.md)
passes Windows/Ubuntu, actual .NET11/10, original stress and installed consumers.

Ten jobs complete in 301.542 seconds: CSV validation, JSON-lines helpers and
guarded search, each with separate compile/warm/compile-to-result, plus captured
text warm control. Two independent processes per engine, seven rotating150ms
rounds, 5s warmups except captured text12s. Same scripts/fixtures on both engines.
All actual-image, stdout/files, allowance, quiet/join/RAM/persistent archival gates
pass; local SHA verifies the archive. Terminal cached; no remaining users.

| Script | Warm Lython / CPython | Compile-to-result | Limits |
| --- | --- | --- | --- |
| CSV validation | 1.03–1.04× | 1.05–1.09× | Warm IQR3.28%; result Lython process spread2.88% |
| JSON-lines | 0.85–0.91× | 0.85–0.87× | Warm Lython spread5.82%; compilation IQR3.50% |
| Guarded search | 1.81–1.99× | 1.61–1.78× | Warm spreads L5.82%/P3.55%; result L8.02%; compile IQR5.44% |
| Captured text | 1.65–1.67× | Not refreshed | This absolute control is stable; it does not resolve causality or disprove original source-pair adversity |

Ratios span all four process-median pairings, not confidence intervals. Separate
source pairs are not compounded into these ratios. Captured-text compile/result
and other unrefreshed guide rows retain their original source/date labels.

Warm/result clocks include public invocation, mediated RAM I/O, complete stdout/
file validation and prior-result cleanup; exclude startup, compile in warm,
fixture staging/reset, protocol and final cache disposal. Compilation includes
construction/disposal under the shared allowance. Wider process CPU includes
reset/protocol. No RSS, pending-I/O, embedding-tail or large-file claim.

Guarded search and captured text retain material gaps, reports retain their
separately measured gap, and CSV/JSON no longer justify broad collection rewrites
from old timings. The [bounded operand source proof](operand-proof.md) records
real transport/effect-order obstacles; no duplicate frame or speculative new
dispatcher, arena, proof scratch vector or tagged-value layer is introduced.
This checkpoint guides the continuing roadmap, not completion of the objective.

First staging refused before mutation: available256659456 plus exact reclaim
3256320 was below required264699904. Add only exact committed B615 raw profile/
map/stack/JIT duplicates, preserving the initial condition and unchanged floors.
Fresh lease, UID/resolved-path/inode/link-count and canonical Git/SHA proofs
precede removal. A unique POSIX link already proves the complete alias set;
multi-link images still require enumeration. Current/reference cores stay
protected. Final archive leaves262340608 bytes free, above the240MiB OS floor.
Publication/tagging and final released-adapter qualification remain postponed.
