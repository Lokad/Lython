# Direct JSON string-token dispatch — 2026-10-16

JSON property names and string values now use the existing synchronous token
reader under both execution APIs. Quoted tokens return a completed ValueTask;
they no longer construct the generic async value-reader state machine. The old
string arm is removed from that async route. Non-string values and hooks retain
their existing path. No new parser, storage representation or memory owner.

| Engine | Measured revision | Warm complete-job median | IQR / median |
| --- | --- | ---: | ---: |
| Lython before | b7fd485a9683f918ac0690578744f2b364688f4d | 9.806 ms | 10.23% |
| Lython after | 5ed2b2038025e6746785857b3cea1910d4889786 | 9.010 ms | 5.15% |
| CPython | 3.13.16 | 4.2146 ms | 0.99% |

The observed 8.1% lower Lython median is within the baseline's 10.2% spread:
**no improvement is established**. One diagnostic supplies no qualified workload
ratio, confidence interval or attribution of the overall performance gap. These
historical revisions do not describe later HEAD. The earlier dc950cde warm capture
and 1ad18b79 fresh compile/run capture are separate source pairs and boundaries.

The unchanged `scripts.json-summary.large` job reads 4,096 JSON records across
5,120 lines / 110,592 UTF-8 input bytes, skips blank lines, parses each object,
filters negative amounts and aggregates by kind. It writes exactly
`{"blue":7168,"red":3072}\n` and prints `3072\n`. The 536-byte
[script](../../scripts/json-summary.py) is unchanged. Independent goldens verify
stdout and output-file bytes at 0, 16 and 1,024 four-record fixture repetitions
for both engines before timing. The selected case, size, source and lane are
checked against the declaration before capture; the script reads case/lane from
that declaration, and cleanup proof verifies the actual receipt again.

Boundary: warm public synchronous `Run` over reused compiled code, fresh invocation
state and file reset/staging, parsing/aggregation/writing, capture/projection,
exact output checks and prior-result cleanup timed. Startup, compilation, transport
and full output hashing are excluded. The final result stays owned through its
response. Both engines use the same file-job boundary. Lython admission, explicit
owners, cancellation checkpoints and public limits remain enabled; optional fuel
is unset. The comparison host supplies efficient ranged reads/appends and a shared
unbounded allowance. CPython has no equivalent in-process containment. Fresh
compile-to-result, startup and embedding-process latency are not measured here.

Machine: dedicated Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS x86_64, four AMD EPYC
9V45 vCPUs, SDK 10.0.401/runtime 10.0.12. Frozen Release workers retain ordinary
GC/tiering; CPython 3.13.16 uses ordinary GIL/GC, `-I -S`, GCC 13.3 PGO/LTO. Only
supervisor tiering is disabled. Seven rounds span 8.895 s under a 30 s systemd
process-group cap. One capture, no repeat/retuning/new producer/full lane. All 524
production sources per role match frozen Git manifests; comparison tooling and
backend pin are identical. Seven workers and their fixtures are gone, relevant
services inactive and exclusive VM lease free. Uploaded scripts are LF-validated.

[Samples](warm.json), [declaration](comparison-declaration.json),
[catalog](catalog-micro.json), independent correctness
([before](baseline-correctness.json), [after](candidate-correctness.json)),
[pre-capture gate](before-proof.json), [cleanup/source proof](cleanup-source-proof.json)
and [frozen baseline guard matrices](baseline-checkpoints.json) preserve evidence.
Frozen sources/scripts and local build/test receipts remain in private notes.

All 2,691 ordinary white-box tests, 337 public JSON/terminal-refusal controls and
57 managed-stack/typed-attribution/copy controls pass locally. The string decoder,
managed frame admission, limits, scratch/output preflights and retained owners are
unchanged. Every cutoff in the 15/29-step scalar/object-pairs witnesses preserves
source spans, hook effects and restored counters; shared allowance returns to zero
after each owner closes. Existing controls cover invalid UTF-8, escaped/lenient
strings, before-allocation refusal, prefix/error positions, subclasses, genuinely
suspended/cancelled hooks, escaping aliases and bounded host-driven batches.
No restored compiler prototype, changed nesting limit, weak reclamation, forced
collection or unsafe/native API. Separate package/platform qualification is a
delivery check; this diagnostic does not claim Stress or a full benchmark lane.
