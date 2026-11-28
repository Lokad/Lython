# B1938 Remove the monitor from private regex match allowance

The optional adapter supplies one private working allowance to each
one-shot match. The pinned PythonRe 0.5.0 call completes matching,
capture copies and working cleanup before it returns. This includes the
cursor and retained snapshot used within a multi-group one-shot match.
Remove the adapter's extra monitor and its object from that lifetime.
Retained program and cursor allowances still serialize their own
aggregation. All reservations and refusals still go through the
borrowed Lython budget.

Only the optional adapter DLL differs between the 32-file reference
and candidate images. Both use the signed 0.5.0 producer assemblies.
The reference source is `23dc25095`. Focused Release passes 716 public
and 102 white-box checks on Windows with .NET 10 and 11 and Ubuntu WSL
with .NET 11. The checks cover actual provider matching, concurrent
commands using a shared compiled pattern, cancellation, capture
lifetimes and refusal before publication. Both worker images verify
all four CPython 3.13.16 output and file goldens.

## Allocation

The Windows .NET 11 witness measures 73,728 B less per compiled log
scan in each API, or 24 B per input line. The module log scan saves
73,784 B in `Run` and 73,672 B in `RunAsync`. The Unicode cursor
and first-line file control have identical allocation. All compiled
and execution admission peaks match, and every result returns its
borrowed allowance to baseline after disposal.

## Warm execution

The November 28, 2026 local Ubuntu WSL run uses .NET 11 RC3 and one
logical CPU. Two workers per image warm for five seconds and supply
seven rotated batches each. The ranges span pairings of their medians.
Elapsed includes applicable RAM-backed I/O, verification and prior
result disposal. Process CPU also includes framing and fixture reset.
Compilation and startup are outside both clocks. All 112 batches
verify and all workers and fixtures are retired before archival.

| Workload, `Run` | Elapsed change | Process CPU change |
| --- | ---: | ---: |
| Compiled log search | −9.86% to −4.15% | −7.51% to −6.04% |
| Module log search | −6.69% to −2.15% | −6.06% to −2.27% |
| Unicode named-group cursor | −1.91% to +0.36% | 0.00% to +1.63% |
| First-line file search | −4.40% to +1.31% | −1.90% to +3.21% |

Retain the change for the removed allocation and the measured CPU
reductions in both one-shot log scripts. The compiled reference
workers differ by 5.28% elapsed and 0.58% CPU. The cursor control's
elapsed spans zero and its CPU ranges from unchanged to slightly
higher. The existing quiet CPython ratios retain their original
revision. [B1932](../2026-11-28-current-cpython-regex-checkpoint/README.md).

[Timing](summary.json) · [Allocation](allocation.json) · [Producer bytes](producer.json) ·
[Image identity](identity.json) · [Observations](observations.tar.xz) ·
[Source patch and procedures](procedures.tar.xz).
