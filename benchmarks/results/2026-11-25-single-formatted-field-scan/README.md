# B1726 Shared scan for formatted-string fields

The field scanner supplies the closing brace, conversion and format
boundaries, and last visible expression character. Ordinary fields no
longer allocate a copy of the whole field or build debug text. The parser
removes its independent delimiter scan. Quoted strings and comments can
contain delimiter-like text without ending the field. Literal text and
nested specifiers still follow the existing formatting grammar.

The initial reference is the frozen B1725 candidate at `e79bf6be4` plus the
recorded direct-field patch. Production `14c2926f7` also had the unrelated
regex cache change. The two measured images differ only in the core DLL.
They match all three independent workload goldens and the inspected layouts
of five code objects and 241 instructions. The local compile allocation
diagnostic moves from 58,400 B to 57,720 B per report, a 1.16% reduction.
The manifest stays at 72,776 B and all retained admission values match.

The Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 25, 2026 UTC.
Two workers per image warm for seven full-request seconds and provide seven
rotated batches per phase. The table compares all four pairings of worker
medians. Compilation includes compiled-owner disposal. Compile-to-result
includes mediated RAM-backed I/O, verification and result cleanup. Wider CPU
also includes reset and framing. Startup is outside these clocks.

| Workload | Phase | Elapsed change | Wider CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| 16-record formatted report | Compile | −1.73% to −0.74% | −1.23% to +0.91% | 0.80% / 0.19% |
| 16-entry file manifest | Compile | −0.07% to +0.38% | −0.16% to +0.97% | 0.43% / 0.02% |
| 16-record formatted report | Compile to result | −3.54% to −1.16% | −3.32% to −1.68% | 2.31% / 0.15% |
| 1,024-record formatted report | Warm sync | −0.54% to +4.58% | −1.96% to +4.13% | 1.54% / 3.56% |

The shorter report improves through result. Compile-only CPU and the large
warm report remain inconclusive. The initial source uses a bulk trim to
recover the debug marker after rendering comments. The refinement carries
the marker through the existing cancellable render pass and repeats the
same four phases. [Refined comparison](confirmation/README.md).

The initial variant passes 351 public and 25 white-box focused Release
checks on Windows .NET 11 and Ubuntu .NET 10. Both quiet gates and all 112
timed batches pass. Workers join and the persistent archive verifies under
the lease with guest fixtures removed.

[Frozen identity](identity.json) · [Timing](quiet-summary.json) ·
[Compiler allocation](compile-allocation-summary.json) ·
[Instruction comparison](tape-summary.json) · [Observations](quiet-observations.tar.xz) ·
[Terminal receipt](quiet-terminal-status.json) · [Source and checks](procedures.tar.xz).
