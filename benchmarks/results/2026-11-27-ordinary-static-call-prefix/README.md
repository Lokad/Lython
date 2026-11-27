# B1877 Reuse the parsed positional arguments in static analysis

The parser already counts the positional prefix for an ordinary call.
Static analysis now reads that prefix directly, removing a full argument
partition scan, a copied expression array and the cached argument record.
Keyword lookup keeps its existing map. Calls with `*` or `**` retain the
expansion path and resolve values in the current binding state.

The frozen reference is `ab50fad4b`. Only the core DLL differs between the
two 32-file images. Both match six independent CPython 3.13.2 goldens. The
compiler probe verifies matching tapes, spans, captures, visible bindings,
admission, cancellation and failure records across 19 scripts and 139
budget levels. Windows on both Release targets and Ubuntu WSL .NET 10
each pass 1,739 public and 72 white-box checks.

## Allocation and admission

The Windows .NET 11 probe reports median CLR bytes over 17 fresh
compilations and disposals following eight warmups. The compiled peak is
equal on both images for every case.

| Workload | Reference | Candidate | Bytes saved |
| --- | ---: | ---: | ---: |
| Ordinary definitions | 1,410,040 B | 1,400,816 B | 9,224 B |
| Escaping definitions | 1,444,544 B | 1,435,320 B | 9,224 B |
| Guarded report | 2,165,120 B | 2,159,504 B | 5,616 B |
| Ordinary keyword report | 564,888 B | 559,264 B | 5,624 B |
| Starred report | 574,320 B | 571,768 B | 2,552 B |

The two report forms produce the same output. Each makes 96 calls and
passes one positional and two keyword values. The starred form supplies
those values through a tuple and dictionary. Its surrounding calls also
include ordinary forms.

## Local timing

Ubuntu WSL and .NET 11 RC3, two workers per image on one logical CPU. Each
warms for five seconds and contributes seven rotated batches. The spans
cover all pairings of worker medians. Compile-only includes compiled-owner
disposal. Through-result also includes execution, verification and cleanup.
The warm nested-read control includes mediated RAM-backed I/O. Process CPU
adds reset and request framing. Startup is outside all lanes.

| Workload | Phase | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Ordinary definitions | Compile only | −5.01% to −3.42% | −1.75% to −1.41% |
| Starred report | Compile only | −2.49% to +1.48% | −1.79% to −0.20% |
| Escaping definitions | Compile only | −2.51% to +0.41% | −4.70% to +4.64% |
| Ordinary keyword report | Compile to result | +0.13% to +2.21% | −5.56% to −0.82% |
| Nested read control | Warm | −2.85% to +2.47% | −2.41% to +4.54% |

Keep the deletion and the allocation saving. Ordinary definition compile
CPU falls modestly. Keyword elapsed rises while its process CPU falls.
Escaping definitions and the nested read leave timing uncertain. All 140
batches verify, workers join and the raw archive verifies.

[Timing](summary.json) · [Compiler parity](compiler-parity.json) ·
[Image identity](identity.json) · [Observations](observations.tar.xz) ·
[Patch and checks](procedures.tar.xz).
