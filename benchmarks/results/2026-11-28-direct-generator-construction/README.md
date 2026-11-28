# B1991 Direct generator-construction trial

The prototype restricts ordinary call handling to generator targets after
executable functions have taken the frame runner. Direct and bound generator
calls share target selection. The ordinary binder transfers its admitted
arguments directly to construction, avoiding a generic function result slot.
The complete positional path keeps its staged consumer.

The two images differ only in the core DLL, comparing `04159957a` with the
archived patch. Windows .NET 10 and 11 and Ubuntu WSL .NET 11 each pass
525 public and 105 white-box focused Release checks. A new regression checks
direct and bound generators with default lookup through a host read, before
either body starts. Both APIs return their allowance. Four independent
CPython 3.13.16 goldens verify on both images. Across the four scripts in
both APIs, CLR allocation, peak execution admission, compiled admission and
allowance growth counts match exactly.

## Local warm timing

November 28, 2026 UTC on Ubuntu WSL and .NET 11 RC3. Two workers per image
warm for five seconds and contribute seven rotated batches each. Elapsed
includes applicable RAM-backed I/O, verification and previous-result
disposal. Process CPU includes fixture reset and framing. Compilation and
startup are outside the clock. Ranges pair worker medians.

| Workload | Boundary | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Generator calls with defaults | Sync | −4.60% to +0.56% | −3.62% to −0.68% |
| Generator calls with defaults | Async | −3.54% to +1.59% | −2.04% to −0.66% |
| Positional generator calls | Async | −0.85% to +1.54% | −3.18% to −0.26% |
| Staged CSV generator control | Async | −5.04% to +1.87% | −2.03% to +1.19% |
| Configuration edit | Sync | −6.71% to +3.38% | +0.48% to +3.66% |

The first 140 timed batches and the 56-batch reverse-order repeat verify.
On repeat, configuration CPU rises 1.34–3.22% and its elapsed range spans
zero. The async default-generator case spans zero on both clocks, with
5.73% reference CPU spread. The prototype stays shelved. The next variant
keeps generator selection at the existing call boundary and removes only
the intermediate generator result dispatch.

[Allocation](allocation.json) · [Admission](admission.json) ·
[First timing](warm-summary.json) · [Repeat timing](repeat-summary.json) ·
[First observations](warm-observations.tar.xz) · [Repeat observations](repeat-observations.tar.xz) ·
[Worker hashes](identity.json) · [Procedures](procedures.tar.xz).
