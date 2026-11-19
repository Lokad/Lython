# B1058 shared lexical and generic membership during compilation

R627 removes flattened ancestor-name sets from executable compilation.
Child builders query the live compiler parents and reuse the frontend's
type-parameter name set. Globals and active comprehension bindings preserve
their existing precedence. The measured `580bfa013` and retained `d4b2088d4`
have identical source and tests. The reference is `f80035c13`.

Windows .NET 11 and WSL .NET 10 each passed 902 focused Release checks.
Both complete 32-file worker images passed ten CPython goldens, including
96 type parameters shadowing an outer name through a nested helper.
Local public compilation allocation fell 1.10–3.23% on the four rule
workloads and 4.15% on the generic selector. Allowance returned to baseline
after every compiled-owner disposal. Warm allocation and accounted peaks
stayed effectively equal.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-19 UTC.
Ranges cover all four pairs of process medians across duplicate same-source
controls. Negative values favor the retained compiler.

| Workload | Boundary | Guest elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Captured rules | Compilation | −3.78…−2.17% | −3.54…−0.72% |
| Captured rules | Compile-to-result | −0.11…+0.77% | −1.96…+0.65% |
| Generic rule selector | Compilation | −3.77…−1.03% | −3.53…−2.09% |
| Mutable counters | Compile-to-result | −1.16…+3.25% | −3.36…+2.94% |
| Generated validator | Compile-to-result | −1.83…−0.95% | −2.32…+0.00% |
| Ordinary rules | Compilation | −1.14…+1.02% | −1.07…−0.24% |
| Shipment selector report | Warm control | −4.16…−0.40% | −3.03…−1.92% |

The reduced compiler work improves captured and generic compilation.
Captured compile-to-result spans zero. Mutable compile-to-result remains
uncertain with more than 2% spread in each same-source pair. The earlier
shipment slowdown did not recur. Its unchanged warm control moves favorably
in this run, so the result supports retention without crediting the compiler
change with a warm speedup. Generated-validator CPU is flat to lower on
this source pair.

Both quiet gates and all 140 timed golden batches passed in 320.66 seconds.
Workers joined, guest fixtures were removed and the archive verified under
the lease. Compilation includes compiled-owner disposal. Compile-to-result
also includes execution, mediated I/O and cleanup. Warm includes mediated
I/O, goldens and previous-result cleanup. Process CPU has a wider boundary.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Checks and procedures](checks-and-procedures.tar.xz) ·
[Earlier prototype and adverse warm control](../2026-11-19-borrowed-compiler-bindings/README.md).
