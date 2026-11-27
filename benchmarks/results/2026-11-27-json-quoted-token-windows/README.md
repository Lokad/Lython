# B1872 Scan ASCII runs and write plain JSON tokens together

This trial on `43ef47bba` scans ordinary ASCII with a byte search and emits
each complete plain quoted string in one admitted window. The initial
implementation also searches for the next run after every escaped byte or
decoded scalar. It keeps strict UTF-8, bounded scanning and the existing
encoder chunk boundaries. The earlier B1585 trial changed scanning without
coalescing the output writes. [B1585](../2026-11-24-json-ascii-runs/README.md).

Only the core DLL differs in the 32-file images. Both match independent
CPython 3.13.2 goldens on the default export, an escape-heavy export and
the unchanged nested document. Both exports load 768 records from the
same 47,361-byte input. They write 54,272 and 75,008 bytes respectively.
Both Windows Release targets and Ubuntu WSL .NET 10 pass 1,034 public
and 232 white-box checks each. These include boundary-spanning scalars,
strict decoding, retained results, refusal, callback order and cleanup.

Managed allocation changes by at most 118 bytes per complete command and
all three admitted peaks match. A separate 96-byte plain root string saves
256 managed bytes and lowers peak admission from 2,372 to 2,276 B in both
APIs. That short case demonstrates the removed replacement growth. The
compound exports use different capacity boundaries.

## Local warm timing

Ubuntu WSL, .NET 11 RC3. Two workers per image warm for five seconds and
contribute seven rotated batches. Intervals span pairings of process medians.
Elapsed includes RAM-backed I/O, output verification and previous-result
cleanup. CPU also includes fixture reset and request framing. Both clocks
exclude compilation and startup. All 112 batches verify and workers join.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Default export | Sync | −7.09% to −3.10% | −7.48% to −1.79% |
| Nested read control | Sync | −4.27% to +4.17% | −7.22% to +1.84% |
| Escaped export | Async | −2.54% to +2.15% | +1.61% to +4.68% |
| Default export | Async | −7.77% to +0.27% | −5.83% to +1.19% |

Keep this variant out of delivery. The default sync export uses less CPU
in every pairing, but escaped CPU rises with under 2% same-image spread.
The unchanged read control and async default timing span zero. One
refinement is justified by the plain-string storage result. Keep the
initial span proof and complete write, then resume the scalar loop at
the first special byte. That removes the repeated searches on escaped text.

[Timing](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Image identity](identity.json) · [Observations](observations.tar.xz) ·
[Patch, allocation detail and checks](procedures.tar.xz).
