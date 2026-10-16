# Ordinary Python function operand transport — 2026-10-16

Ordinary Python function and bound-method calls now feed existing operand values
and compiled argument names to the current binder. The temporary CallArgumentValue
array rental/fill/clear and asynchronous preparation closure are skipped. One
validation/default/variadic implementation remains. Protocol-key default lookup
starts after inputs have entered invocation-owned parameter slots; its completion
loop keeps those slots through suspension and returns them on failure. No new
binder/view/cache/owner interface, stack policy or class compiler is introduced.

| Engine | Measured revision | Warm keyword-call median | IQR / median |
| --- | --- | ---: | ---: |
| Lython before | e9dbb7acb7e9c17950442a28fe3d586af8b9ea0f | 6.800 ms | 13.26% |
| Lython after | 60961196b8f1c70c2e43163bd9e4df2b637b32c4 | 6.680 ms | 1.29% |
| CPython | 3.13.16 | 1.0849 ms | 0.62% |

No speedup is established: the 1.8% lower median is within baseline spread.
This is one inconclusive, unqualified diagnostic, with no confidence interval,
qualified CPython ratio, allocation count or complete-file-job/embedding-latency
claim. The CPython gap remains substantial. These measured revisions do not
describe later HEAD. Percentages from earlier call changes must not be combined.

The unchanged calls.keyword.large case makes 16,384 calls to a small helper,
passing one ordinary positional value and the keyword offset=5, then prints
`134291456\n`. Its source is 134 UTF-8 bytes. Independent goldens are
N*(N-1)/2+5*N at 256, 2,048 and 16,384 calls. Both engines pass all three sizes
before timing. Case, source hash, size and warm-async lane match before/after.
There are no host input files and no new producer.

Boundary: warm public RunAsync with reused compiled code, completed host
operations, fresh invocation state, execution/projection and prior-result cleanup
timed. Last-result ownership survives through its response. Compilation, worker
startup, transport and full response hashing are excluded. Lython limits,
cooperative admission and cancellation remain enabled; optional fuel is unset.
CPython has no equivalent in-process containment. Actual protocol suspension is
validated separately without adding artificial I/O delay to the comparison.

Machine: dedicated Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS x86_64, four AMD EPYC
9V45 vCPUs, 16 GiB RAM, no swap; SDK 10.0.401/runtime 10.0.12. Workers retain normal
GC/tiering. CPython 3.13.16 uses ordinary GIL/GC, `-I -S`, GCC 13.3 PGO/LTO.
Supervisor tiering alone is disabled. Seven rounds span 7.209s under a 30s group
cap. One capture, no retry/retuning/long lane. All 524 production sources per role
match frozen Git manifests; comparison tooling/backend are identical. Seven
workers are gone, relevant services inactive and the VM lease is free.

Validation: 2,768 ordinary white-box tests pass (20.108s including cleanup), 112
focused runtime checks (1.239s), 158 public binding/default/ownership/admission
checks (3.296s), 59 original stack/attribution/copy controls (8.830s), and 93
representative CSV/text/JSON/file retention/cancellation checks (15.079s).
Nine pure binding controls match local CPython 3.13.2 through both APIs. Sixteen
saved fuel/span/body-effect matrices match byte-for-byte. Six new default-key
protocol controls cover function/method suspension, cancellation and terminal
refusal, retained funding, no replay, failure origin and cleanup before reuse.

Mutable __defaults__/__kwdefaults__, keyword/positional-only/variadic errors,
argument effects/rebinding, exposed method identity and dynamically attached
executable methods retain their Python behavior. Required lowered class bodies
remain. Two old keyword-pool assertions now expect untouched storage, preserving
all result/alias/cleanup checks. Other native/wrapped callables retain their array
contract. Fast positional eligibility, original managed stack limits, 300-frame
traceback and 100/600-copy fixtures are unchanged. Small transport buffers were
pooled already; bypassing a rental alone does not prove a heap allocation saved
for every synchronous call. Larger frame copies, keyword-map caching and other
builtin/general-call transport remain separate work.

[Samples](warm-async.json), [declaration](comparison-declaration.json),
[catalog](catalog-micro.json), independent correctness
([before](baseline-correctness.json), [after](candidate-correctness.json)),
[pre-capture gate](before-proof.json), [cleanup/source proof](cleanup-source-proof.json),
[baseline guards](baseline-guards.json), [candidate guards](candidate-guards.json)
and [local validation](local-proof.json) preserve evidence. Frozen archives,
LF-validated scripts and detailed local receipts remain in private notes.
