# B1025 prepared format field selectors

Compared retained runtime `6ce82ba48 / 589d6b7b` against measured
prototype `23da93c39 / 79d43441` on the quiet Ubuntu Azure F4as_v7
with .NET 11 RC3 on 2026-11-19 UTC. Runtime and regression-test source
are identical to retained `b8532e0ef`.

`str.format` and `format_map` prepare positions and attribute/subscript
selectors in the existing execution-owned template. The formatter removes
its mutable numbering state, repeated Unicode digit classification and
suffix parsing and per-row CLR token slices. It still makes fresh guest
subscript keys. Grammar and selector failures remain ordered with actual
lookups and callbacks. An admission estimate covers added selector
metadata and intermediate capacity. Conservative estimation can bring
refusal forward for templates with long access chains.

| Workload and phase | Elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| Shipment report using format subscripts, warm | −6.65…−4.13% | −8.09…−3.56% | 0.42 / 2.27% |
| Shipment report using format subscripts, compile-to-result | −2.95…−1.12% | −3.70…−1.81% | 1.78 / 0.10% |
| Expanded forwarding, warm | −0.77…−0.24% | −1.75…+1.55% | 0.47 / 0.07% |
| Expanded forwarding, compile-to-result | −1.79…−0.11% | −2.88…−0.47% | 1.52 / 0.20% |
| Ordinary method-format report, warm | −2.39…+5.20% | −4.00…+3.85% | 3.10 / 4.53% |
| F-string report control, warm | −0.98…+2.16% | −2.49…+3.78% | 0.68 / 2.48% |
| Positional call control, warm | −3.68…+0.12% | −3.33…+0.41% | 3.92 / 0.02% |

The ranges cover four pairings of process medians. The format-subscript
variant reuses the shipment fixture and stdout/file goldens. It passes
the whole record to the formatter instead of extracting the three keys
in Python first. The comparison runs the same source on both images.
The local allocation diagnostic for that variant falls from 5,194,500
to 4,875,446 bytes (**6.14%**), with an accounted peak increase of 384
bytes to 4,728,927. The ordinary method-format and forwarding allocations
are effectively unchanged. All five cases return the allowance to baseline.
The ordinary method-format elapsed result remains inconclusive.

Both complete 32-file images verified against the five-case catalog with
independent CPython goldens. Windows .NET 11 and WSL .NET 10 each passed
402 public and 68 white-box focused checks. Checks cover cached numbering
and Unicode indices, ordered errors after earlier callbacks, actual hosted
suspension, native cancellation, retained output and refused admission.

Seven jobs completed in 321.00 seconds. Each used two workers per source,
eight seconds of full-request warmup per worker and five rotated batches.
All 140 measured golden batches and both quiet gates passed. The workers
joined and guest fixtures were removed under the owned lease. The first
local retrieval check still expected six jobs. It stopped before copying
the successful archive. Correcting that assertion retrieved and verified
the original data.

Warm includes mediated RAM-backed I/O, golden checks and prior-result
cleanup. Compile-to-result adds compilation and disposal of its owner.
Startup, fixture reset, framing and final cache teardown are outside
guest elapsed. Process CPU covers the wider request.

[Medians](summary.json) · [Raw observations](observations.tgz) ·
[Exact retained core](lython-core.dll.gz) · [Source, checks and procedures](checks-and-procedures.tar.xz).
