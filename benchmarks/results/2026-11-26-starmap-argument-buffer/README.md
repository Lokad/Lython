# B1793 Share starmap's admitted argument construction

The source patch on `307285aa1` replaces the separate synchronous
argument-list drain and asynchronous value-list drain for general
iterable rows. Rows with up to three items use the existing short
callback operand slots. Wider rows move the prefix into the existing
admitted call accumulator and append subsequent values there. Span
callables consume its active values. Array-only callables still receive
an exact packet. The input cursor finishes before the call, and argument
funding stays live while the source or callback is suspended.

The two list-row fixtures use the existing 2,048-record, 27,350 B shipment
input and output. Each formats one three- or five-item list per policy
selection. The original zip-to-starmap report uses the same input and
supplies a control for the existing exact zip path. The CSV meter is a
separate allocation and correctness control.

| Complete script | CLR allocation before | CLR allocation after | Admitted peak before / after |
| --- | ---: | ---: | ---: |
| Three-item list rows | 2,145,630 B | 1,572,016 B | 3,467,273 / 3,467,109 B |
| Five-item list rows | 2,719,582 B | 2,588,454 B | 3,795,865 / 3,796,161 B |
| Existing zip-to-starmap control | 1,100,516 B | 1,100,454 B | 2,822,785 / 2,822,785 B |
| CSV meter control | 991,808 B | 991,808 B | 5,171,785 / 5,171,785 B |

The allocation diagnostic runs synchronously on Windows .NET 11.
Allocation falls 26.73% for three items and 4.82% for five. The
existing zip control differs by 62 B. Every allowance returns to
baseline. Both full 32-file images match four independent CPython
goldens. Windows .NET 11 and Ubuntu .NET 10 each pass 279 focused
Release checks. CPython 3.13.2 reproduces the wide generator's prefix,
argument order and retained alias. A suspended-source regression
checks cancellation, one close, callback suppression and allowance reuse.
[Allocation](allocation-summary.json).

## Local diagnostic

The local Ubuntu WSL block uses the same frozen images, two processes
per source and 112 batches. Negative values indicate less time. Ranges
span all four pairs of process medians.

| Workload | Warm lane | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Three-item starmap list rows | Async | −8.39% to +2.19% | −6.78% to −0.10% |
| Existing zip-to-starmap control | Async | +5.60% to +13.41% | +5.56% to +13.62% |
| Three-item starmap list rows | Sync | −9.08% to −1.76% | −8.36% to +0.27% |
| Five-item starmap list rows | Async | +7.24% to +26.12% | +9.49% to +26.98% |

The five-item candidate's CPU spread is 14.48% locally. The zip control
also moves adversely. The local observations and conditions remain in
the procedures archive.

## Quiet repeat

The reordered comparison ran November 26, 2026 UTC on Azure F4as_v7,
Ubuntu 24.04.4 and .NET 11 RC3. Two workers per source warm for five
seconds each and supply seven rotated batches targeting 440 ms.
Elapsed covers mediated RAM-backed I/O, output checks and previous-result
disposal. Process CPU also covers fixture reset and request framing.
Compilation and startup are outside these phases.

| Workload | Warm lane | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Three-item starmap list rows | Sync | −6.19% to −3.64% | −4.52% to −1.77% |
| Five-item starmap list rows | Async | +7.54% to +14.67% | +4.30% to +15.08% |
| Three-item starmap list rows | Async | −5.68% to −0.23% | −7.17% to +1.57% |
| Existing zip-to-starmap control | Async | −1.46% to +1.81% | −3.59% to +1.79% |

Three-item rows take less elapsed time in both modes. Sync CPU also falls
1.77–4.52%. Five-item async elapsed rises 7.54–14.67% and CPU 4.30–15.08%.
The zip control spans zero in the quiet repeat. Keep this version out of
production. Each five-item row starts with capacity four, then reserves and
copies its replacement on the fifth item. Test the existing live count
hint as a way to remove that growth. The comparison measures the whole
candidate and does not isolate growth as the cause of the increase.


All 112 quiet batches and both machine checks pass. Workers join and
guest fixtures are removed. The persistent observations archive verifies.
During staging, two byte-identical expanded observation directories were
removed after verification against the pushed and VM archives. The
frozen benchmark executable and configuration files reuse the
immutable donor files.

[Image identity](identity.json) · [Timing and spreads](summary.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Procedures, patches and local observations](procedures.tar.xz).
