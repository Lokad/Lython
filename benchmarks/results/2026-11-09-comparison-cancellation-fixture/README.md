# Concurrent comparison cancellation fixture — R171

Full delivery CI42661297753/source220732b7 passed Ubuntu, both frameworks and
installed consumers, plus Windows ordinary/package checks. Windows .NET10 Stress
failed one comparison assertion: the comparison returned before cancellation was
requested. This demonstrates a scheduling assumption in the test, not a runtime
cancellation failure. Original source and failing CI logs are preserved.

The fixture now checks the exact comparison value if it completes before the
request; observed cancellation still requires its reason and source span. The same
large inputs are checked deterministically after cancellation. The original256MiB
inputs,1ms canceller,10s timeout, joined workers and exact ownership/refund checks
remain. No runtime, cancellation checkpoint, timeout or admission change.

A controlled isolated snapshot delays only the canceller to2000ms. Original tests
fail both equal/ordered cases with cancellation not requested at completion; the
corrected snapshot passes both, including the subsequent cancelled operation.
Normal source retains1ms. This reproduces the failed scheduling premise without
claiming deterministic interruption during a concurrent scan. Focused normal
Release scalar/native-construction checks pass7 each on actual.NET11/10.

[Original source](original.cs), [controlled confirmation](confirmation.json),
[isolated recipe](confirm.py), [original CI and both snapshot/focused logs](observations.tgz).
Full delivery [CI42667400495](https://github.com/Lokad/Lython/actions/runs/42667400495)
on e0522ef5 is green: Windows/Ubuntu actual.NET11/10, original Stress and installed
consumers. [Terminal receipt](ci-terminal.json) is cached; no unchanged replay.
This qualifies R167/R171, before the later R172 runtime correction. Publication
remains postponed.
