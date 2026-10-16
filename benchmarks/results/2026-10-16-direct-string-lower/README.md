# Direct immediate string lower calls — 2026-10-16

Immediate zero-argument lower calls on exact strings now use the existing
executable receiver operation. Both execution APIs avoid creating a bound callable
for each line. Synchronous member-cache traffic is also removed; the asynchronous
path already bypassed that cache before this change. Exposed, unbound, expanded,
keyword/error and custom-object calls keep their general paths.

The current casing/result-ownership expression is one shared StringMembers
operation, used by the direct call and retained method wrapper. Unicode mapping,
final-sigma rules, builder admission/release, owner bridge and public transfer
remain unchanged. The same two call checks and managed frame admission remain.
No casing algorithm, memory scope, exception rewriter, compiler fallback or new
configuration framework is introduced.

| Engine | Measured revision | Warm complete-job median | IQR / median |
| --- | --- | ---: | ---: |
| Lython before | b41a03450be7173855ebd25b10a67c17795d3baa | 6.168 ms | 10.49% |
| Lython after | 1894234070082d35eac4ee203410b6edbc472270 | 5.699 ms | 4.46% |
| CPython | 3.13.16 | 1.2403 ms | 0.93% |

**No speedup established.** The candidate median is 7.6% lower, within the
baseline's 10.5% spread. This single short diagnostic is inconclusive, with no
qualified ratio or confidence interval. Source proves the dispatch removal, not
its elapsed magnitude or a measured allocation count. These revisions do not
represent later HEAD. AY's prior split diagnostic and the other historical text
captures have separate source pairs; their percentages must not be combined.

The unchanged `scripts.text-summary.large` job reads 3,072 lines / 35,840 UTF-8
bytes, calls lower().split() and counts 6,144 words. The 353-byte
[script](../../scripts/text-summary.py) writes four sorted count lines and prints
`6144\n`. The fixture includes accented and supplementary characters plus blank
lines. Independent goldens check exact stdout/report bytes at 0, 16 and 1,024 fixture
repetitions for each engine before timing. The micro command reads the case/lane
from its declaration; actual case, source hash, size, byte/line counts and lane
are checked before and after capture.

Boundary: warm synchronous public Run over reused compiled code, fresh invocation
state, file reset/staging, read/convert/count/write, capture/projection, cheap
exact output checks and prior-result cleanup timed. The last result stays owned
through its response. Compilation, worker startup, transport and full hashing
are excluded. Lython containment, conservative ownership/admission and cancellation
remain enabled, optional fuel unset. The host supplies efficient ranged I/O and
a shared unbounded allowance. CPython has no equivalent in-process containment.
Fresh compilation/startup, asynchronous throughput and embedding latency are
unmeasured here.

Machine: dedicated Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS x86_64, four AMD EPYC
9V45 vCPUs, 16 GiB RAM, no swap; SDK 10.0.401/runtime 10.0.12. Release workers retain
ordinary GC/tiering; CPython 3.13.16 uses ordinary GIL/GC, `-I -S`, GCC 13.3 PGO/LTO.
Only supervisor tiering is disabled. Seven rounds span 7.625s under a 30s process-
group cap. One capture, no retry, retuning, new producer or long lane. All 524
production sources per role match frozen Git manifests; tooling/backend are
unchanged. Seven workers/fixtures are gone, services inactive and VM lease free.
Uploaded scripts are LF-validated.

[Samples](warm.json), [declaration](comparison-declaration.json),
[catalog](catalog-micro.json), independent correctness
([before](baseline-correctness.json), [after](candidate-correctness.json)),
[pre-capture gate](before-proof.json), [cleanup/source proof](cleanup-source-proof.json),
[original guard spans](baseline-guards.json), [candidate spans](candidate-guards.json),
[original synchronous cache failure](baseline-cache-failures.json),
[local checks](local-proof.json) and [CPython witnesses](oracles.json) preserve evidence.
Frozen source archives, scripts and detailed local receipts remain in private notes.

All 2,734 ordinary white-box tests pass (18.630s), 114 focused string/casing/ownership
checks pass (1.212s), 205 public casing/ownership/terminal-refusal/shared-budget checks
pass (2.457s), and 57 original stack/attribution/copy controls pass (8.056s). Frozen
8-step calls preserve every fuel cutoff/source span under both APIs. Distinct
receivers use owned results without bound-method cache entries; real receiver
suspension keeps ownership until success/cancellation/refusal cleanup without
replay. Scope disposal returns the shared allowance to baseline/zero. Existing
public checks cover retained methods, Unicode expansion/final sigma, custom host
I/O, exposed/unbound/expanded calls and argument effects before errors.

The first rejection fixture exhausted all headroom, so the old route refused
transient callable observation before casing. The corrected immediate/exposed
controls leave 128 bytes of control headroom and use a 256-byte receiver, isolating
initial casing-capacity refusal. They require the original refusal object and
storage-origin span, an adequate capacity request, unchanged input and cleanup.
Removing an unnecessary operation can change which allocation refuses first;
G02 preserves the actual first origin. No runtime workaround or rewritten origin
is added to preserve the old synthetic failure site.

Five independent CPython 3.13.2 sources match both public APIs. The sixth reference
case derives from native str and remains explicitly unsupported at its class
header; it is preserved as a limitation, not a compatible result. Ordinary
custom-object lower overrides remain covered. Original 300-frame traceback and
100/600 copy-hook fixtures/timeouts are unchanged. No unsafe/native APIs, forced
GC or collector machinery. Platform/package gates are separate delivery checks;
no Stress/full-lane claim.
