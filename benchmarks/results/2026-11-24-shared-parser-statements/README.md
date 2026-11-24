# B1612 Share parser statement handling

The measured patch against `533e13ebe` shares module and indented-suite
iteration, including recovery and the queued declarations from a split
import. The next statement's source span locates its physical line.
Comparing the previous endpoint with that line start replaces two
binary searches per eligible statement pair.

| Complete script | Boundary | Elapsed change | Wider process CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Configuration manifest | Compile | −1.22 to +1.87% | −1.00 to +1.62% | 1.26 / 1.85% |
| Sparse generated validator | Compile | −5.04 to −3.10% | −6.27 to −3.55% | 0.82 / 1.21% |
| Small configuration edit | Compile | −1.53 to −0.89% | −2.08 to −1.49% | 0.54 / 0.11% |
| Configuration manifest | Compile to result | −0.12 to +0.79% | +0.79 to +2.98% | 0.82 / 0.09% |
| Sparse generated validator | Compile to result | −3.52 to −1.69% | −3.97 to −1.30% | 0.89 / 0.99% |

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November
24, 2026 UTC. Two workers per source warm for eight seconds and provide
seven rotated batches. Ranges cover all four pairings of worker medians.
Compilation includes preceding-code cleanup. Compile to result includes
compilation, mediated RAM-backed I/O, output verification and compiled
owner and previous-result disposal. Process CPU also includes fixture
reset and request handling. Process startup and final teardown are
outside the guest clocks. This collection compares Lython revisions.

Retain the common statement loop and direct span use. Sparse validator
elapsed and CPU fall in both boundaries. The manifest's time-to-result
CPU increases while elapsed spans zero. Compilation allocation and
retained admission match on all three cases. The initial local micro
comparison remains in the procedures. Its identical-image control and
large batch spreads leave its timing inconclusive.

Windows and Ubuntu each pass 944 focused Release checks. Both frozen
images pass three complete CPython 3.13.2 goldens locally. Comparison
checks match diagnostics and source ranges on 108 inputs, and match
tapes and compilation metadata for 96 code objects in 16 scripts.
Every compilation owner refunds its allowance. All 140 timed batches,
both quiet gates, joined cleanup and the persistent archive verify.
Three previously archived VM cores were losslessly compressed after
their identities, archive custody and joined users were verified.

## Earlier declaration-discovery trial

A separate candidate reused the syntax walk to collect outermost
functions before local-binding flow. Nine representative shapes visit
2–10 statements in the removed discovery walk. A function-free class
fixture visits 101 and allocates 2,000 fewer bytes. The manifest adds
136 bytes. Retained admission matches and both platforms pass 266
focused checks. The first regression used CRLF source offsets for a
normalized span. The corrected test and original condition are
archived. Keep the prototype private because the expensive local-flow
work remains. No timing comparison was run for this trial.

[Timings](summary.json) · [Images](identity.json) ·
[Compilation allocation](allocation-summary.json) ·
[Quiet observations](observations.tar.xz) · [Probes and patches](procedures.tar.xz) ·
[Storage custody](storage-custody.json) · [Terminal status](terminal-status.json).
