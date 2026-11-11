# Native storage and call inputs — 2026-11-12

Retained for delivery qualification: the exact seven-file candidate over
`9b7ccda9`, whose production source is fully qualified `8f8628a4`. Frozen source
archives and loaded hashes identify the measured code; assembly revision metadata
precedes its delivery commit. Full platform/stress/installed-consumer checks and
the next external CPython checkpoint are separate. Publication remains postponed.

Two physical stages disappear. Eligible private histogram segments probe mapped
UTF-8 without constructing every duplicate short-word object, while inserted keys
and observable bindings retain independent immutable values. Captured native
helpers borrow actual parameter storage and the definition's fixed collection of
live cells through one readonly stack view. The second packing buffer, copy/clear
loops, appended-cell index convention and combined capacity subtraction are gone.
The real keyword/default binder keeps its necessary parameter buffer. No new owner,
cache, controller, universal return ABI or public API change is introduced.

Existing eager admission still funds every eventual word. Allocation reduction
does not imply smaller reservations, physical-heap bounds or embedding latency.
Managed stack protection, host mediation, active cancellation, preflights, first
terminal refusal, protocols, retained aliases and joined cleanup remain intact.

## Complete source-pair observations

Text allocation falls **20.45% Run / 20.43% RunAsync** for captured helpers and
**22.44% / 22.42%** for ordinary helpers. Other/compile allocation is effectively
flat. The same six complete scripts, fixed desktop worker/adapter, shared CLR/GC
and hashing, 256 warmups and five rotating rounds of sixteen invocations pass
all output/file/balance/cleanup gates. Both APIs and compilation are separate.
Desktop timing remains diagnostic; CSV Run +14.01..+20.83% and ordinary RunAsync
+3.42..+3.82% with 0.39% unchanged control are preserved, not omitted.

B597 reuses the existing F4as_v7 Ubuntu/.NET11RC3 quiet VM and fixed `fec79`
worker/adapter 0.4. Ten short jobs finish in **279.026 seconds** with two independent
processes per image, five-second warmups and seven rotated 150 ms rounds.

| Complete script / API | Elapsed change | Wider process CPU change |
| --- | ---: | ---: |
| Captured text / Run | −8.74..−4.66% | −9.21..−4.33% |
| Captured text / RunAsync | −6.81..−3.80% | −7.29..+1.63% |
| Ordinary text / Run | −9.68..−8.54% | −11.54..−8.53% |
| Ordinary text / RunAsync | −12.48..−9.49% | −11.90..−9.68% |
| Search / Run | −1.42..+3.62% | −1.94..+5.21% |
| Search / RunAsync | −4.44..−0.30% | −4.60..+0.78% |
| CSV / Run | −4.95..+0.13% | −2.94..0% |
| CSV / RunAsync | −3.82..−0.32% | −3.23% |
| JSON-lines / Run | −0.86..+1.53% | −2.27..0% |
| Report / RunAsync | −2.79..+1.98% | 0..+6.25% |

Negative means less time. Ranges cover four independent process-median pairings,
not confidence intervals. Captured Run and both ordinary APIs have supporting
CPU improvement. Captured async CPU remains uncertain. Earlier R250 async-search
and CSV adversity is not reproduced here; that does not identify its cause or
attribute a recovery solely to the input change. Search/JSON/report are variable;
the [summary](quiet/summary.json) preserves all control spreads and IQRs.

Report async's wider CPU 0..+6.25% is **additional uncertainty versus `8f8628a4`**,
whose earlier report debt has its own reference. Current elapsed overlaps zero,
with 3.04/1.77% controls and max IQR 5.05%; CPU includes protocol/reset and uses
process tick sampling. This does not establish neutrality or a new elapsed
regression. Accept that uncertainty at this milestone, preserve it for later
representative profiling, and do not compound or equate the two CPU ranges.

Warm VM clocks include mediated RAM-file I/O, full output/file validation and
prior-result cleanup. Startup, compilation, fixture reset/staging, protocol and
final caches are excluded; external process CPU also includes protocol/reset.
Desktop clocks include fixture reset and immediate result disposal. Do not merge
the clocks, infer CPU from allocation or multiply source-pair gains into new
CPython ratios. These are combined changes versus the qualified reference,
not an isolated R251 effect.

## Correctness and decision

Focused Release checks pass **1,642 internal and 3,810 public cases per actual
.NET11/10**, plus seven cell/reentry checks per target. They preserve missing
parameters before cell writes, live cells and rebinding, defaults/keywords/wide
calls, Unicode, identity, current binding publication, integer promotion, actual
protocol suspension, cancellation, first refused admission, retained results,
joined cleanup and budget reuse. Reentry also changes caller locals after a native
cell effect while already-bound positional/keyword inputs keep their old values.
Existing independent Python goldens remain applicable; unchanged references are
reused rather than retimed. This does not replace full delivery qualification.

The [initial R250 source pair](../2026-11-11-native-word-probing/README.md) retains
all earlier gains/adversity. Its bounded paired profile, actual maps and
generated-name-normalized IL inventory do not establish a JIT cause; further
unchanged profiling was stopped. This candidate removes actual storage/transport
stages and has useful complete-text CPU/allocation evidence, so retain it with
the explicit report uncertainty above. No fixed improvement cutoff decided it.

All VM identity/output/file/allowance/quiet/join/RAM/archive gates pass. Independent
retrieval verifies the archive. B597 is terminal/cached with no remaining users or
RAM fixtures. Only superseded R250 core aliases and terminal B596 raw/catalog
duplicates were retired after fresh path/UID/inode/all-alias proof against exact
committed Git bytes. Qualified reference and selected candidate remain protected.
Source/code, adverse desktop phases and new regression source are preserved here.

[Decision](decision.json), [source/core identity](diagnostic-identity.json),
[exact candidate sources](sources-candidate.tar.xz),
[raw quiet observations](quiet/observations.tgz), [focused checks](focused-checks.tar.xz)
and [current external account](../../../docs/BENCHMARKS.md) record distinct claims.
