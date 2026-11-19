# B1072 direct singleton capture

Private `57d3e8f39` retains the actual cell as a singleton capture list,
with the existing array for multiple captures. The change removes its
per-function array allocation and charge. It preserves the charge for
the cell itself, first capture publication, namespace owners and shared
rebinding. The reference is `12fabffb1`.

Both complete 32-file images passed five CPython goldens. Windows .NET 11
and WSL .NET 10 each passed 587 focused Release checks, including retained
nonlocals, namespace-backed closures, generators, identity and memory
refusal. Local allocation falls 6.39% on retained siblings and 2.76% on
captured rules. Their accounted peaks fall 28,800 B and 163,840 B. Both
metrics are unchanged on mutable counters. All budgets refund to baseline.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-19 UTC.
Ranges cover all four pairs of duplicate process medians. Negative changes
favor the prototype.

| Workload | Boundary | Guest elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Retained siblings | Warm | −5.54…−1.66% | −5.92…−2.25% |
| Retained siblings | Compile-to-result | −3.59…−1.37% | −3.40…−2.47% |
| Captured rules | Warm | +2.56…+5.53% | +2.65…+5.88% |
| Mutable counters | Warm | −1.30…+0.59% | −1.99…+1.68% |
| Annotated rules | Warm | −1.42…+0.29% | −0.78…+0.02% |
| Expanded forwarding | Compile-to-result | −0.76…+0.99% | −1.12…−0.67% |

Retained siblings improve. Captured rules regress on all pairs, with
same-source elapsed spreads below
1.49%. Keep this interface-backed shape private. Measure whether the
singleton representation changes hot capture loads before selecting a
refinement. Preserve the measured allocation and peak reductions as a
separate result from CPU and elapsed effects.

Both quiet gates and all 120 timed goldens passed in 275.30 seconds.
Workers joined, guest fixtures were removed and the archive verified under
the lease. Warm includes mediated I/O, goldens and previous-result cleanup.
Compile-to-result adds compilation and compiled-owner disposal. Process
CPU has a wider boundary.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Checks and procedures](checks-and-procedures.tar.xz) ·
[Prior attribution](../2026-11-19-retained-call-profile/README.md).
