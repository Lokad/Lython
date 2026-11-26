# B1783 Bind completed fixed zip and CSV rows directly to private locals

The compiler already identifies adjacent, consecutive, uncaptured local
stores following fixed unpack. The trial carries that proof in the existing
instruction operand. The completed producer writes to its admitted row
slots, then checks arity before transferring the values to their locals in
assignment order. Each transfer checks the live execution state. A
cancellation between assignments leaves the unconsumed slots live for the
original instruction and unwind path. Mirrored or published namespaces,
captured bindings and compound targets keep ordinary assignment.
[Candidate patch](candidate.patch) ·
[Earlier CSV-only trial](../2026-11-24-csv-unpack-bindings/README.md)

| Complete script | Fixed rows transferred, either API | Separate store instructions bypassed |
| --- | ---: | ---: |
| Shipment callbacks | 2,048 | 4,096 |
| Explicit-float CSV meter | 4,096 | 20,480 |
| File-fed CSV quantity map control | 0 | 0 |
| Protected file search control | 0 | 0 |

The separately instrumented core records these counts in both hosting
modes. Both complete images match the four independent CPython output
and file goldens. Local median CLR allocation differs by at most
848 bytes per script, with matching peak admission and restored
allowances. [Route counts](routes.json) ·
[Allocation and admission](allocation-summary.json)

Windows .NET 11 and Ubuntu .NET 10 pass 440 white-box and 1,632 public
focused Release checks each. The targeted strict-zip replay covers a
genuine delayed host, one completed row and a failing next advance,
and confirms that the failed row leaves the prior private aliases
intact. The selection also exercises fixed CSV arity, cancellation,
ordinary targets and managed stack protection.

## Quiet comparison

The reference is `d256c5200`. Both 32-file images were verified and
differ only in the core DLL. On November 26, 2026 UTC, the Azure
F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3. Two workers per source
warmed for five seconds and supplied seven rotated batches. The
ranges span all four pairings of worker medians. Warm elapsed
includes mediated RAM-backed I/O, verification and previous-result
disposal. Process CPU includes fixture reset and request framing.
Compilation, startup and final teardown are outside these clocks.

| Complete script and lane | Initial elapsed change | Initial process CPU change |
| --- | ---: | ---: |
| Shipment callbacks, async | −4.61% to +0.47% | −4.64% to +1.40% |
| File-fed CSV quantity map, async | −4.79% to +1.73% | −5.53% to +1.04% |
| Explicit-float CSV meter, sync | −0.70% to +1.16% | +1.32% to +2.27% |
| Protected file search, sync | +0.40% to +16.99% | +0.38% to +14.16% |
| Shipment callbacks, sync | −4.25% to −0.49% | −4.44% to +1.00% |

File-search candidate elapsed spread reaches 16.17%. The preparation
recovers space from 21 verified duplicate archives and expanded
records with matching committed copies. The compressed-only inspection
found insufficient room and made no changes. All 140 timed batches
complete, both quiet gates pass and the archive verifies after workers
join and fixtures are removed.

[Initial timing](summary.json) · [Identity](identity.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Procedures and test logs](procedures.tar.xz)

Retain the direct transfer. Sync shipment elapsed falls in both
blocks. The reordered block also shows lower CPU for sync and async
shipment and the CSV meter. Keep the initial adverse CSV CPU and the
initial file-search spread with the evidence. This compares against
`d256c5200`. It leaves the earlier B1782 async comparison against
`ca0fd89f4` open. [Confirmation](confirmation/README.md)
