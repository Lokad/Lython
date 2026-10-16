# JSON container forwarding and prefix deletion — 2026-10-16 UTC

Object/array tokens return the existing parser ValueTask directly. Whole-document
decoding calls the existing value reader directly. Removed: two async forwarding
arms, an unused depth argument, DecodeJsonPrefixAsync, JsonPrefixDecode and
duplicated whitespace/empty checks. Real parsers, nesting admission, hooks,
cancellation and input/result ownership retain their existing routes.

| Engine | Measured revision | Warm JSON transform median | IQR / median |
| --- | --- | ---: | ---: |
| Lython before | 734260108f0d198303ba1c1890cfd8be539f9494 | 2.626 ms | 8.42% |
| Lython after | 35d99a88f7b1cff5aa71e2005027d890e2449036 | 2.691 ms | 12.68% |
| CPython | 3.13.16 | 0.4439 ms | 0.33% |

The 2.5% higher Lython median is within spread: inconclusive, no performance
benefit or qualified ratio. This diagnostic measures the two JSON deletions,
not the subsequent integer-admission correction or today's HEAD. Historical
complete file jobs remain separate. No allocation, embedding-latency or startup
improvement is inferred from this elapsed-time capture.

Unchanged json.transform-roundtrip.large: 1,024 records, 55,723 UTF-8 input bytes,
58,977 UTF-8 source bytes including the existing embedded document. It parses,
inverts each active boolean and dumps compact ASCII JSON. Source SHA256:
0a8ff6409a214cebdae833c96ab9bc33f3a8c50adeb4ad3823ad4b51ae8b608c.
Independent row/inversion goldens pass at 16, 128 and 1,024 records for both
roles; escaped lambda/emoji text is included. No files or artificial I/O delay.

Boundary: warm public RunAsync with reused compiled code, fresh invocation state,
execution/projection and prior-result cleanup timed. Compilation, startup,
transport and full response hashing excluded. Lython limits, cancellation and
cooperative admission remain enabled, optional instruction fuel unset. CPython
does not provide equivalent in-process containment. Actual callback suspension
and cancellation are covered separately by existing delayed-host tests.

Machine: dedicated Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS x86_64, four AMD
EPYC 9V45 vCPUs, 16 GiB RAM, no swap; .NET SDK 10.0.401/runtime 10.0.12.
CPython 3.13.16, GCC 13.3 PGO/LTO, ordinary GIL/GC, -I -S. Workers retain normal
GC/tiering; only supervisor tiering disabled. Seven rounds span 7.516 s under
the 30 s group cap. All 524 frozen production sources per role match, backend
and tooling are identical. Seven workers/fixtures gone, services inactive, VM
lease free. One capture; no retry, retuning, full lane or new producer.

JSON validation before the subsequent arithmetic change: 54 focused white-box,
359 public JSON/admission, 2,797 ordinary white-box, 59 original stack/trace/copy
and 93 representative CSV/text/JSON/file retention/cancellation tests pass.
Four literal, two whitespace, sixteen call and four string traces remain exact.
The new whitespace control uses 1,025 spaces, crossing the existing checkpoint
boundary; its 17-step trace was frozen before prefix deletion. Depth controls
preserve 64-container success/65-container rejection and original error offsets
through both APIs, with object hooks never running past rejected admission.

The later correction 84257ca7 reserves major integer-addition result/scratch
capacity together before computation, commits the result from that reservation
and removes duplicate byte-sum result admission. This is correctness work, not
part of the timing pair. Native-helper controls originally allocated 68,104–78,368
bytes before refusal. The corrected 32 local/shared-budget cases allocate
2,480–14,528 bytes, below their unchanged 32 KiB bound, with unchanged funding
and first-refusal origin. Eight funded-result and two cancelling-result controls
pass. This is not a demonstrated public-host exploit or an arithmetic-wide audit.
Conservative estimates can reject earlier, including opposite-sign cancellation;
see [MEMORY.md](../../../MEMORY.md). No elapsed-time claim for that correction.

Final combined validation: 2,839 ordinary white-box, 669 focused public, 59 stack
and 93 representative checks pass; all 26 saved traces remain exact. Original
stack fixtures, timeouts and stress sizes are unchanged. Full platform/package
qualification is recorded separately at the delivery revision.

[Samples](warm-async.json), [declaration](comparison-declaration.json),
[catalog](catalog-micro.json), independent goldens
([before](baseline-correctness.json), [after](candidate-correctness.json)),
[pre-capture gate](before-proof.json), [cleanup/source proof](cleanup-source-proof.json),
[exact traces](guards-proof.json), [pre-edit whitespace](whitespace-baseline.json),
[JSON-only validation](local-proof.json), [integer baseline](integer-addition-baseline-proof.json)
and [final combined validation](final-local-proof.json). Failed intermediate
approaches, cold-allocation observations and original archives remain in private
notes; no failed admission is retried in a command.
