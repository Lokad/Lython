# B1725 Direct parsing of formatted-string fields

The compiler supplies temporary grouping tokens for the original field text
and calls the existing grouped-expression parser. Each field skips construction
of a synthetic assignment string, statement and script. The outer parser still
validates the field in its real scope and maps diagnostics to source positions.
A bare backslash at the end of a field now fails compilation. An explicit
backslash-newline continuation still parses. CPython 3.13.2 confirms both
outcomes. The old Lython image fails the new bare-backslash regression.

The source-equivalent reference is `93eead293`. The frozen 32-file images
differ only in the core DLL and verify three complete CPython goldens.
On Windows .NET 11 and Ubuntu .NET 10, 350 public and 25 white-box focused
Release checks pass per target. The inspected three scripts have identical
instruction, source-span and storage-layout metadata across five code objects
and 241 instructions. The compile diagnostic restores the shared allowance.
It reports 60,776 B versus 58,400 B per report compilation, a 3.91% reduction.
The unchanged manifest compiles at 72,776 B in both images. Retained admission
is identical in all three cases.

The Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 25, 2026 UTC.
Two workers per image warm for seven full-request seconds and provide seven
rotated batches per phase. Ranges span all four pairings of worker medians.
Compilation includes disposal of the compiled owner. Compile-to-result adds
warm-host execution with mediated RAM-backed I/O, verification and result
cleanup. Wider process CPU also includes fixture reset and request framing.
Startup is outside these clocks.

| Workload | Phase | Elapsed change | Wider CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| 16-record formatted report | Compile | −6.43% to −4.71% | −8.56% to −4.14% | 0.05% / 1.79% |
| 16-entry file manifest | Compile | −1.74% to −0.53% | −2.03% to −1.19% | 0.82% / 0.40% |
| 16-record formatted report | Compile to result | −0.63% to +0.21% | +0.09% to +0.43% | 0.19% / 0.66% |
| 1,024-record formatted report | Warm sync | +0.05% to +2.85% | +0.00% to +2.41% | 0.29% / 2.51% |

Retain the construction simplification, lower compiler allocation and
compatibility correction. Compilation CPU falls in this block, including on
the unchanged control to a smaller extent. The compile-to-result elapsed
result spans zero. The large warm report leans adverse and its cause remains
open. The inspected executable instructions are unchanged for this report.
The observation stays in the next external checkpoint. Confirm warm timing
earlier if source changes expose a plausible repeatable cause.

Both quiet gates and all 112 batches pass. All workers join, guest fixtures
are removed and the persistent archive verifies under the lease. The first
local instruction inspector expected an obsolete private field and stopped
before examining code objects. The corrected inspector uses the current
compiled-script field.

[Identity](identity.json) · [Timing](quiet-summary.json) ·
[Compilation allocation](compile-allocation-summary.json) ·
[Instruction comparison](tape-summary.json) · [Continuation probes](python-continuations.json) ·
[Observations](quiet-observations.tar.xz) · [Terminal receipt](quiet-terminal-status.json) ·
[Source and procedures](procedures.tar.xz).
