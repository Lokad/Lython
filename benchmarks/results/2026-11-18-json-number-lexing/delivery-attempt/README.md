# Original JSON and regex delivery attempt

[Run 44304492242](https://github.com/Lokad/Lython/actions/runs/44304492242) tested
55fe3da03. Ubuntu .NET 11 passed the ordinary suite. Ubuntu .NET 10 reported one
failed white-box test, `ActiveBackwardProjectionCancelsBeforePublishingTheCursor`.
All 18,054 public tests passed. The job stopped at this checkpoint.

The test signalled before one rewind and spun for a fixed interval. Its assertion
that the task was still running failed because the rewind had already completed.
The corrected fixture repeatedly runs the existing nonallocating rewind on a
smaller admitted subject. It signals after the first real rewind so the worker
stays active independently of a single scan's duration. It still requires
cancellation attribution at the supplied span, an unchanged cursor for the
interrupted attempt, cleanup and allowance reuse. Threads join before refund.
No production source changed in this correction.

The focused test passed locally on Windows for actual .NET 11 and .NET 10.
On Ubuntu under WSL, 79 focused regex and scalar-width checks passed on each
target using the same built test assemblies. Full GitHub delivery needs a
separate successful run of the corrected source.

The original run's Windows job passed both ordinary and original-scale
stress suites and installed Release package verification. The overall
original run failed at the recorded Ubuntu checkpoint.

[Terminal jobs](terminal.json) · [Complete CI logs](run-logs.zip) ·
[Original and focused logs](failed-and-focused-logs.tar.xz)
