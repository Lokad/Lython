# Direct ordinary builtin length calls — 2026-10-16

Eligible executable calls to the exact builtin len now consume their single
positional input directly from existing operand storage. They bypass pooled
CallArgumentValue buffer rental/fill/clear and the binder's fresh one-element
parameter array. The call buffer was already pooled: this is not evidence that
two heap allocations disappear per call. The existing string-only guarded entry
is replaced by one shared single-input guard with cached static operations.
No binder, owner, dispatch interface, stack policy or compiler route is added.

| Engine | Measured revision | Warm strip/length-loop median | IQR / median |
| --- | --- | ---: | ---: |
| Lython before | 17eac376c766aa8ebd691b03b08c02b3808d3be9 | 24.213 ms | 18.79% |
| Lython after | d4ac39db4139168a9950a2460c1b2b1e835ba8a2 | 17.832 ms | 8.07% |
| CPython | 3.13.16 | 3.1868 ms | 0.54% |

The candidate median is 26.4% lower in one short, noisy, unqualified diagnostic.
This does not establish a stable speedup, confidence interval, qualified CPython
ratio, allocation count or complete-file-job improvement. The remaining CPython
gap is substantial. These revisions do not describe later HEAD. Earlier strip
and string-entry diagnostics are distinct source pairs; do not combine percentages.

The unchanged strings.strip-lines.large case creates 425,984 UTF-8 bytes of text
inside execution and strips/measures 32,768 lines, printing `245760\n`. It uses
16,384 repetitions and 143 UTF-8 source bytes; there are no host input files.
Independent goldens are 15 times the repetition count at 256, 2,048 and 16,384.
Both engines pass all three sizes before timing. Case, source hash, size and
warm-async lane match the declaration before and after capture.

Boundary: warm public RunAsync, reused compiled code, completed host operations,
fresh invocation state, construction/execution/projection and prior-result
cleanup timed. Last-result ownership survives through its response. Compilation,
worker startup, transport and full response hashing are excluded. Lython limits,
cooperative admission and cancellation remain enabled; optional fuel is unset.
CPython has no equivalent in-process containment. Actual suspension is tested
separately, without injecting I/O delay into the comparison.

Machine: dedicated Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS x86_64, four AMD EPYC
9V45 vCPUs, 16 GiB RAM, no swap; SDK 10.0.401/runtime 10.0.12. Workers retain normal
GC/tiering. CPython 3.13.16 uses ordinary GIL/GC, `-I -S`, GCC 13.3 PGO/LTO.
Supervisor tiering alone is disabled. Seven rounds span 8.470s under a 30s group
cap. One capture; no retry, retuning, new producer or long lane. All 524 production
sources per role match frozen Git manifests; comparison tooling/backend are
identical. Seven workers are gone, relevant services inactive and VM lease free.

Validation: 2,746 ordinary white-box tests pass (19.825s including cleanup), 40
focused runtime checks (1.158s), 229 public semantic/ownership/refusal checks
(3.587s), 59 original stack/attribution/copy checks (9.536s), and 93 representative
CSV/text/JSON/file retention/cancellation checks (14.800s). All seven pure controls
match local CPython 3.13.2 through both APIs. The original eight-step/span matrix
matches at every cutoff through both APIs. Suspended inputs are not replayed and
retain funding through success, cancellation and terminal admission refusal.

Builtin aliases, argument evaluation effects, member-fallback argument offsets,
shadowed functions, expanded/keyword/wrong-arity calls retain their behavior.
Python instances keep their existing type-slot/async protocol route under both
APIs; no synchronous attempt/replay is introduced. General binding and required
lowered execution remain. Original stack limits, 300-frame trace, 100/600-copy
fixtures, length-result validation and ownership/preflight kernels are unchanged.

[Samples](warm-async.json), [declaration](comparison-declaration.json),
[catalog](catalog-micro.json), independent correctness
([before](baseline-correctness.json), [after](candidate-correctness.json)),
[pre-capture gate](before-proof.json), [cleanup/source proof](cleanup-source-proof.json),
[baseline guards](baseline-guards.json), [candidate guards](candidate-guards.json)
and [local validation](local-proof.json) preserve evidence. Frozen archives,
LF-validated scripts and detailed local receipts remain in private notes.
