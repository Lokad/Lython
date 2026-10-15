# Exact integer unary instructions through RunAsync

Exact `BigInteger` unary operands now use the existing synchronous arithmetic and
observed value-transfer handler before entering generic async operation helpers.
The integer binary/augmented guard already follows this policy. Other operands
keep real asynchronous unary/truth protocols; user-defined classes/subclasses can
still suspend through host I/O and cancellation. No second arithmetic, binding,
ownership or interpreter implementation is added.

| Warm dynamic unary loop, 2,048 values | Earlier Lython | Candidate Lython | CPython 3.13.16 |
| --- | ---: | ---: | ---: |
| Apply `+`, `-`, `~`, `not`; accumulate and print checksum | 1.5519 ms | 1.1854 ms | 0.2455 ms |

One [fixed diagnostic](warm-async.json) observed a **23.6% lower Lython median**.
IQR/median was 1.1% / 1.4% / 0.4%, respectively. This seconds-long diagnostic has
no milestone qualification or claimed confidence interval. It measures this
specific CPU-only script; it does not establish a current overall CPython ratio.
The owned command took 7.475 s including cleanup. No repeat or retuning.

Runtime baseline: `66addead03315549b79b57f3c18135220c8c9cd5`.
Candidate: `f2fb4e074afb745da73884dcf8cc4fec157857dd`, with exactly one frozen
production file in [the declaration](comparison-declaration.json). Both roles use
the identical new catalog fixture from `5d298b6fade0986a1ad95c45ef94c30646f6f5d4`.
The existing addition loop cannot exercise dynamic unary dispatch; it was not
used as an affected comparison. The new case uses existing comparison tooling,
normal range iteration and an independent C# checksum `1 - N*(N+1)/2`.

Measured 2026-10-16 Europe/Paris (raw UTC timestamps fall on October 15), on the
dedicated Azure Standard_F4as_v7: Ubuntu 24.04.4 LTS, four AMD EPYC 9V45 vCPUs /
16 GiB RAM; SDK 10.0.401/runtime 10.0.12, CPython 3.13.16 with normal GC/GIL and
`-I -S`. Ordinary Lython protection, cooperative allowance, GC/tiering and managed
stack admission remain enabled. CPython has no equivalent containment.

Lython uses warm, completed-host `RunAsync`; CPython runs ordinary warm compiled
code. Both create fresh invocation state, execute/check/project output and dispose
previous results inside the worker boundary. Compilation, process startup,
transport and full hashing are excluded. This is not real asynchronous guest
syntax; protocol suspension is tested separately. It does not measure compilation
or startup, and the synchronous interpreter is unchanged.

[Baseline](baseline-correctness.json) and [candidate](candidate-correctness.json)
match the independent checksum at all three core sizes. [Cleanup/source proof](cleanup-source-proof.json)
verifies the one production difference, identical comparison/catalog/Python worker
sources, all seven exited workers, inactive services and available VM lease.

[Local validation](local-validation.json): 2,354 ordinary white-box, four focused
result-denial and 128 affected public checks pass (overlap; all owned groups under
30 s including cleanup). Controls cover exact scalar results and unary-plus
identity, noncallable/NotImplemented protocols, supported user-defined inheritance,
actual host suspension/cancellation without replay, retained heap-integer output,
cleanup to baseline, and denial after body entry before target publication.
Five [independent CPython 3.13.2 goldens](cpython-goldens.json) match.

The initial callback fixture wrongly inherited builtin `int`, which Lython
explicitly rejects. Original diagnostics and the owned 28 s stop are preserved
privately; the corrected fixture uses supported user-defined inheritance with all
callback/cancellation assertions retained. Early-completion reporting avoids waiting
for a host pause when execution has already failed. No timeout/allowance relaxation
or builtin-inheritance support was introduced.

The change shares existing result admission, fuel/span, failure transfer and
ownership behavior. No reference tracing, forced GC, native shortcut or extra
configuration is added. Exact Release package/platform qualification is recorded
at delivery; original Stress and full local public suites were not rerun.
