# Returned-record CSV external baseline — R73/B429

2026-11-08. Source **68d5bffe**, core **a0aef9ef** matches the recorded production
inventory; informational SourceLink is precommit a34f56b5. Focused 1,729 internal /
597 public checks per actual .NET 11/10 passed. Combined full Windows/Ubuntu,
original Stress and installed-core delivery CI42428096630 remains pending. This
measurement does not replace that qualification. Publication postponed.

Only five jobs fill a material gap: maintained CSV validation/helper keyword calls/
early exits/tuple results/aggregation/report, separately compiled, warm and compiled
plus executed once. Current ordinary CSV/text warm controls complete the set.
**5,120 records / 68,625 input bytes**, complete independently specified stdout/file
goldens checked on every invocation. This is the same script used for R69–R72,
not a microkernel. Existing broader R67 rows stay valid for their named source;
[prior guide](prior-BENCHMARKS.md) preserves the full source824fc6bb tables.

**Five jobs complete in 107.441 seconds**. CPython3.13.16, normal GIL/GC/no JIT/debug,
GCC13.3 PGO/LTO, isolated `-I -S -X utf8`; .NET11RC3 normal GC/tiering. Same quiet
Azure F4as_v7/Ubuntu24.04.4, four EPYC9V45 vCPUs,16GiB/no swap. Two independent
identical-build Lython processes and one CPython process per job; five-second
warmup per role, seven rotated150ms rounds. No new framework or old full replay.

| Complete job | Lython | CPython | Lython / CPython | Status; control / max IQR |
| --- | ---: | ---: | ---: | --- |
| csv-validated-records.large / compile | 70.400–72.059 µs | 65.559 µs | 1.07–1.10× | Variable; 2.36% / 4.59% |
| csv-validated-records.large / warm | 5.681–5.817 ms | 4.320 ms | 1.32–1.35× | Variable; 2.32% / 1.07% |
| csv-validated-records.large / compile-run | 5.913–6.073 ms | 4.527 ms | 1.31–1.34× | Variable; 2.70% / 1.08% |
| csv-summary-functions.large / warm | 3.737–3.817 ms | 3.209 ms | 1.16–1.19× | Variable; 2.14% / 1.21% |
| text-summary-helpers.large / warm | 1.939–1.950 ms | 1.163 ms | 1.67–1.68× | Diagnostic; 0.60% / 1.49% |

Ratio is elapsed Lython/CPython; greater than one is slower. Ranges span two Lython
process medians, not confidence intervals. Control is their absolute difference /
candidate; IQR is the largest role IQR/median including CPython. Variable labels
the 2.1–2.7% control differences or compilation IQR4.59%; it is uncertainty, not a
design veto or discarded result. New returned-record execution remains about
1.32–1.35× CPython, compile-to-result1.31–1.34×. Ordinary CSV1.16–1.19× is variable;
text1.67–1.68× is diagnostic. Substantial text and call/record costs remain despite
retained allocation/route improvements. These are not additive source-pair gains,
proof that one subsystem causes the gap, or whole-objective completion.

## Included work and limits

Warm reuses the compiled script and includes invocation, mediated RAM-file I/O,
full validation and prior-result cleanup. Compile includes construction/disposal
under shared admission. Compile-to-result includes both plus execution cleanup.
Startup, staging/reset, protocol transport and final cached teardown excluded.
The inherited identity.included field retains its old warm-only campaign caption;
the explicit five job lanes and actual command-v2 Ready boundaries govern this
collection. Raw input is preserved rather than silently edited.

Efficient host overrides and contained capability/path-component checks remain
inside Lython's timed I/O; CPython uses ordinary builtin open and resident imports,
where Lython isolates execution imports. RAM fixtures measure mediated file access,
not physical-storage throughput. All reported execution is synchronous Run.
No startup, RSS/heap, genuinely pending-I/O or embedding-tail claim. Historical
adverse source-pair CPU/text observations are not explained or repaid by these
independent ratios. Original variable/adverse observations remain visible.

All exact source/core/auxiliary/actualReady/reference/runtime/golden/quiet/Closed/
exit/join/empty-fixture gates pass. Every user joins and owned128MiB RAM fixtures
are unmounted before archival verification under lease and independently locally.
Persistent records/workers remain outside RAM. Staging274,800,640B passes256MiB;
the conditional retired-prototype reclaim list is empty (no extra files removed).
150s owner +30s stop +30s margin; no timeout, restart or further terminal query.
B429 is terminal/archived/verified. Only affected final adapter qualification is
deferred; the optional0.4adapter/worker are unchanged and this is no backend claim.

[Summary](summary.json), [exact identities](identity.json), [focused checks](focused.json),
[contract](contract.md), [original raw archive](observations.tgz),
[archive receipt](archive-receipt.json), [collection inputs](collection-inputs.tar.xz).
