# B1158 direct class header handoff

Class statements now use executable header evaluation wherever their
containing body is executable. Explicit bases and keywords go from
operand slots into the final arrays. Starred headers borrow their
already-expanded arguments. The change removes the captured-expression
wrappers, cloned declaration and second expansion at yielding headers.
Empty headers need no accumulator. Generic headers still evaluate in the
type-parameter scope, and lowered class suites share class completion.

The reference is `0bb3783a2`. The measured and retained revision is
`c3f229508`. Windows .NET 11 and Ubuntu .NET 10 each passed 1,216 public
and 150 white-box focused Release checks. The new yielding-header test
returns `header True` on CPython and both Lython APIs. The prior Lython
core returns `header False` because rebuilding the arguments dropped
the original expanded key. Tests also cover real async work, cleanup,
admission refusal, generic headers and subclass-hook order. Both 32-file
images matched four CPython goldens.

The first prototype changed only yielding-header transport. The two
ordinary class scripts showed little allocation movement on that
revision. The final source adds executable headers to ordinary classes.
The procedures retain the initial diagnostics and the superseded
assembly test that expected a fallback.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 20,
2026 UTC. The inherited dataclass case defines 48 subclasses. The registry
creates 256 decorated classes with expanded bases and options. Warm includes
mediated RAM-backed I/O where used, output validation and prior-result
cleanup. Compile-to-result adds compilation and compiled-owner disposal.
Fixture reset and process startup are outside guest elapsed. Process CPU
covers the wider batch. The ranges compare duplicate process medians.

| Workload | Phase | Guest elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Inherited dataclasses | Warm | −8.06…−0.01% | −7.24…−2.21% |
| Expanded component registry | Warm | −3.93…−2.65% | −4.82…−2.66% |
| Annotated rules control | Warm | −1.54…+2.90% | −0.63…+2.90% |
| CSV invoice control | Warm | −1.24…+1.60% | −1.36…0.00% |
| Inherited dataclasses | Compile-to-result | −0.16…+0.02% | +0.22…+3.64% |
| Expanded component registry | Compile-to-result | −1.71…+0.41% | −1.43…0.00% |

Warm allocation falls 1.99% for inherited dataclasses and 5.72% for
the registry. Their accounted peaks fall by 1,176 and 48 bytes. The
compile-only allocation diagnostic rises 2.26% and 1.60%. Both controls
have effectively flat allocation and the same accounted peaks.
All runs return their shared allowance to baseline.

Retain the simpler class path and the registry warm improvement. The
inherited dataclass warm candidate spread is 7.44%, leaving the elapsed
effect size uncertain. Its compile-to-result CPU increase remains a
cost to track. The controls and registry compile-to-result elapsed are
inconclusive. This is a Lython source-pair comparison with CPython
correctness goldens.

The class work exposed a separate compiler fact bug, fixed at `9f5f85efd`.
A subclass hook can mutate a list or dictionary through an alias. Before
the correction, the compiler diagnosed later reads using the old empty
shapes. Both execution APIs now accept the regression case. The correction
widens collection facts in one pass at the class boundary. In an isolated
diagnostic against `0bb3783a2`, warm allocation was unchanged for the four
class and control scripts. Compile allocation changes ranged from −0.28%
to +0.62% across five cases. No elapsed or CPU measurement was made for
that correction alone. Its patch and checks are in the procedures archive.

All 120 measured batches and both quiet gates passed. Workers joined,
fixtures were removed and the persistent archive verified under the lease.

[Summary](summary.json) · [Observations](observations.tgz) ·
[Lease receipt](archive-receipt.json) ·
[Source, checks and procedures](checks-and-procedures.tar.xz).
