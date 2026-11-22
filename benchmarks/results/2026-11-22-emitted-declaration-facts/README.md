# Prepare closure captures from emitted declarations

Compared `bf4b22764` with private `e73b2e544` on Windows .NET 11 RC3 on
November 22, 2026 UTC. The compiler records actual declarations in its
reusable builder buffers. Capture preparation iterates those declarations
in named-function, lambda and generator order. This avoids scanning the
constant table while adding scratch references for declarations.

| Source | Local compile allocation change | Diagnostic elapsed change |
| --- | ---: | ---: |
| Sparse validator | −0.808% | −11.56 to +7.76% |
| Postponed nested factories | +0.465% | −25.80 to +47.59% |
| Inherited dataclass configuration | +0.225% | −28.55 to +26.63% |
| Configuration manifest | +0.090% | −9.42 to +4.56% |
| Generated read-only branches | +0.011% | — |
| Rule defaults | +0.307% | — |
| Captured rules | +0.407% | — |
| Closure `locals()` order | +0.682% | — |

The allocation probe measures the compiling thread. The elapsed probe
rotates four independent processes in reference, candidate, candidate,
reference order. Ranges use every pairing of process medians across
seven batches. Each invocation includes compiled-owner disposal and
checks the allowance returns to baseline. Guest execution, I/O and process
startup sit outside that boundary. Local worker spreads leave all elapsed
results inconclusive. This was a local diagnostic without quiet-VM timing.

Keep the candidate private. The simplified discovery trades scans for
storage and raises allocation on most controls. A new profile that places
declaration scans in a substantial critical path, or a way to reuse
already-owned storage, would justify refinement or quiet confirmation.

Both 32-file worker images pass eight CPython goldens. Inspected metadata
matches in all 81 code objects, including the previously sensitive
[forwarded-names ordering case](../2026-11-19-child-declaration-preparation/README.md).
Windows .NET 11 and Ubuntu .NET 10 each pass 734 focused Release checks.
The initial sparse worktree lacked the representative scripts. The
corrected runs restored those fixtures and preserve all alias, cancellation
and budget assertions. The archive includes both initial failures.

[Source and images](source.json) · [Local observations and checks](observations.tar.xz).
