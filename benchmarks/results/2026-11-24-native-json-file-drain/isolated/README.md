# Isolate the native JSON file result (B1618)

This private variant restores the original JSON traversal on
`d7dd3d4bd` and changes the exact text-file sink to a count-free
completion. The manifest still allocates 96,536 bytes per invocation,
down 3,104 bytes. The JSON summary saves 128 bytes. The five measured
admission peaks match, and both frozen images pass all five goldens.
Windows and Ubuntu each pass 1,338 focused .NET 11 Release checks,
including the refined partial-output and suspended-producer tests.

The same Azure F4as_v7 ran six quiet lanes on November 24, 2026 UTC.
It used .NET 11 RC3, two separately warmed workers per source and
seven rotated batches per worker. Warm elapsed includes applicable
RAM-backed I/O, verification and prior-result cleanup. Wider CPU
also includes fixture reset and request framing. Ranges span the
four pairings of individual worker medians.

| Script | API | Elapsed change | Wider CPU change |
| --- | --- | ---: | ---: |
| Configuration manifest | Run | −2.22 to −0.45% | −1.82 to −1.48% |
| Configuration manifest | RunAsync | −1.21 to −0.18% | −1.79 to +2.48% |
| JSON summary helpers | Run | −0.73 to +0.35% | −1.00 to +0.30% |
| Expanded JSON Lines | Run | +0.19 to +2.96% | +0.20 to +3.85% |
| Default JSON Lines | Run | +2.24 to +2.87% | +1.59 to +3.57% |
| Quoted CSV invoice | Run | −2.92 to +0.32% | −2.27 to +3.57% |

Both quiet gates, all 168 batches, verified archive and joined cleanup
passed. The manifest benefit survives isolation. The eager runner
still pays the changed counted text-write path, and its adverse
default-options result repeats despite the restored traversal.
This variant remains private. A final comparison restores the
original counted writer byte-for-byte and isolates discarding the
typed result before boxing.

[Quiet summary](quiet-summary.json) · [Quiet observations](quiet-observations.tar.xz) ·
[Terminal receipt](quiet-terminal-status.json) · [Sources, test logs and allocation evidence](procedures.tar.xz) ·
[Combined trial](../README.md).
