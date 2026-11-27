# B1856 Reuse the comprehension binding proof

The enclosing scope already walks comprehension expressions to collect
outward assignments. It records when none are present. Generator scope
preparation skips creating and filling an empty name set, and static name
diagnostics reuse the same proof after analyzing the comprehension.
Subtrees with assignment expressions keep their ordinary binding walk.
Lambda bodies keep their own scope and lambda defaults still bind in the
enclosing scope.

Windows passes 2,328 public and 311 white-box focused Release checks on
each target. Ubuntu WSL .NET 10 passes 78 public and three white-box
checks. A new independent CPython expectation covers a walrus inside a
lambda default in a partially consumed generator. A compiler probe checks
identical executable tapes, retained admission, refusal and cancellation
results across 16 scripts and 127 allowance levels. Its first launch looked
for the image identity in the wrong directory. The corrected launcher uses
the same verified images and preserves the original failure log.

## Allocation and admission

The generated report has 96 rules, 193 comprehensions and 20,988 bytes of
source. Both complete frozen images match three independent CPython output
and file goldens on Windows and on the VM. Only the core DLL differs. In
the Windows .NET 11 compile-and-dispose diagnostic, the report drops from
5,262,064 to 5,186,800 managed bytes per compile, a reduction of 75,264 B
or 1.43%. The ordinary definitions control allocates 1,443,248 B in both
images. Retained compilation, peak compilation, warm allocation and peak
execution admission match on the tested cases. Every owner returns its
allowance to baseline.

## Quiet timing

November 27, 2026 UTC on Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3.
Two workers per image warm for at least five seconds each and supply seven
rotated batches. Ranges span pairings of worker medians. Compile-only
includes disposal of the previous compilation. Compile to result includes
execution, verification and cleanup. The synthetic report has no host file
I/O. Process CPU also includes fixture reset and request framing. Startup
sits outside both clocks.

| Workload | Boundary | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Generated comprehension report | Compile only | −1.00% to +0.61% | −2.22% to +0.10% |
| Ordinary definitions control | Compile only | −0.74% to +1.82% | −3.26% to +2.46% |
| Generated comprehension report | Compile to result | −4.80% to −3.74% | −6.85% to −3.11% |

Retain the change. It removes repeated work and lowers managed compilation
allocation. The improvement holds across worker pairings for the complete
report, while compile-only timing remains inconclusive. All 84 batches and
both quiet gates pass. Staging reuses 32 reference files and 31 candidate
files from B1855. All workers join, the RAM-backed fixtures are removed and
the persistent archive verifies.

[Compiler parity and allocation](compiler-parity.json) · [Fixture counts](fixture-census.json) ·
[Timing summary](summary.json) · [Execution allocation and admission](allocation-summary.json) ·
[Image identity](identity.json) · [Observations](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Patch and checks](procedures.tar.xz).
