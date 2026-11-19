# B1144 reuse abstract bindings on a selected path

Compared `ee057fd12` and prototype `5828dad90` on the quiet Azure F4as_v7
with Ubuntu 24.04.4 and .NET 11 RC3 on November 19, 2026 UTC. The change
landed at `5784352e4`. Each complete 32-file image verified against five
CPython script goldens before staging.

A proven conditional or a `with` body can update the existing abstract
bindings directly. This removes the two binding-map snapshots and copy
back. The `with` target still takes its fact from the incoming context
expression before any shadowing. Uncertain branches still join their
facts, and annotation diagnostics still use their existing walk.

| Workload | Phase | Elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | --- | ---: | ---: | ---: |
| Generated validator | Compilation | −5.32% to +5.14% | −5.01% to +5.17% | 4.71 / 6.06% |
| Manifest | Compilation | −1.76% to −0.004% | −2.85% to −0.77% | 0.58 / 1.20% |
| Formatted report | Compilation | −3.98% to −1.30% | −4.93% to −2.64% | 0.13 / 2.66% |
| Generated validator | Compile-to-result | −2.99% to −0.22% | −3.94% to −0.63% | 1.78 / 1.06% |
| Manifest | Compile-to-result | −2.60% to −0.20% | −2.96% to +1.13% | 0.13 / 2.33% |
| Formatted report | Warm control | −1.96% to +0.98% | −3.07% to +0.79% | 2.17 / 0.82% |

Ranges cover all four pairings of process medians. Report compilation
has lower elapsed and CPU. The generated validator also improves
compile-to-result, although its largest IQR is 5.33%. Validator
compile-only is inconclusive. Manifest compilation has lower CPU, with
elapsed as close as 0.004% to parity. The warm report control spans zero.

Local compilation allocation falls 1.47% for the validator, 1.02% for
the manifest and 3.40% for the report. Rule compilation and warm
allocation are effectively flat. Accounted runtime peaks stay equal.
Windows .NET 11 and Ubuntu .NET 10 each passed 277 focused public tests
and five white-box tests.

The collection took 327.06 seconds. All 120 timed batches passed,
workers joined, both quiet gates passed and the archive was verified
before fixture cleanup completed. Two staging preflights found
insufficient disk space and launched no workers. Staging then preserved
eight frozen dependency paths while coalescing 38.5 MiB of identical
data. Both paths' bytes, owner, mode, modification time and extended
attributes were checked before the atomic replacements. No input or
worker binary was changed.

Warm includes mediated I/O, golden checks and previous-result cleanup.
Compile-to-result measures compilation, one execution and compiled-owner
disposal together. Startup, fixture reset and final teardown are outside
guest elapsed. The process CPU boundary also includes fixture reset and
protocol handling.

[Measurements](summary.json) · [Raw observations](observations.tgz) ·
[Lease receipt](archive-receipt.json) · [Checks and procedures](checks-and-procedures.tar.xz).
