# B1879 Omit form arrays for positional executable calls

The compiler uses the parser's argument shape to select expanded calls
and represent positional-only sites by their argument count. Generic
binding can still read each positional form. Named sites keep their
full form arrays and named `format` lookups. The positional marker and
the absence of a form array are the same fact.

The frozen reference is `18c82c45f`. Only the core DLL differs between the
two 32-file images. Both match seven independent CPython 3.13.2 goldens.
The compiler probe checks every call site's observable forms, spans and
receiver shape, as well as tapes, captures, cancellation and refusal. All
20 scripts and 143 budget levels match. Windows Release .NET 10 and .NET 11
and Ubuntu WSL .NET 10 pass 3,100 public and 1,270 white-box checks each.

## Allocation and admission

Windows Release .NET 11 medians over 17 compilations with disposal.
Both images give the same governed peaks and return to baseline.

| Workload | Arrays removed | CLR bytes before | CLR bytes after |
| --- | ---: | ---: | ---: |
| Ordinary definitions | 163 | 1,400,816 | 1,381,208 |
| Escaping definitions | 163 | 1,435,320 | 1,415,712 |
| Guarded report | 100 | 2,159,504 | 2,147,472 |
| Named-format shipment | 6 | 49,072 | 48,352 |

The named shipment keeps two named form arrays. The separate keyword report
keeps all 96 named arrays. The probe verifies the forms returned by the
call site's accessor.

## Local timing

Ubuntu WSL and .NET 11 RC3. Two workers per image ran on one logical CPU.
Each warmed for five seconds and supplied seven rotated batches. Ranges
cover all worker median pairings. Compile lanes include compiled-owner
disposal. Execution lanes include applicable RAM-backed I/O, result
verification and cleanup. Process CPU also covers reset and framing.
Startup is outside these lanes.

| Workload | Lane | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Ordinary definitions | Compile | −5.27% to +6.10% | −8.41% to +3.59% |
| Named-format shipment | Warm | −5.00% to +4.55% | −3.07% to +3.01% |
| Ordinary definitions | Compile to result | −8.84% to +2.62% | −2.75% to +5.62% |
| Named-format shipment | Warm `RunAsync` | −4.10% to +1.00% | −1.71% to +0.29% |
| Nested read control | Warm | +1.43% to +4.13% | −0.47% to +7.99% |

Keep the metadata deletion and the allocation reduction. Definition and
named-format timing spans zero. The nested read elapsed drift remains
visible. All 140 batches verify, workers join and the archive verifies.

[Timing](summary.json) · [Compiler parity](compiler-parity.json) ·
[Image identity](identity.json) · [Observations](observations.tar.xz) ·
[Patch and checks](procedures.tar.xz).
