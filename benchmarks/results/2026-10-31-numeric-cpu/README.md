# Numeric-report CPU attribution

Source **0268ebff** (production identical to 959df325), 2026-10-31 UTC, dedicated
Ubuntu 24.04.4 Azure F4as_v7, four AMD EPYC 9V45 vCPUs / 16 GiB, .NET 11 RC3.
One fixed capture, two maintained numeric reports, 2,048 warm executions each.
Both quiet-machine gates and complete stdout/file goldens before/after pass.
Workers close and exit zero. No recaptures or additional workload campaign.

## Boundary and limits

The existing [profiler driver](profile.py) uses the maintained comparison worker,
[numeric-report source](../../scripts/numeric-report.py) and frozen independent
file/stdout goldens. The catalog and exact assembly identities are preserved in
[raw-capture.tar.gz](raw-capture.tar.gz). Warmup and settling precede each batch;
compilation/startup are excluded. Fixture reset/staging and goldens remain in the
batch's declared public boundary. The selected dedicated execution thread covers
actual guest/native builtin/host-operation work and its cleanup, not every
main/thread-pool staging or result-disposal cost. Shared-budget reuse is checked
separately by the existing complete-report/public lifetime controls; the CPU
record itself samples no live-balance telemetry.

External perf cpu-clock at 199 Hz with frame-pointer chains, CLR perf maps and
selected optimized JIT listings. Tiered compilation is disabled, matching B10's
fixed comparison, and recorded in each response. Ordinary GC remains. This is
external profiling only: no native probing, unsafe layout, runtime instrumentation
or protection-policy change is introduced. No forced collections.

The [outer collector](collect.sh) holds the exclusive VM lease; the owned service
has a 90-second collection bound and kills/joins its process group on exhaustion.
The two capture windows total **10.66 seconds**, below the 20-second limit.
Guest-thread selection explicitly uses its recorded Lython name, not max CPU.
Offline analysis reuses saved events and does not execute the workloads again.

| Report | Capture wall s | Guest CPU s | Guest samples | Unknown leaves | Empty / single-frame chains |
| --- | ---: | ---: | ---: | ---: | ---: |
| Integer | 5.584 | 2.44 | 484 | 158 (32.6%) | 0 / 0 |
| Float | 5.077 | 1.93 | 380 | 137 (36.1%) | 0 / 0 |

Non-guest thread-pool CPU totals are 0.36 / 0.37 seconds. Wall-minus-guest CPU is
**not** an attributed I/O or GC phase. Unknown native/R2R leaves and incomplete/
inlined managed transitions remain visible. Caller categories can overlap;
counts are approximate inclusive sample proportions, not exact removable cost,
latency shares or speedup predictions.

## Findings

| Inclusive caller category | Integer | Float |
| --- | ---: | ---: |
| JSON operations | 356 / 484 (73.6%) | 299 / 380 (78.7%) |
| JSON array parsing | 319 / 484 (65.9%) | 264 / 380 (69.5%) |
| Number-token/conversion paths | 248 / 484 (51.2%) | 198 / 380 (52.1%) |
| Budget work inside number conversion | 146 / 484 (30.2%) | 0 observed |
| Managed stack/admission frames inside numbers | 10 / 484 (2.1%) | 15 / 380 (3.9%) |
| Sum paths | 74 / 484 (15.3%) | 32 / 380 (8.4%) |

These proportions do not mean array, number and budget costs add. Many unknown
leaves still have informative managed callers. First resolved managed callers
include Reservation.TryResize for 95 integer samples (19.6%) and
ConvertJsonNumberTextAsync for 135 float samples (35.5%). Do not assign every
unknown leaf under conversion to one BCL operation, or every budget sample to
an uncontended lock. Raw chains remain available.

Source inspection identifies one concrete redundant mechanism: callback-free
JSON integer decoding creates GuardIntegerParseBytes's temporary reservation
**before** attempting long.TryParse. The guard reserves a conservative limb
estimate and refunds it even when a short number needs only the already-modeled
small scalar storage. Thus each short integer creates a reservation object and
resizes the shared allowance twice. Floating conversion has no corresponding
limb reservation. This accounts for a concrete source path behind the sampled
budget work, rather than assuming memory accounting globally explains the gap.

The next candidate should couple the limb preflight to actual large-integer
conversion: try a representation-bounded signed-64-bit span parse first, then
retain the original admitted fallback for larger magnitudes. Keep parse hooks
before that choice, their retained text/real suspension, token validation,
managed nesting and cancellation checks. A maximum 20-byte signed-decimal token
is a machine-representation bound, not a guest input/work ceiling; longer tokens
continue to exact parsing. Small values continue to use the existing conservative
scalar/container headroom. No failed admission is retried or bypassed.

Float conversion and remaining Sum work are separate observations; do not turn
this result into a new parser/number framework or retune the closed B10 redesign.
The samples do not justify removing stack admission or checkpoints, merging
lexical/interpreter identities, or adding a universal value representation.

## Evidence and stopping point

Readable [leaves](integer-warm/guest-cpu-leaves.json),
[caller chains](integer-warm/guest-call-chains.json) and
[number-origin categories](integer-warm/number-origins.json) are retained for
both reports. The original source catalog, records, all perf events, JIT maps/
listings, profiler/worker logs, gates and analysis sources are in the raw archive.
[Leaf](analyze-cpu-guest.py), [chain](ops-chains-guest.py) and
[category](number-origins.py) scripts reproduce this one offline derivation.
The explicit malformed-map diagnostics remain; no sample is silently replaced.

Stop B11: the one capture establishes a dominant numeric-decoding component and
a specific removable reservation roundtrip. It supplies no elapsed claim and
does not update docs/BENCHMARKS.md's primary CPython table. PLAN.md records the
next candidate's ownership/semantic acceptance and fixed comparison before code.

## Subsequent offline float call-site attribution (B13)

After B12, one offline pass over these same saved events resolves the float
conversion ambiguity without running another workload. The unique recorded
ConvertJsonNumberTextAsync method has 2,038 code bytes; map/listing sizes agree.
Of 380 guest samples, 135 have this as their first managed Lython frame:

| Sample location | Count |
| --- | ---: |
| Nested under the return IP of System.Number.TryParseFloat<byte,double> | 106 |
| Nested under invariant NumberFormatInfo resolution | 2 |
| Nested under the token's IndexOfAny classification | 2 |
| Direct self IPs in ConvertJsonNumberTextAsync | 25 |

The parser call site accounts for 27.9% of guest samples. It identifies the
actual CLR UTF-8 numeric parser boundary, not an exact internal algorithm or
removable CPU fraction. Native leaves remain unnamed; saved chains can omit
inlined/missing frames. Self IPs include setup and result materialization, not
25 samples proved to be boxing. No unclassified nested return offsets remain.

Source and listing agree that this default conversion uses native UTF-8 and a
completed ValueTask; async callable machinery is on the unused hook branch.
Consequently these samples do not justify another text-conversion fix, removing
stack protection, a broad async rewrite, or a custom floating parser. Different
CLR parsing entry points would need exact rounding/overflow/underflow/signed-zero
acceptance and their own material whole-script evidence. Stop B13 without adding
a new runtime path or retiming B12.

[call-sites.json](float-warm/call-sites.json) retains all counted offsets, and
[float-call-sites.py](float-call-sites.py) performs this deterministic derivation.
Extract the existing raw archive's float-warm folder, decode its saved perf.data
with `perf script -F pid,tid,ip,sym > saved-chains.txt`, then pass that folder to
the script. The original capture/identities and unknown-sample counts are unchanged.
