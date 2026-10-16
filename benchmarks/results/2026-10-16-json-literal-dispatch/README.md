# Synchronous JSON literal dispatch — 2026-10-16 UTC

JSON true/false/null now use the existing synchronous scalar branch through both
execution APIs. The three literal branches are removed from the generic async
helper. Scalar frame admission, checkpoints and prefix rules stay in place;
NaN/Infinity, numeric hooks and recursive containers keep their existing routes.
No new parser, token representation, ownership machinery or stack policy.

| Engine | Measured revision | Warm JSON transform median | IQR / median |
| --- | --- | ---: | ---: |
| Lython before | 2a2ebada5a74663c98ad2947dea5e7fab5386df1 | 2.365 ms | 17.11% |
| Lython after | 4b930ca52527bb7a320e3bc0a6acef3a41846aff | 2.380 ms | 22.15% |
| CPython | 3.13.16 | 0.4414 ms | 0.15% |

No performance benefit is established: the 0.6% higher Lython median is well
within spread. One inconclusive, unqualified diagnostic gives no confidence
interval, qualified CPython ratio, allocation count, embedding-latency or later
HEAD claim. The CPython gap remains substantial. Historical complete file jobs
remain separate; this workload processes an in-memory document.

The unchanged json.transform-roundtrip.large case loads 1,024 records, flips each
active boolean and dumps compact ASCII JSON. Input is 55,723 UTF-8 bytes; source,
including the existing embedded text fixture, is 58,977 UTF-8 bytes. Source SHA256:
0a8ff6409a214cebdae833c96ab9bc33f3a8c50adeb4ad3823ad4b51ae8b608c.
Independent row construction/inversion goldens pass at 16, 128 and 1,024 records
for both engines before timing. Text includes escaped lambda/emoji data. There
are no host files, artificial delays or new producers. Case, source, input size
and warm-async lane match the frozen declaration.

Boundary: warm public RunAsync, compiled code reused, fresh invocation state,
execution/projection and prior-result cleanup timed. Compilation, startup,
transport and full response hashing excluded. Lython public limits, cooperative
admission and cancellation remain enabled; optional instruction fuel unset.
CPython has no equivalent in-process containment. Actual suspension is tested
separately through object hooks without adding I/O delays to the timing case.

Machine: dedicated Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS x86_64, four AMD EPYC
9V45 vCPUs, 16 GiB RAM, no swap; .NET SDK 10.0.401/runtime 10.0.12. CPython 3.13.16,
GCC 13.3 PGO/LTO, ordinary GIL/GC, -I -S. Workers retain ordinary GC/tiering;
only supervisor tiering is disabled. Seven rounds span 7.385 s under a 30 s group
cap. Frozen Git archives match all 524 production sources per role; tooling and
backend are identical. Seven workers/fixtures are gone, relevant services inactive
and VM lease free. One capture, no retry/retuning/full lane.

Correctness prerequisite: 2a2ebada corrects parser-produced JSON errors before
this comparison. Both measured roles include it. Trailing input uses Extra data;
formatted messages include character coordinates, and args uses the ordinary
single-message route. Trailing-character conversion, three-argument payload
reconstruction and duplicate strict-control location formatting are deleted.
Ten new trailing-document cases fail against old behavior; the corrected focused
suite passes 116 cases. Fourteen new CPython error oracles cover loads/decode/
mediated load, Unicode/multiline text, strict controls, truncation, past-end indexes
and untouched raw_decode suffixes. Explicitly constructed JSONDecodeError behavior
and unrelated backend error wording are outside this parser-produced correction.

Validation: 52 focused white-box tests, 231 public JSON/owner/admission checks,
2,795 ordinary white-box tests (17.179 s), 59 original stack/trace/copy checks
(8.830 s), and 93 representative CSV/text/JSON/file retention/cancellation checks
(14.773 s) pass. Four pre-edit literal fuel/span/effect matrices and 16 saved call
matrices match exactly. Seven frozen CPython controls are now public semantic
cases. Existing delayed-host fixtures additionally cover suspended literal object
hooks and cancellation. No guard, timeout, fixture-scale or memory-policy changes.

[Samples](warm-async.json), [declaration](comparison-declaration.json),
[catalog](catalog-micro.json), independent correctness
([before](baseline-correctness.json), [after](candidate-correctness.json)),
[pre-capture gate](before-proof.json), [cleanup/source proof](cleanup-source-proof.json),
[guard evidence](guards-proof.json), [local validation](local-proof.json) and
error oracles ([trailing](trailing-error-oracles.json), [other](other-error-oracles.json),
[CPython identity](oracle-runtime.json)). Detailed original failed/corrected
receipts and frozen archives remain in private notes.
