# JSON numeric and callback forwarding — 2026-10-16

Default JSON integer/float conversion now returns a completed ValueTask, and
optional callbacks are forwarded directly. The shared callback helper also
forwards its existing invocation without an extra state machine. These are two
removed forwarding state machines; parsing, result types, binding, managed guards,
preflights and caller-owned frame/scratch cleanup remain. Object/pairs/constants
and encoder-default hooks share that helper. No parser/owner/dispatch framework.

| Engine | Measured revision | Warm complete-job median | IQR / median |
| --- | --- | ---: | ---: |
| Lython before | 537f8903bfe4db600897c0f76f46c036ff061c5c | 8.932 ms | 14.19% |
| Lython after | 5d5fd2e7d7c0f9f044a03b0c31d730835ea3e0e9 | 9.475 ms | 2.65% |
| CPython | 3.13.16 | 4.1751 ms | 0.96% |

**No performance benefit established.** The candidate median is 6.1% higher,
within the baseline's 14.2% spread. This single unqualified diagnostic establishes
neither a speedup nor a regression magnitude, workload ratio or confidence
interval. Retain the removal for simpler forwarding with unchanged contracts,
not as a measured performance improvement. This job uses default integer parsing;
callback-overhead magnitude remains unmeasured. The old completed ValueTask state
machines normally use structs, so their removal does not by itself establish a
heap-allocation reduction. These revisions do not describe later HEAD. Earlier
5ed2b203 string dispatch, dc950cde warm and 1ad18b79 fresh captures remain separate
source pairs/boundaries; do not combine their percentages.

The unchanged `scripts.json-summary.large` job reads 4,096 JSON records across
5,120 lines / 110,592 UTF-8 bytes, skips blanks, filters negative amounts and
aggregates by kind. The 536-byte [script](../../scripts/json-summary.py) writes
exactly `{"blue":7168,"red":3072}\n` and prints `3072\n`. Independent goldens
check stdout and exact output files at 0, 16 and 1,024 four-record fixture
repetitions for both engines before timing. The declared case/source/size/lane
are verified before capture; the micro script reads case/lane from the declaration,
and the actual receipt is checked again afterward.

Boundary: warm synchronous public `Run` over reused compiled code, fresh invocation
state, file reset/staging, parsing/aggregation/writing, capture/projection, cheap
exact output checks and prior-result cleanup timed. The final result stays owned
through its response. Compilation, worker startup, transport and full hashing are
excluded. Both engines use the same file-job boundary. Lython admission/explicit
owners, cancellation checkpoints and public limits remain enabled; optional fuel
is unset. The host supplies efficient ranged reads/appends and a shared unbounded
allowance. CPython has no equivalent in-process containment. Fresh compile-to-result,
startup and embedding-process latency are unmeasured here.

Machine: dedicated Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS x86_64, four AMD EPYC
9V45 vCPUs, SDK 10.0.401/runtime 10.0.12. Release workers retain ordinary GC/tiering;
CPython 3.13.16 uses ordinary GIL/GC, `-I -S`, GCC 13.3 PGO/LTO. Only supervisor
tiering is disabled. Seven rounds span 8.851 s under a 30 s systemd process-group
cap. One capture; no retry, retuning, new producer or long lane. All 524 production
sources per role match frozen Git manifests, comparison tooling/backend are
identical, seven workers/fixtures are gone, services inactive and VM lease free.
Uploaded scripts are LF-validated.

[Samples](warm.json), [declaration](comparison-declaration.json),
[catalog](catalog-micro.json), independent correctness
([before](baseline-correctness.json), [after](candidate-correctness.json)),
[pre-capture gate](before-proof.json), [cleanup/source proof](cleanup-source-proof.json),
[frozen baseline guards](baseline-checkpoints.json) and independent
[CPython numeric witnesses](oracles.json) preserve evidence. Frozen source archives,
scripts and local build/test receipts remain in private notes.

All 2,703 ordinary white-box tests, 337 public JSON/terminal-refusal controls and
57 stack/typed-attribution/copy controls pass locally. Forty-eight focused controls
preserve every 16/15-default and 18/19-hook checkpoint, span, original token spelling
and exact 8/40-byte scratch reservation at callback depth 4. Actual native hook
suspension keeps caller frame/scratch until completion or cancellation, without
replay; cleanup restores counters/scratch and closes the owner to zero allowance.
Existing tests cover long/exact-bigint/signed-zero/float-overflow behavior, denied
magnitude/text allocation before parsing/callback, all public callback families,
subclasses, real host effects, retained aliases and bounded batches. No changed
limits, compiler prototype, weak reclamation, forced collection or unsafe/native
API. Package/platform gates are separate delivery checks; no Stress/full-lane claim.
