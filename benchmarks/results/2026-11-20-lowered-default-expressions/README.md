# Lowered default expressions

`a9f0724e2` removes the lowered parameter records from ordinary functions
and lambdas. The shared layout supplies names, kinds and default positions.
Lowering retains the actual default expressions in the order they execute in
the enclosing scope. Executable instructions and lowered sync and async
evaluation use the same expressions.

The Windows allocation comparison uses `07a88e0ed` and source-identical
`baa34fb67`. Compilation includes synchronous public compile and code-owner
disposal using a shared allowance. Tiered compilation was disabled for both
compile-allocation measurements to remove observed tier-transition variance.

| Script | Reference bytes per compile | Candidate bytes per compile | Change |
| --- | ---: | ---: | ---: |
| Ordinary rule closures | 79,424 | 78,840 | −0.74% |
| Eager annotated rules | 57,736 | 57,392 | −0.60% |
| Generated validator | 3,048,638 | 3,048,494 | <0.01% lower |
| Manifest | 286,072 | 285,232 | −0.29% |
| Positional invoice | 168,952 | 168,616 | −0.20% |
| Inherited dataclasses | 644,336 | 644,336 | Flat |

Both complete frozen images matched six CPython goldens. Windows .NET 11
and Ubuntu .NET 10 each passed 1,039 focused tests. Warm allocation matched
except for 120 more bytes in the inherited dataclass run. Accounted warm peaks
matched and every run returned the borrowed allowance to baseline.
The deletion simplifies the retained representation. The absolute savings
are small and this comparison supplies no CPU or elapsed result.

[Allocation summary](summary.json) · [Source, observations and checks](diagnostics.tar.xz).
