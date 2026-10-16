# Bulk hashing of string and bytes storage — 2026-10-16

The first hash of a string or bytes value now feeds its existing storage directly
to the safe managed `HashCode.AddBytes` API. Two per-byte loops are deleted: two
production insertions/eight deletions. Cached fields, volatile publication,
equality, result ownership, admission and callers remain unchanged. There is no
new hash helper, dependency, configuration or unsafe API.

| Engine | Measured revision | Warm complete-job median | IQR / median |
| --- | --- | ---: | ---: |
| Lython before | cd4c8b27aa55b425788b868be4f7660236eb1f37 | 8.807 ms | 29.96% |
| Lython after | c487cb703efa52bd65796fcefadb5e0fac3a72b2 | 6.118 ms | 5.48% |
| CPython | 3.13.16 | 1.2864 ms | 0.73% |

**Inconclusive.** The candidate median is 30.5% lower, alongside a noisy baseline
with 30.0% spread. This one short diagnostic establishes no speedup, qualified
ratio or confidence interval. The change is retained for the source simplification;
the individual hash-operation cost and allocation count are unmeasured. These
revisions do not describe later HEAD. BE, BD and AY are separate historical pairs;
their percentages must not be combined.

The unchanged [complete text-summary job](../../scripts/text-summary.py) reads
3,072 lines / 35,840 UTF-8 bytes, calls lower().split(), counts 6,144 words, writes
four sorted report lines and prints `6144\n`. Independent stdout/file goldens pass
at 0, 16 and 1,024 fixture repetitions for each engine. Actual case, source hash,
size, byte/line counts and lane match the declaration before and after capture.

Boundary: warm synchronous public Run, reused compiled code, fresh invocation
state, file staging/read/count/write, exact output checks and prior-result cleanup
timed. Last-result ownership survives through its response. Compilation, worker
startup and transport are excluded. Lython limits, conservative ownership and
cancellation stay enabled; optional fuel is unset. The host supplies efficient
ranged I/O and a shared unbounded allowance. CPython has no equivalent in-process
containment. Async, fresh compilation and embedding latency are unmeasured.

Machine: dedicated Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS x86_64, four AMD EPYC
9V45 vCPUs, 16 GiB RAM, no swap; SDK 10.0.401/runtime 10.0.12. Workers keep ordinary
GC/tiering. CPython 3.13.16 uses ordinary GIL/GC, `-I -S`, GCC 13.3 PGO/LTO. Supervisor
tiering alone is disabled. Seven rounds span 7.845s under a 30s group cap. One
capture, no retry/retuning/new producer/long lane. All 524 production sources per
role match frozen Git manifests; comparison tooling/backend are identical. Seven
workers/fixtures are gone, services inactive and VM lease free.

Validation: 2,735 ordinary white-box tests pass (18.532s including cleanup), 25
focused key/storage checks (1.179s), and 171 public key/ownership/admission checks
(3.254s). Before and after, 30 text/bytes shapes match CPython 3.13.2 through both
execution APIs, including empty values, NUL, Unicode, tuples, builtin __hash__,
custom hash/equality keys and dict/set lookup. Hashing leaves the supplied allowance
unchanged; repeated executions return to the compiled baseline and retained
text/bytes stay funded until result disposal. No existing tests, original stress
scales, stack policy or timeouts change.

Numeric string/bytes hashes may differ from CPython or across processes/versions;
equal-key consistency is the contract, not the numeric payload. SPEC11.1 now makes
this explicit and points to `hashlib` for stable content digests. No numeric hash
is used as a CPython oracle.

[Samples](warm.json), [declaration](comparison-declaration.json),
[catalog](catalog-micro.json), independent correctness
([before](baseline-correctness.json), [after](candidate-correctness.json)),
[pre-capture gate](before-proof.json), [cleanup/source proof](cleanup-source-proof.json),
[local validation](local-proof.json) and [semantic source](snippets.json) preserve
evidence. Frozen archives, LF-validated scripts and detailed local receipts remain
in private notes.
