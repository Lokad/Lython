# B1887 Bind module regex arguments in fixed slots

The eight module-level pattern operations use the existing fixed-slot
binder for ordinary executable calls. This removes one reserved bound
array per call. Both APIs retain the same argument validation and
distinguish omission from explicit Python `None`. Pattern resolution still
precedes conversion of counts and windows. A suspended lookup keeps the
needed argument values without extending the binding slots' lifetime.
Calls coming through the general argument-packet interface can still
use the ordinary owned binder.

The reference image contains `4ab65dcc6`. The candidate contains the
archived patch over `4c3edd3c8`. Only the core DLL differs between the
32-file images. The optional adapter and signed PythonRe 0.5.0 backend
are identical. Both images match four independent CPython 3.13.16
output and file goldens.

The 3,072-line module log search saves **196,608 CLR bytes per scan**
in each API, or **10.98%**, with the same 1,178,293-byte peak admission.
The compiled-pattern log, cursor and CSV controls match allocation
and admitted peaks exactly. The Windows Release allocation witness
returns the shared allowance to baseline after every command.

The flag regression suspends during compiled-pattern resolution and
then converts the start bound through a separate awaited protocol.
The cancellation case closes the pending file before returning the
allowance and preserves the original cancellation reason. Each Release
target passes 900 focused checks on Windows and Ubuntu WSL with .NET 10
and .NET 11.

## Local matched timing

Ubuntu WSL and .NET 11 RC3 use one logical CPU, two workers per image
and seven rotated batches after each worker's warmup. Warm elapsed
includes mediated RAM-backed I/O, verification and prior-result cleanup.
Process CPU also includes fixture reset and request framing. Compilation
and startup are outside both clocks. Ranges span pairings of worker medians.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Module-level log search | `Run` | −11.03% to −2.17% | −8.99% to −0.14% |
| Compiled-pattern log control | `Run` | −1.27% to +5.06% | −0.21% to +3.10% |
| Module-level log search | `RunAsync` | −6.22% to −1.55% | −4.50% to −0.74% |
| CSV invoice control | `Run` | −5.56% to +2.88% | +0.34% to +1.27% |

The module search uses less elapsed and process CPU in both local
lanes. The compiled control spans zero and the unchanged CSV control
uses slightly more CPU. Keep the allocation and shorter binding lifetime
with the local timing evidence and the control variation. All 112 batches
verify in 148.04 seconds. Workers join and fixtures are removed before the
archive verifies.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Image identity](identity.json) · [Observations](observations.tar.xz) ·
[Exact source and checks](procedures.tar.xz).
