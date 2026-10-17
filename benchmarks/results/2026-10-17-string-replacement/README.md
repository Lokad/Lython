# UTF-8 string replacement and admitted copies — 2026-10-17 UTC

Bounded and unlimited replacement now share PyString.Replace. The 59-line
PyStringOps.Replace algorithm and its sole routing call are removed. Empty-pattern
insertion scans only the required prefix, admits checked capacity, copies UTF-8
scalar spans and appends the untouched suffix in bulk. Per-scalar PyString/array
objects disappear. Both branches release private builder scratch on failure.

The same actual bounded member-call diagnostic, warmed in a separate fresh
execution, allocates **115,032 → 4,392 bytes** for a 2,048-byte Unicode input and
one prefix insertion. Before: a03ef99e; after: b10b8e9e. Both include the preceding
copy-admission correction. A both-API script with retained result aliases now
fits its 32 KiB execution cap; both old controls refused. Two sequential runs
return root accounting to baseline. This is an allocation/working-set result,
not an elapsed-time speedup or a CPython allocation comparison.

The existing unlimited replace/split/join pipeline provides separate elapsed
context. It does not exercise that bounded empty-pattern insertion:

| Engine | Measured revision | Warm pipeline median | IQR / median |
| --- | --- | ---: | ---: |
| Lython before | a03ef99e50c40ccd73533b2dd22e5cb6808ecffe | 3.621 ms | 80.79% |
| Lython after | b10b8e9e4753dffc8eeb983141f788dc1781ccb1 | 2.981 ms | 12.69% |
| CPython | 3.13.16 | 0.5404 ms | 0.52% |

The 17.7% lower Lython median is within the very large baseline spread:
inconclusive, no elapsed benefit or qualified ratio established. No later HEAD,
embedding-latency or overall CPython-gap claim. No cause is assigned to the noise.

Unchanged strings.pipeline-supplementary.large: 8,192 blocks, 139,264 UTF-8 input
bytes / 90,112 Unicode scalars; 139,335 UTF-8 source bytes including the embedded
fixture. It replaces :: with /, splits on /, and joins using |. Source SHA256:
f514e27a1b27180ea855f564e268b41d8bd0ee3c753540441d0383df3d8653e3.
Independent literal-block goldens pass at 128, 1,024 and 8,192 blocks for both
roles. No host files, artificial delays or new benchmark producer.

Boundary: warm public RunAsync with reused compilation, fresh invocation state,
execution/projection and prior-result cleanup timed. Compilation, startup,
transport and full response hashing excluded. Public Lython limits, cancellation
and cooperative admission enabled; optional instruction fuel unset. CPython has
no equivalent in-process containment.

Machine: dedicated Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS x86_64, four AMD
EPYC 9V45 vCPUs, 16 GiB RAM, no swap; .NET SDK 10.0.401/runtime 10.0.12.
CPython 3.13.16, GCC 13.3 PGO/LTO, ordinary GIL/GC, -I -S. Workers keep normal
GC/tiering; only supervisor tiering disabled. Seven rounds span 7.759 s under
a 28 s process-group cap. Both 524-production-source manifests and archives
match; tooling/backend identical. Seven workers/fixtures gone, services inactive,
VM lease free. One capture, no retry/retuning/full lane.

Correctness prerequisite a03ef99e unifies builder string materialization through
one governed UTF-8 copy factory. Shared refusal formerly allocated 66,648 bytes
before rejecting a 64 KiB copy; consuming and nonconsuming controls now allocate
1,592 bytes before rejection. Local controls remain below the unchanged 32 KiB
bound. Admission happens before copying; failed builders keep their storage and
funding, successful consuming conversions release working storage, and results
remain charged. Duplicate materialization/local-only preflight code is removed.
Existing FromUtf8 full-array ownership behavior is unchanged. This is a native
helper witness, not a demonstrated public-host exploit or array-wide audit.

Validation: 17 focused white-box, 258 focused public, 2,902 ordinary white-box,
59 original stack/trace/copy, 93 representative CSV/text/JSON/file retention/
cancellation and 325 public JSON/shared-memory checks pass. All 44 saved fuel/
span/effect traces remain exact. Nineteen independent CPython controls cover empty
input/pattern/replacement, count extremes, nonoverlap, supplementary and NUL
characters. Count-zero identity, private scratch cleanup and retained ownership
pass. Original fixtures, guard limits and timeouts are unchanged.

Oversized bounded output uses the existing unlimited string-size RuntimeError
guard before partial construction; [MEMORY.md](../../../MEMORY.md) documents it.
Shared-budget admission refusal remains terminal. Full platform/package
qualification is recorded at the delivery revision, separately from timing.

[Samples](warm-async.json), [declaration](comparison-declaration.json),
[catalog](catalog-micro.json), independent goldens
([before](baseline-correctness.json), [after](candidate-correctness.json)),
[pre-capture gate](before-proof.json), [cleanup/source proof](cleanup-source-proof.json),
[initial failing witnesses](baseline-proof.json), [isolated replacement baseline](isolated-replacement-baseline.json),
[copy-admission correction](copy-admission-proof.json), [CPython semantic oracles](expanded-replacement-oracles.json)
and [local checks/exact traces](local-proof.json). Original process receipts and
frozen archives remain in private notes.
