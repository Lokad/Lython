# Reuse scope validation for directive presence

B975 compared the d9234f876 runtime image `33025cee` with `643f3980` on
the Azure F4as_v7 VM on 2026-11-18. The 32-file images differ only in the
core DLL. All four scripts matched the existing CPython output and file
goldens before timing. The workers used .NET 11 RC3 and ordinary GC and tiering.

Scope validation now records whether each class or function body contains
scope directives. Abstract analysis uses that fact and skips invalidation
scans on directive-free classes. Function-summary eligibility already rejects
directives through its accepted statement shapes. Removing the separate
presence scanner and sharing the child-suite traversal deletes 33 net lines.
Directive ordering, nested binding, and conservative callback invalidation
keep their existing semantics.

| Script and phase | Elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| CSV invoice, compile | +0.02 to +1.89% | −1.05 to +0.83% | 1.46 / 0.40% |
| Text helper list, compile-to-result | +0.05 to +2.50% | −0.40 to +1.89% | 0.47 / 1.97% |
| JSON config edit, compile | −0.43 to +1.17% | −0.35 to +0.85% | 0.49 / 1.11% |
| JSON config edit, compile-to-result | −1.36 to −0.01% | −3.19 to +1.30% | 0.03 / 1.34% |
| Positional calls, warm control | −1.70 to +1.87% | −2.54 to +0.21% | 2.06 / 1.54% |

Ranges compare all pairings of two process medians per source. Negative favors
the candidate. Every process had eight seconds of rotated warmup and five
samples. The compile lane includes compiled-owner disposal. Compile-to-result
includes mediated RAM I/O, golden comparison and result and compiled-owner
disposal. Startup and warmup are separate. Process CPU also includes reset and
protocol. All five jobs ran in 232.58 seconds with verified quiet endpoints,
joined workers and released fixtures. No CPython timing was collected here.

Compile allocation moved by 0–160 bytes per command in the warmed local
diagnostic. Execution allocation medians and accounted peaks matched and
balances were restored. Both actual .NET targets passed 24 white-box and
192 public focused checks on Windows and the same 192 public checks in WSL.
The first freeze was stopped by its hash assertion before any worker copied or
ran. It pointed at the earlier prototype directory and was corrected to use
the retained source. The initial WSL shell invocation mishandled the test
filter and was corrected before running tests.

Retain the simpler traversal. CSV compilation and complete text elapsed
observations lean adverse. Control spread and mixed CPU leave their cost
unresolved, so carry that uncertainty to the next representative compiler
checkpoint. This pair establishes no substantial whole-script improvement.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Source and validation](checks-and-procedures.tar.xz)
