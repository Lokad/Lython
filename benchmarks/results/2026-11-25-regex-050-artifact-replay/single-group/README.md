# Optional adapter single-group development replay

The private adapter now uses caller slots for one-shot matches with zero
or one capture group. With one group, its participation determines
`lastindex`. This also preserves the last successful capture of a repeated
group, a missing optional capture, and a named lookbehind capture outside
the selected match window. Matches with multiple groups still use the
backend's last-close metadata.

The comparison changes only the adapter DLL after the
[whole-match bridge](../value-match/README.md). The Lython core and
`0.5.0-dev.lython.6edc01772` producer binaries match exactly. Both images
match six complete CPython 3.13.2 goldens.

| One-group log search, local warm allocation | Previous | Candidate | Reduction |
| --- | ---: | ---: | ---: |
| Compiled pattern | 3,388,758 B | 1,938,656 B | 42.79% |
| Bound method | 3,487,140 B | 2,037,150 B | 41.58% |
| Module call | 3,585,356 B | 2,134,974 B | 40.45% |

The measurement uses Windows .NET 11. Allocation is identical for the
two-group and invoice controls. The no-group log search allocates 176 B
more per invocation. All admitted peaks match and budgets return to
baseline. Neither elapsed nor process CPU is measured.

All 520 focused Release checks pass on Windows .NET 11 and Ubuntu
.NET 10. They cover in-flight cancellation and refused working storage
on the direct and cursor paths. The test and cancellation worker DLLs
match the development package for each target. The core has no producer
dependency. The production adapter keeps its existing pin.

[Allocation](allocation-summary.json) · [Image identities](identity.json) ·
[Assembly verification](verification.json) · [Procedures and results](procedures.tar.xz).
