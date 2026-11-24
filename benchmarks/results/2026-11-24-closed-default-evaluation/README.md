# B1590 Evaluate closed defaults on the direct definition path

Production `877463ac5` retains the source measured at `d40960ae`. The
compiler now proves two closed default expressions instead of matching a
seven-instruction factory. The eager `compile_rule` enters the existing
direct definition route before a native workspace is created. It evaluates
each default with one running value, uses the funded integer fallback for
overflow and preserves the original checks and default admission order.
Eager annotations and fresh callables still come from the ordinary
construction path. The postponed factory stays eligible under the new proof.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 24,
2026 UTC. The 32-file images at `af717101d` and `d40960ae` differ only in
the core DLL and both verify six CPython 3.13.16 goldens. Two workers per
image warm for eight seconds and supply seven rotated batches. Ranges are
across pairings of worker medians. Warm elapsed includes mediated RAM-backed
I/O, output verification and previous-result disposal. Wider CPU includes
reset and protocol. Compilation, startup and final teardown sit outside
these clocks.

| Warm script | Elapsed change | Wider process CPU change | Elapsed spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Captured rules control | −1.11 to +6.52% | −1.23 to +7.26% | 2.15 / 5.45% |
| Defaulted rules control | −2.79 to +6.47% | −2.74 to +6.81% | 3.88 / 5.43% |
| Eager annotated rules | −43.34 to −36.64% | −43.67 to −36.66% | 9.68 / 1.95% |
| Postponed annotated rules | +0.14 to +5.85% | −0.71 to +6.50% | 2.99 / 2.63% |
| Short text report control | −7.03 to −0.89% | −6.82 to −1.56% | 3.02 / 3.48% |
| Quoted CSV invoice control | −1.63 to +1.71% | −3.20 to +1.87% | 1.49 / 1.88% |

Retain the eager improvement. It clears the largest observed worker spread
and is supported by CPU and 65,416 fewer CLR allocation bytes per run
(13.38%). The other allocation rows and all admitted peaks match. Every run
restores its allowance. The postponed elapsed increase is open regression
debt. Its CPU range crosses zero, and the moving text control limits
attribution of smaller changes. Captured, defaulted and invoice timings
leave uncertainty. This is a source comparison with no new CPython ratio.

Both platforms pass 276 public and 286 white-box focused Release checks.
Eight additional programs match CPython 3.13.2 on both frozen images and
cover signed arithmetic, overflow, defaults and annotation identities.
Refused admission stops before a division error on both entry points.
The division test uses normalized source offsets and preserves the exact
error span. The initial checkout-dependent expectation and independent
span probes are in the procedures. Both quiet gates and all 168 batches
verify, workers join, fixtures retire and the persistent archive validates.

[Timing and spread](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Compiled shape proof](definition-proof.json) · [Images](identity.json) ·
[Reference core](../2026-11-24-eager-json-owner/candidate-core.dll.gz) ·
[Observations](observations.tar.xz) · [Procedures and oracles](procedures.tar.xz) ·
[Terminal status](terminal-status.json).
