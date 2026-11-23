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
Keep the candidate private pending one confirmation that splits the
postponed phases and repeats the eager and untouched helper controls.

[Frozen identity](identity.json) · [Timing summary](summary.json) ·
[Raw observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Executable parity](executable-parity.json) · [Compile allocations](compile-allocations.json) ·
[Runtime allocations](allocation-summary-six.json) · [Source and procedures](procedures.tar.xz).
