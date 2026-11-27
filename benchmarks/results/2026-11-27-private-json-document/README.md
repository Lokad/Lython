# B1907 Release private JSON file input after decoding

A successful built-in `json.load()` of a native text file now releases
the document's private charge. A returned token that shares the UTF-8
backing keeps that backing charged through the existing string ownership.
Failed loads and custom sources or decoder classes retain normal ownership.
The exact text-file path calls its existing synchronous or asynchronous
read directly.

Both APIs complete six loads of a padded 20 KiB scalar within one 140 KiB
allowance and return that allowance to baseline. A comparable 48 KiB
document stays funded when a returned token shares it. The four new cases
fail on the measured reference and pass with the change. Focused Release
checks pass on Windows .NET 11 (1,400 checks), Windows .NET 10 (296) and
Ubuntu WSL .NET 11 (296).

Against production `a159f24f2`, the dense JSON document and configuration
edit allocate 288 fewer CLR bytes per invocation in each hosting mode.
Dense JSON's admitted peak falls from 2,855,754 to 2,854,734 B.
Configuration and the unchanged CSV control have equal peaks. All
measured invocations return the borrowed allowance to baseline.

The November 27, 2026 UTC Ubuntu WSL comparison verifies three CPython
3.13.16 goldens and 112 matched timed batches. Only the core DLL differs
between the verified 32-file images. Warm elapsed includes mediated I/O,
verification and previous-result disposal. Process CPU also includes
fixture reset and request framing. Ranges span pairings of worker medians.

| Workload | Mode | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Dense JSON | Run | −2.00% to +3.44% | −3.98% to +0.89% |
| Configuration edit | Run | −3.16% to +6.65% | −5.02% to +3.79% |
| Dense JSON | RunAsync | +0.78% to +16.78% | −2.51% to +6.22% |
| File-fed CSV control | RunAsync | +11.52% to +22.93% | +14.06% to +18.34% |

Dense JSON async elapsed moved adversely, and the unchanged CSV control
moved further in the same direction. The timing is inconclusive. Keep
the change for its demonstrated ownership behavior.

[Image identity](identity.json) · [Allocation and admission](allocation-summary.json) ·
[Timing summary](summary.json) · [Raw observations](observations.tar.xz) ·
[Source patches and focused checks](procedures.tar.xz).
