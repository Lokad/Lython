# B1903 Retire discarded send results at direct yields

Standalone `yield` and generator-expression output record that they
discard the send result. Their instruction consumes the yielded value
with no result slot to publish on resume and no following discard.
The buffered route uses the same compiler fact and drops its opcode
lookahead. A direct suspension keeps the yield instruction current so
an injected exception routes through its protected region. A normal
resume advances past it and only publishes values consumed by an
expression. The waiting-for-send flag is redundant. Delegated yields
keep their existing protocol and return path.

The reference is `5a46eb097`. The frozen images each have 32 verified
files and differ only in the core DLL. Both match five independent
CPython 3.13.16 output and file goldens. A separate CPython 3.13.2
comparison on Windows checks discarded and consumed sends, object
identity, exception injection, close, delegation and generator-expression
output in both APIs. The first probe wrapper read the execution status
from the wrong JSON level. Its recorded sync result matched, and the
corrected wrapper verified both results.

The Windows .NET 11 Release run passes 1,106 public and 379 white-box
focused tests. Coverage includes real asynchronous context cleanup,
terminal admission refusal, cancellation, staged list yields and
retained frame lifetimes. Windows .NET 10 and Ubuntu WSL .NET 11 each
pass 435 public and 91 white-box focused checks.

The Windows allocation witness returns the borrowed budget to baseline
on every run. Compiled and peak execution admission match for all five
scripts in both APIs. CLR allocation is identical except for a 56 B
increase on the async wide-list run. The candidate removes instruction
dispatches and stack writes during execution. The iterable shipment has
five fewer emitted discard instructions, and the generator reduction and
wide shipment each have one fewer. The bound shipment and file-fed map
keep their original instruction counts.

## Timing on November 27, 2026 UTC

Ubuntu WSL ran .NET 11 RC3 on CPU 27. Two workers per image warm for at
least five seconds and contribute seven rotated batches each. Ranges
span all pairs of worker medians. Warm timings include mediated
RAM-backed file I/O, output and file verification, and previous-result
cleanup. Process CPU also includes fixture reset and framing.
Compilation and process startup are outside both clocks.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Iterable-row shipment | `Run` | −3.84% to −1.16% | −4.08% to +0.99% |
| File-fed map control | `RunAsync` | −0.18% to +1.15% | −1.78% to +3.06% |
| Per-row generator reduction | `RunAsync` | −3.49% to +2.76% | −3.22% to +2.44% |
| Bound shipment | `RunAsync` | −1.00% to +2.25% | −0.40% to +0.83% |
| Iterable-row shipment | `RunAsync` | −1.85% to −0.93% | −0.03% to +1.83% |
| Per-row generator reduction | `Run` | −0.13% to +1.11% | −1.30% to +1.81% |

All 168 timed batches verify. The workers join, guest fixtures are
removed and the persistent archive verifies. Retain the simpler yield
transport. Iterable shipment elapsed falls in both local lanes, while
every process CPU range spans zero. The quiet VM had 18,059,264 free
bytes. Staging the two new DLLs would leave less than the established
free-space floor, so the timing result is local.

[Emitted yield sites](tape-summary.json) · [Allocation and admission](allocation-summary.json) ·
[Timing summary](summary.json) · [Image identity](identity.json) ·
[Observations](observations.tar.xz) · [Patch, checks and procedures](procedures.tar.xz).
