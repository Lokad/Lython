# B1384 Borrow prepared casing ranges during dictionary accumulation

The frozen `d45f2ed21` and private `64b4e6457` cores ran on
November 22, 2026 UTC on the quiet Azure F4as_v7 with Ubuntu
24.04.4 and .NET 11 RC3. Both 32-file images verified. Only the
core DLL differs.

The prototype replaces the inline UTF-8 copy of each short word with
an index into ranges owned by the active casing cursor. Dictionary
lookup reads the funded bytes while the loan stays active. The loop
publishes the final Python word and preserves key identity before
returning the loan or invoking the fallback. A focused ownership
test also materializes a short word after cancellation or refusal.
[Attribution](../2026-11-22-record-text-attribution/README.md).

| Complete script | Boundary | Elapsed change | Wider process CPU change | Elapsed spread reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Captured Unicode report, computed width | Warm | −5.16 to +2.02% | −4.95 to +1.50% | 5.78 / 1.69% |
| Captured Unicode report, counter | Warm | −3.79 to +0.44% | −4.89 to +0.79% | 1.79 / 2.57% |
| Map-based mixed Unicode report | Warm | −0.70 to +1.04% | −0.29 to +0.71% | 0.20 / 1.55% |
| Quote-heavy policy invoice | Warm | −3.04 to +0.39% | −3.37 to 0.00% | 1.42 / 2.09% |
| Captured Unicode report, computed width | Compile to result | −3.69 to +9.81% | −2.91 to +9.76% | 4.41 / 9.20% |

Ranges cover all pairings of two process medians per source after
eight seconds of full-request warmup per process and seven rotated
batches. Warm includes mediated RAM-backed I/O, verified stdout and
file bytes, and prior-result cleanup. Compile to result adds compilation
and code-owner disposal. Wider CPU includes worker reset and protocol.
Startup and final teardown are separate.

Keep the prototype private. The code eliminates a real copy, but the
change also makes word lookup depend on the cursor loan. The two target
warm ranges span zero. The first-use slowdown does not have consistent
support across candidate workers. Local allocation is effectively flat,
admitted peaks match and the shared allowance fully refunds. Consider
a bounded follow-up if independent evidence links the copy or the
first-use spread to a code path that changes the decision.

Both frozen images match four CPython 3.13.2 goldens. Windows .NET 11
and Ubuntu .NET 10 each pass 74 white-box and 330 public focused
Release checks. The first test build caught ownership tests calling
the old materializer. They now exercise the rewritten path while
retaining the original checks. All 140 timed batches verify. Both
quiet gates pass, workers join, fixtures are removed and the archive
verifies under the lease. Only byte-proven older staging replicas
were retired. Their canonical bytes remain archived.

[Timings](summary.json) · [Raw observations](observations.tgz) ·
[Local allocation](allocation-summary.json) · [Source and images](source.json) ·
[Procedures and tests](procedures.tar.xz) · [Archive receipt](archive-receipt.json).
