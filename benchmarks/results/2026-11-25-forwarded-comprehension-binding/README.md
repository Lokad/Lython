# B1737 Forward a comprehension item into its expression

The trial retains one binding in the existing scope object for a straight-line
single-target list projection. The yielded operand goes straight into the item
expression. Its scope remains visible to `locals()` during custom iterator
pulls. Each shipment `tally` projection replaces a named store and reload with
one slot-aware binding operation. The helper uses 71 instructions instead of
73 with the same 21 value slots. Ordinary single-item display append bypasses
the suspension wrapper. Expansion can still suspend.

| Workload | Phase | Elapsed change | Process CPU change | Same-source elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Shipment callback report | Warm sync | −6.23% to −4.33% | −5.26% to −3.85% | 0.87% / 1.13% |
| Shipment callback report | Warm async | −6.78% to −1.40% | −6.77% to −1.16% | 2.38% / 3.31% |
| Shipment callback report | Compile-to-result | −11.96% to +2.31% | −10.23% to +0.68% | 5.88% / 9.75% |
| File-fed CSV quantity map | Warm async | −2.19% to +3.46% | −1.96% to +1.82% | 1.51% / 4.21% |
| Unicode text report | Warm sync | −8.79% to +3.00% | −8.14% to +1.79% | 5.81% / 6.73% |

The reference is `4e8e60923`, whose production code is `7f0c5b996`.
The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 25,
2026 UTC. Each workload and phase uses two processes per verified image,
seven seconds of warmup per process and seven rotated batches. The ranges
span the four pairings of process medians. Negative means the candidate
uses less time. Elapsed includes any RAM-backed guest file I/O, result
verification and disposal. The combined phase also includes compilation
and its cleanup. Wider CPU includes request framing and fixture reset.

Shipment allocates 19,712 B less during execution and 1,192 B less during
compilation. Peak execution admission and compiled-owner admission match
for all four diagnostics. Both Release targets pass 917 focused checks each
on Windows .NET 11 and Ubuntu .NET 10. Four independent output and file
goldens pass per frozen image. The scope observer exposes a shadowed local
through `__next__ = staticmethod(locals)` before it is bound. The attempted
row lookup raises `KeyError`, and the outer local is restored.

The first prototype passes semantic checks and measures lower shipment
warm CPU in both hosting modes. Time through result and both unchanged
controls span zero. Source review found that cancellation during the new
compiler selection loop needs the structured compiler path. This image
still uses the raw token exception there. The corrected source requires
its own focused validation and paired timing evidence.

[Corrected block](confirmation/README.md) · [Retained isolation](isolation/README.md) ·
[Identity](identity.json) · [Tape shape](shape-summary.json) ·
[Execution allocation](allocation-summary.json) · [Compilation allocation](compile-allocation-summary.json) ·
[Timing summary](quiet-summary.json) · [Raw observations](quiet-observations.tar.xz) ·
[Terminal receipt](quiet-terminal-status.json) · [Procedures](procedures.tar.xz).
