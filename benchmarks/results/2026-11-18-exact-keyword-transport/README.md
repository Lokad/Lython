# B1005 direct transport for exact trailing keyword mappings

Measured `2783d3d7f` (`64fd18de`) and prototype `1e7f1d526` (`d97f6f98`)
on the quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3 on
2026-11-18 UTC. Both complete images verified against their 32-file manifests.
Five jobs took 232.52 seconds. Each job used two identical processes per
source and five measured rounds per process. Both quiet gates and all golden
checks passed. All workers joined and raw evidence was archived before exit.

| Workload and boundary | Candidate elapsed change | Candidate process CPU change |
| --- | ---: | ---: |
| Expanded file forwarding, warm | −17.89% to −16.36% | −18.91% to −17.06% |
| Expanded file forwarding, compile-to-result | −16.96% to −16.33% | −17.65% to −14.75% |
| Expanded file forwarding, compilation | −0.34% to −0.07% | −0.66% to +1.58% |
| Mutable bound-method report, warm | −26.92% to −24.44% | −27.30% to −24.36% |
| Positional calls, warm | −0.05% to +0.58% | −1.56% to +1.23% |

The mutable report calls one bound method across 1,024 records. Each call
mutates its kwargs and checks the source and the original non-ASCII key alias.
A separate Windows allocation screen shows 20.73% lower allocation for file
forwarding at the same accounted peak and 40.86% lower for the mutable report
with a peak within 0.06% of baseline. Allowance returned to baseline in all
cases. Compilation movement is inconclusive. Both execution gains support
retaining the direct path.

A sole trailing exact dictionary can populate the final admitted `**kwargs`
once all positional slots are supplied in an eligible signature. This removes
the per-key packet, its growth and the second build of the keyword dictionary.
The original expansion still handles custom mappings and other argument shapes.
The bound guest-method path also borrows the active packet for ordinary
expanded arguments. Release checks covered Windows .NET 11 and WSL .NET 10.
The CPython probes match in both execution modes, including bad-key precedence,
alias identity and mutation after an earlier expansion. Focused tests cover
admission reentry, terminal refusal and cancellation during a suspended call.
The suspension test was added after freezing and changes only test source.

Warm includes mediated I/O where used, output checks and prior-result cleanup.
Compile-to-result adds compilation and compilation-owner disposal. Startup,
fixture reset and final teardown are outside guest elapsed. Process CPU covers
the wider request. The initial catalog fixture had a mismatched size and was
rejected before any measurement. The corrected catalog verified against both
actual images. The original fixture, verifier failure and reentry fixture
correction are preserved in the procedure archive.

[Measurements](summary.json) · [Raw observations](observations.tgz) ·
[Source, checks and procedures](checks-and-procedures.tar.xz)
