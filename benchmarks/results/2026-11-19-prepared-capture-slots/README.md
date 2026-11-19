# B1055 prepared executable capture sources

R624 prepares local and incoming cell indexes once for executable parent-child
pairs. Closure creation uses those indexes for sizing and filling the retained
cell array. It keeps namespace lookup for inlined comprehensions, class cells
and independently compiled code. Cell retention, admission and rollback stay
shared. The runtime and test sources measured at `8e877df62` match the retained
`f80035c13`. The exact reference is `68a7fc465`.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-19 UTC.
Both complete 32-file worker images and all eight cases verified against
CPython goldens. Ranges cover all four pairs of candidate/reference process
medians across duplicate same-source controls. Negative changes favor the
candidate.

| Workload | Boundary | Guest elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Captured rules | Warm | −7.42…−6.05% | −6.81…−6.19% |
| Captured rules | Compile-to-result | −5.16…−4.12% | −7.01…−4.61% |
| Mutable rule counters | Warm | −9.89…−7.23% | −9.86…−6.85% |
| Mutable rule counters | Compile-to-result | −9.66…−5.24% | −9.81…−5.85% |
| Ordinary rules | Warm | −2.01…−1.26% | −0.24…+0.32% |
| Annotated rules | Warm | −1.63…−1.43% | −1.56…−1.17% |
| Expanded forwarding | Compile-to-result | −1.85…+0.29% | −3.70…+1.55% |
| Positional calls | Warm | −2.43…+1.74% | −2.47…+2.42% |

Mutable counters' reference process medians differ by 3.59% in the
compile-to-result lane. Both target workloads still improve across all
pairs with lower CPU. Forwarding and positional elapsed span zero.
The Windows allocation diagnostic finds 3.98% less warm allocation for
captured rules and 2.86% less for mutable counters, at equal accounted
execution peaks. All allowance baselines recovered after cleanup.

Windows .NET 11 and WSL .NET 10 each passed 738 public and 113 white-box
Release checks, including inlined comprehension rebinding, nonlocal and
class-cell semantics, generators, imports, cancellation, retention and refusal.
Both VM quiet gates and all 160 measured golden batches passed. Workers
joined, guest fixtures were removed and the complete raw data was archived
and verified under the lease in 364.72 seconds of collection.

R622 and R624 passed the Windows and Ubuntu .NET 10/11 Release suites,
original stress cases and installed-package consumers at `af7cdee8a`.
[Delivery CI](https://github.com/Lokad/Lython/actions/runs/44471822057).

Warm includes mediated I/O, golden checks and preceding-result cleanup.
Compile-to-result also includes compilation and compiled-owner disposal.
Startup and final cache teardown are outside guest elapsed. Process CPU
has a wider boundary. CPython supplies correctness goldens in this run.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Archive receipt](archive-receipt.json) · [Checks and procedures](checks-and-procedures.tar.xz).
