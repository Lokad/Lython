# Original combined delivery and repetition fixture correction

The Ubuntu .NET 11 white-box lane of run 44328575041 failed one of
4,871 tests on d9234f876. The text repetition test measured 32,976
allocated bytes before refusal against a 32,768-byte bound. All 18,054
public tests passed. That lane skipped its later framework, stress and
installed-package checks after the failure.

The test now warms the out-of-line repetition path with a small operation.
It measures from inside the assertion harness after setting up the refusal
delegate. The same allocation bound remains. Checks also confirm that the
refusal is for the large repeated storage and that its callback ran on the
measured thread. Original reason, source span, budget balance and shared
scope cleanup assertions remain.

All 18 focused repetition tests passed on Windows and under WSL on each
actual .NET target. The correction changes only the test. The original
allocation observation and source remain available. A corrected full
Windows/Ubuntu delivery is a separate run.

[Original CI](https://github.com/Lokad/Lython/actions/runs/44328575041) ·
[Failed lane and focused checks](failed-lane-and-local-checks.tar.xz)
