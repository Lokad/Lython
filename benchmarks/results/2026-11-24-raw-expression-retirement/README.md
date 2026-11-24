# B1640 Retire raw expression evaluation

A C# symbol audit on both target reference packs found the same 27 raw
expression methods, with calls confined to the retired group. That
includes raw display, comprehension, formatting, binding and operator
dispatch. Lowered and executable code still share the live collection,
pattern, call and operator implementations. The test-only raw argument
adapter and its selectors are gone. Lowered argument expansion reads its
prepared records directly and reuses the current accumulator for keyword
mapping merges. The edit removes 723 net runtime lines.

White-box tests exercise the remaining paths for evaluation order, target
spans, keyword identity and errors, tuple expansion admission and unwind,
deciding comparison results and ASCII formatting. Existing public tests
exercise real suspension and cooperative cancellation, retained data and
shared allowance reuse in executable and lowered code. Each platform
passes 1,837 white-box and 7,402 public .NET 11 Release checks and 92
public .NET 10 checks. The first
migrated test expected a keyword value without the original string key.
It now asserts that the lowered result keeps the key identity. The first
Ubuntu filter also required the matching Release probe and an alias to
the existing `python3.13` installation.

The reference is `9e3e85e44`. The 32-file images differ only in the core
DLL and verify against the same three CPython workload goldens. The
generic-class diagnostic constructs 96 classes with starred bases,
keyword mappings, annotations and subclass hooks. The short report
and manifest keep their existing fixtures. Local execution allocation
for the generic-class workload drops from 894,528 to 877,632 B (1.89%).
Its peak admission stays at 478,369 B. Allocation and admission match
for both controls. Every diagnostic returns its allowance to baseline.

| Workload | Phase | Elapsed change | Process CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Generic class headers | Warm | −3.52 to −1.67% | −3.04 to −2.57% | 1.65 / 0.27% |
| Short Unicode report | Warm | −2.45 to +10.65% | −2.85 to +11.31% | 1.61 / 11.64% |
| Generic class headers | Compile to result | −2.46 to −0.83% | −2.72 to −1.19% | 0.16 / 1.51% |
| Configuration manifest | Compile to result | −1.97 to +0.12% | −4.22 to −1.32% | 1.46 / 0.67% |

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on
November 24, 2026 UTC. Timings span the four pairings of medians from
two workers per image. Each process contributes seven rotated batches
after at least five seconds of warmup. Positive values mean more time.
Warm elapsed includes applicable RAM-backed I/O, verification and
prior-result disposal. Time to result adds compilation and compile-owner
disposal. Process CPU also includes fixture reset and protocol.

The generic-class script uses less CPU and elapsed time in both phases.
Report timing remains inconclusive because the two candidate workers
differ. The manifest elapsed range crosses zero. Retain the reduced
allocation and the simpler execution path. All 112 timed batches,
quiet gates, joins, fixture cleanup and archive verification pass.

[Allocation](allocation-summary.json) · [Image identity](identity.json) ·
[Timings](quiet-summary.json) · [Raw observations](quiet-observations.tar.xz) ·
[Terminal status](quiet-terminal-status.json) ·
[Symbol audit, exact patch and procedures](procedures.tar.xz).
