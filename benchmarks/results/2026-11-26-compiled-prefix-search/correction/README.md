# B1814 compiler correction and delivery check

A negative operand can encode a global as well as a constant. Check the
literal index before marking the loop. The regression errors during
compilation in the first prototype and returns `4` in the corrected
build and CPython 3.13.2. Windows .NET 11 and Ubuntu .NET 10 each pass
118 white-box and 858 public focused Release checks.

The measured candidate core matches the Windows probe and public test
builds. Its 32-file image passes all three independent CPython output
and file checks. The allocation and admitted peak medians match the
[parent experiment](../README.md) in both APIs, including restoration
of the shared allowance after each run.

The 84-batch local WSL repeat uses the same workers, goldens and timings
as the parent experiment, limited to the affected warm asynchronous lane.
Negative changes mean less time than the same Lython reference.

| Workload | Warm async elapsed change | Process CPU change |
| --- | ---: | ---: |
| Match at line 1,025 | −54.29% to −52.82% | −45.31% to −43.24% |
| Unicode file-report control | −4.75% to −0.70% | −6.06% to −0.64% |
| Match at line 1 | +0.52% to +5.91% | −4.46% to +3.34% |

Retain the corrected implementation for the late-match allocation and
admission reductions and the repeated elapsed and CPU gains. Early-match
elapsed remains higher, with mixed process CPU and larger worker spread
in the repeat. The Unicode control is faster in the repeat, which limits
attribution there. The early-match cost remains open for a quiet check.

[Summary](summary.json) · [Frozen image](identity.json) ·
[Observations](observations.tar.xz) · [Archive receipt](archive-receipt.json) ·
[Source patches, parity and Release checks](qualification.tar.xz).
