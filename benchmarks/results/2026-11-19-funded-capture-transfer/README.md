# B1096 transfer funded lexical cells

R666 keeps a captured cell's frame admission when it becomes retained. This
removes the second charge for the same cell and the preflight lookup pass for
prepared lexical captures. A capture array and new namespace-backed cells
still need admission. On an array refusal the frame remains the cell owner.
The exact comparison is `06b40a712` versus `f9953fb40`. The measured source and
test changes are identical to the retained `2ba3105c3` changes.

Windows .NET 11 and Ubuntu .NET 10 each passed 752 focused Release checks.
The two-cell regression checks alias identity, funding past frame cleanup and
terminal refusal before capture publication. The reference requested 240
bytes because it charged both cells again. The candidate requests 48 bytes
for the array. Both 32-file images and all six CPython goldens passed. Local
allocation is effectively unchanged. Captured rules peak 416 accounted bytes
lower and mutable counters 49 bytes lower. Budgets return to baseline.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 19, 2026.
Each range covers all four pairs of two process medians per image. Warm
elapsed covers mediated I/O, output checks and prior-result cleanup.
Compile-to-result adds compilation and compiled-owner disposal. Process CPU
includes a wider boundary.

| Workload | Boundary | Guest elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Captured rule closures | Warm | −3.76…−2.13% | −4.32…−2.72% |
| Retained nonlocal siblings | Warm | −13.54…+4.87% | −14.22…+3.63% |
| Mutable rule counters | Warm | −2.11…−1.15% | −2.56…+0.07% |
| Ordinary rule closures | Warm | −0.58…+2.83% | +0.42…+4.13% |
| Annotated rule closures | Warm | +0.61…+2.65% | +0.69…+3.82% |
| Expanded forwarding | Warm | +0.60…+0.99% | +0.41…+2.42% |
| Captured rule closures | Compile-to-result | −1.71…−0.91% | −3.16…0.00% |
| Retained nonlocal siblings | Compile-to-result | −2.26…+0.09% | −3.16…−1.59% |

Retain the simpler ownership path and captured-rule gain. Sibling warm
process medians diverge 14.10% on the reference and 6.30% on the candidate.
Keep that result inconclusive. The ordinary CPU and annotated and forwarding
elapsed and CPU regressions are open for a combined checkpoint.

The first local catalog was rejected by CPython before timing because it
carried a file digest for a no-file case. The corrected run used the archived
B1062 catalog without changing the case sources, fixture or stdout goldens.
All 160 samples, quiet gates, joins and persistent archive checks passed in
364.68 seconds. The checks archive includes the original verification
and test-fixture corrections.

[Summary](summary.json) · [Reports and procedures](checks-and-procedures.tar.xz) ·
[Raw observations](observations.tgz) · [Lease receipt](archive-receipt.json).
