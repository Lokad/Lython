# B1470 Forward terminal private assignments

The private candidate `8a44f644d` removes the store when an uncaptured
lexical local is assigned and immediately returned. The existing
reachable-block forwarding pass recognizes the three adjacent operations.
The ordinary return consumes the original result and retains its span.
Protected and mirrored functions and captured locals keep their stores.

Windows .NET 11 and Ubuntu .NET 10 each passed 140 white-box and 904
public Release checks. The cases cover returned identity, captured stores,
`finally`, real asynchronous host work, cancellation and allowance reuse.
A Windows pass added 165 regex contract, semantic and lifetime checks.
The two exact 32-file images matched all six CPython goldens. A separate
CPython check matched the private, captured and protected factory case.
Executable inspection found 171 unchanged code objects. Each changed
object drops exactly one `StoreLocal`; storage capacity, exception
regions, captures and native eligibility are unchanged. The first
inspector invocation rejected the generated catalog envelope before
processing any script. The corrected envelope retains the same inputs.

Factory runtime allocation and admitted peaks match. All three factory
scripts allocate 240 fewer bytes during compilation and all six compile
peaks match. Small allocation changes on the two other scripts are below
0.2%. All observed owner balances return to baseline.

## Initial quiet comparison

The dedicated Ubuntu Azure F4as_v7 ran the same .NET 11 RC3 workers and
adapter with `6e01ec965` as reference. All six lanes used two independent
processes per image, eight seconds of warmup and seven rotated batches
per process. Warm elapsed includes applicable mediated RAM-backed I/O,
golden checks and prior-result disposal. Time to result also includes
compilation and compiled-owner cleanup. Wider CPU includes worker
reset and protocol. Startup and final teardown are separate.

| Workload | Boundary | Elapsed change | Wider CPU change |
| --- | --- | ---: | ---: |
| Postponed factories | Time to result | +1.68% to +3.15% | +1.75% to +5.84% |
| Eager factories | Warm | −5.85% to −1.99% | −6.02% to −2.93% |
| Defaults observer | Warm | −1.57% to +2.17% | −3.32% to +3.42% |
| Fully supplied helper | Warm | −0.39% to +4.48% | −1.80% to +2.83% |
| Quoted CSV invoice | Warm | −3.96% to +3.84% | −2.56% to +3.85% |
| Sparse configuration | Time to result | −2.26% to −0.77% | −2.31% to −0.62% |

All 168 batches passed and the archive verified after worker and
fixture cleanup. Ranges compare all four pairings of process medians.
The postponed control spread stays below 0.8% on both images, while
unchanged scripts have broader or directionally favorable controls.
The candidate stayed private while a second run separated the
postponed phases and repeated the eager and fully supplied controls.

## Same-image phase confirmation

B1471 uses the same images, catalog and sampling boundaries. All 140
batches pass. Workers join and the quiet-machine archive verifies.
The initial local launch failed before uploading when an image hash
map shadowed the payload list. The corrected launch verifies all the
same files and preserves its original inputs.

| Workload | Boundary | Elapsed change | Wider CPU change |
| --- | --- | ---: | ---: |
| Postponed factories | Time to result | −2.78% to +1.64% | −3.41% to +2.85% |
| Postponed factories | Warm | −5.23% to −3.15% | −4.99% to −3.02% |
| Postponed factories | Compilation | −1.81% to −0.49% | −0.55% to +0.33% |
| Eager factories | Warm | −6.65% to −3.26% | −8.17% to −3.34% |
| Fully supplied helper | Warm | −3.69% to −2.29% | −3.51% to −2.27% |

Retain the simpler tape in `fed081ff0`. Eager warm elapsed falls in
both runs. The unchanged fully supplied helper also improves in the
phase confirmation, which limits attribution. Postponed time to
result remains inconclusive across runs. Retain the initial adverse
result for the next callable-construction checkpoint.

[Phase identity](phase-identity.json) · [Phase summary](phase-summary.json) ·
[Raw confirmation](phase-observations.tar.xz) · [Phase terminal status](phase-terminal-status.json) ·
[Phase procedures and initial launch failure](phase-procedures.tar.xz).

[Frozen identity](identity.json) · [Timing summary](summary.json) ·
[Raw observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Executable parity](executable-parity.json) · [Compile allocations](compile-allocations.json) ·
[Runtime allocations](allocation-summary-six.json) · [Source and procedures](procedures.tar.xz).
