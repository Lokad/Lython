# B1271 generated validator against CPython

Measured frozen Lython `406388271`, with the same production source
as `6e972677e`, against stock CPython 3.13.16 on November 21, 2026 UTC.
The Azure F4as_v7 runs Ubuntu 24.04.4 and .NET 11 RC3.
[Compiler source pair](../2026-11-21-sparse-abstract-invalidation/README.md).

The script reads 1,714 bytes of JSON, evaluates 96 generated validation
guards with successful integer parses, and writes the normalized result.
Both engines verify stdout and file bytes against the same goldens.

| Boundary | Lython | CPython | Lython / CPython |
| --- | ---: | ---: | ---: |
| Warm execution | 0.07360–0.07384 ms | 0.04048–0.04121 ms | 1.786–1.824× |
| Compilation alone | 1.415–1.436 ms | 1.770–1.781 ms | 0.795–0.811× |
| Compile-to-result | 1.572–1.573 ms | 1.953–1.979 ms | 0.795–0.806× |

Each boundary uses separate workers. Ranges cover all pairings of two
independently warmed process medians per engine. Within-engine control
spreads are at most 1.81%. The maximum elapsed IQR is 1.13%. The observed
warm gap coexists with a lower compile-to-result time than CPython on this
one script. This is a diagnostic comparison of the two frozen engines
and does not isolate the effects of individual compiler changes.

Warm elapsed includes guest execution, mediated RAM-backed file I/O,
golden observation and prior-result cleanup. Compilation alone includes
code-owner disposal. Compile-to-result adds compilation and code-owner
disposal to the execution boundary. Process startup, fixture reset and
final teardown sit outside the guest clocks.

All 84 batches and both machine checks passed in 228 seconds. The complete
Lython worker image, stock CPython identity and goldens verified. All workers
joined, guest fixtures were removed and the remote archive was SHA-verified
under the lease.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Image verification and procedures](procedures.tar.xz).
