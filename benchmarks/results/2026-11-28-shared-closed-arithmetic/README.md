# B1976 Share completed scalar arithmetic across both execution loops

The native loop and ordinary prefix use one entry for integer slots,
inline doubles and completed scalar arithmetic. The ordinary input
path uses the same exact-operand decision and the private native
integer wrapper is gone. Exact numeric and text pairs continue through
the existing builtin operators. Dynamic operands retain their sync or
async protocol path.

The seven-slot generator now completes exact exponentiation before
transferring its slots. On the 2,048-row CSV diagnostic this eliminates
278,500 CLR bytes in sync and 278,472 in async execution, about 272 KiB.
The 31-slot form, ordinary tuple/list/range reductions and the explicit
and iterable controls differ by less than 256 B. The 128 retained
iterator aliases allocate the same CLR bytes. All eight witnesses
keep their peak execution admission, compiled charge and growth
request counts, then refund to the original allowance.

Windows .NET 10 and 11 and Ubuntu WSL .NET 11 each pass 630 public and
120 white-box focused Release checks. The supplemental Windows .NET
11 run passes 709 formatting, comparison and exception checks.
The measured worker pair differs only in the core DLL. The reference
is the exact `1f48a0477` source. The candidate is the archived patch
over that revision. Both images pass eight stock CPython 3.13.16
output and file goldens locally and on the VM.

## Quiet warm async comparison

November 28, 2026 UTC on Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3.
Two workers per image warm for at least five seconds and run seven
rotated batches each. All four timed workloads use the same 2,048 CSV
rows. Elapsed includes mediated RAM-backed I/O, verification and
previous-result disposal. Process CPU also includes fixture reset
and framing. Both exclude compilation and launch. The ordinary
generator compares a record field with a string. The exponentiation
diagnostic squares that boolean before yielding it. Ranges span
pairings of worker medians.

| Workload | Elapsed change | Process CPU change |
| --- | ---: | ---: |
| Compact generator exponentiation | −13.29% to −10.41% | −13.04% to −8.48% |
| Explicit loop control | −2.56% to +2.73% | −2.55% to +2.77% |
| Ordinary tuple generator control | −2.31% to +1.67% | −2.98% to +0.73% |
| Iterable shipment control | −0.61% to +0.09% | −0.75% to +0.37% |

Both quiet gates and all 112 timed batches verify. The workers join,
the RAM-backed fixtures are removed, and the persistent archive
verifies before the lease ends.

[Timing summary](summary.json) · [Worker identities](identity.json) ·
[Allocation](allocation-summary.json) · [Admission](admission-summary.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Patch, layouts and checks](procedures.tar.xz).
