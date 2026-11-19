# B1136 native integer transport against CPython

Lython `e20604ef4` (core SHA-256 `1e9d02fb…`) ran against stock CPython
3.13.16 on the quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3 on
November 19, 2026 UTC. The complete 32-file Lython image came from
[B1135](../2026-11-19-native-integer-transport/README.md) and passed the
unchanged validator golden. It reads 1,714 bytes of JSON, including six
malformed integer fields, and applies 96 guards. Ratios divide Lython
elapsed by CPython elapsed across all four worker-median pairings.

| Phase | Lython | CPython | Lython / CPython | Measurement status |
| --- | ---: | ---: | ---: | --- |
| Warm execution | 0.084–0.085 ms | 0.044–0.046 ms | 1.83–1.92× | Diagnostic. Controls L/C 1.13 / 3.76%. IQR ≤3.73%. |
| Compile-to-result | 2.199–2.243 ms | 2.025–2.072 ms | 1.06–1.11× | Diagnostic. Controls L/C 2.01 / 2.34%. IQR ≤3.93%. |
| Compilation only | 2.04878–2.08010 ms | 1.86838–2.00960 ms | 1.02–1.11× | CPython controls differ by 7.56%. Gap size remains uncertain. |

Warm includes mediated RAM-backed file I/O, golden checks and previous-result
cleanup. Compile-to-result adds compilation and compiled-owner disposal.
Compilation-only includes disposal of the compiled owner. CPython uses native
file APIs. Lython uses shared memory accounting and verifies allowance reuse.
Fixture reset, framing, process startup and final cache teardown use separate
boundaries. The warm external gap is smaller than in [B1130](../2026-11-19-postfix-cpython-checkpoint/README.md).
The internal source pair B1135 measures the effect of the transport change
with a common baseline renderer.

The 73-second collection completed 60 timed batches across all three phases
and passed both quiet gates. All workers joined, guest fixtures were removed
and the persistent archive was verified under lease.

[Measurements](summary.json) · [Raw observations](observations.tgz) ·
[Lease receipt](archive-receipt.json) · [Procedures](procedures.tar.xz) ·
[Measured core](../2026-11-19-native-integer-transport/measured-core.dll.gz).
