# Call-operand transport prototype withdrawn

2026-11-05, baseline9c1d7e74/core70e608fc, prototypebaa7b6ae. The complete source
patch/snapshot, identities, conditions and every sample are in observations.tgz.
No production implementation from this prototype remains.

The D39 transition proof used the stable paused instruction for call site/start/
span and existing owned operands for the selected function/receiver. Descriptor
getters kept exact selected callables across awaited cleanup, with two additional
compiler-admitted slots. Child creation and caller-mode retirement stayed at their
existing boundaries. Five staging fields were removed without a frame arena,
saved-call record, second engine or new memory owner. Net production−4lines.

This is a structural simplification hypothesis, not an established CPU/allocation
benefit. It adds reconstruction/type selection and increases some frame capacities
while reducing object fields. Their individual contributions were not measured.

## Bounded complete-script evidence

139.98s/five jobs on the same quiet VM, frozen workerb94/auxiliaries, independent
identical-current process and CPython3.13.16. Five-second per-engine warmup/job,
seven rotated approximately150ms samples, normal GC/tiering. All source/golden/
identity/quiet gates, Closed responses, final zero reservations, successful joins
and fixture cleanup passed.

| Workload/lane | Candidate ms | Identical candidate ms | Reference ms | Changes vs reference |
| --- | ---: | ---: | ---: | ---: |
| Ordinary helper CSV/warm |5.115|4.996|4.855|+5.34% /+2.90%|
| Ordinary helper CSV/compile plus run |5.223|5.238|5.200|+0.45% /+0.73%|
| Ordinary helper CSV/warm async |5.837|5.873|5.790|+0.80% /+1.42%|
| Text/warm |2.591|2.498|2.496|+3.83% /+0.08%|
| JSON/warm control |4.543|4.654|4.537|+0.13% /+2.57%|

Ordinary warm CSV is slower in both roles; its candidate IQR1.35%/.42% and
reference.63% do not explain the whole2.9–5.3% difference. Current spread2.32%
limits magnitude. Text controls differ3.61%; JSON2.44%; both retain all adverse
signs without causal claims. No whole-script improvement is established.

**Withdraw.** Reducing five fields does not justify the measured ordinary-script
regression, extra reconstruction and capacity tradeoff in a performance milestone.
This is a design decision on actual evidence, not a fixed-percent veto or permanent
ban. A materially different elimination/refinement may be justified later; no
automatic confirmation or narrower field-by-field rescue is proposed now.
Allocation and accounted peak changes remain unmeasured. The overall performance
objective remains active; no claimed solution to the diagnostic CPython gap.

Clocks include actual mediated /dev/shm I/O, invocation, complete output/file
validation and prior-result cleanup; fresh also compiles/disposes each invocation.
Startup, fixture staging/reset, transport and final owner/process/fixture teardown
are excluded. Async uses completed host operations; genuine suspension is tested
separately. Same boundaries as the current performance guide.

## Correctness and restoration

Build30.37s, zero warnings/errors. Focus886internal/1730public per actual.NET11/10
pass: calls, subscriptions, closures, generators, managed stack, cancellation,
refusal, retained ownership and cleanup. Full actual-target suites each pass
4113internal/16001public. No tests or timeouts weakened and no dormant replacement
remains. All three source files restored byte-for-byte to9c1d7e74; a nonincremental
build31.87s/zero warnings/errors follows restoration. Retained-source7internal/
142public focused checks pass per actual.NET11/10. Restored core6cb05f8e differs
from the prior precommit70e608 binary; exact source-blob hashes and build identities
are recorded separately. No timed/binary-identity claim for that rebuild.

One preparation script initially assumed fixed padding in test counts, failing
before upload/collection. It was changed to parse whitespace; no measurements
were discarded or repeated. Original error remains in the conversation/private
notes. The earlier B61/B69 measured slower call-control/arena designs remain
separate evidence, not a universal conclusion about frame construction.
