# B1238 and B1239 compiled postponed annotation payloads

Executable definitions now reuse one immutable compiled payload when all
annotation text is postponed. The definition pre-admits its eventual
dictionary with the callable. It skips the per-annotation instructions,
temporary operand slots and per-function copy. The first metadata read
builds that function's mutable dictionary directly. Replacing or deleting
unobserved metadata refunds its prepaid storage. An exposed dictionary
can keep aliases after replacement. Eager expressions and annotations
that need the existing type scope still run in their original order.

Both comparisons run `78ad4127d` and the private `b26b50822` with
complete 32-file images and CPython 3.13.2 output checks. The nested
fixture creates 4,096 functions from 2,048 inputs and reads the final
annotation dictionary. The observed fixture reads metadata at each of
its 2,048 definitions. The invoice is a separate file-backed control.

| Warm workload | B1238 elapsed | B1238 process CPU | B1239 elapsed | B1239 process CPU |
| --- | ---: | ---: | ---: | ---: |
| Nested postponed factories | −6.19% to −3.63% | −5.68% to −3.32% | — | — |
| Every postponed dictionary read | −1.60% to +7.13% | −1.66% to +7.10% | −3.62% to −0.72% | −2.81% to −0.78% |
| Nested eager factories | −8.12% to −0.45% | −8.65% to −0.87% | — | — |
| CSV invoice control | +0.38% to +4.22% | −0.42% to +4.60% | −1.91% to −0.71% | −1.92% to −0.30% |

B1238's observed candidate workers differ by 6.48% in elapsed and the
eager reference workers differ by 7.52%. B1239 repeats the observed case
and the invoice with both exact images and reverse worker startup order.
The adverse results do not recur. Both the affected case and the unchanged
invoice move in the repeat, so the evidence for a small observed-case
speedup is weaker than for the nested postponed case. The eager workers
also vary and provide no separate performance claim.

The local Windows Release allocation diagnostic finds 8.25% fewer CLR
bytes for nested postponed factories. The observed, eager and invoice
cases are effectively flat. Accounted peaks match. Repeated local
compilation allocates 0.07% less for the nested postponed workload and
1.71% less for the observed script. Its eager and invoice controls are
within 0.04%. Both diagnostics verify return to the shared allowance
baseline. Compile elapsed and revised CPython ratios remain open.

The Azure F4as_v7 VM ran Ubuntu 24.04.4 and .NET 11 RC3 on November 20,
2026 UTC. Seven rotated batches per lane and two workers per image yield
the four pairings in each range. B1238 used 15 seconds per worker and
completed four lanes in 349 seconds. B1239 used 22 seconds per worker
and completed two lanes in 248 seconds. All 168 samples matched their
goldens. Both runs passed machine checks, joined all workers and verified
the persistent archives after guest fixture cleanup.

Warm elapsed includes mediated RAM-backed I/O where used, goldens and
previous-result disposal. Process CPU covers the wider request including
fixture reset and framing. Startup, compilation and final teardown sit
outside the warm clock. Windows .NET 11 and Ubuntu .NET 10 each pass 346
focused Release cases. The added alias case covers independent mutation,
deletion before the first read, replacement and shared text identity.

[B1238 timing](summary.json) · [B1239 timing](confirmation/summary.json) ·
[Warm allocations](allocation-summary.json) ·
[Compile allocations](compile-allocation-summary.json) ·
[Source and checks](checks-and-procedures.tar.xz).
