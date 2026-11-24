# Discard native JSON file write results before boxing (B1619)

The frozen pair uses the original traversal and counted text writer.
For exact file output, `json.dump` awaits the writer's typed result
and discards it directly. The writer still validates, stages and
publishes the same bytes under the same ownership. CSV and ordinary
`file.write` still return their count. Managed frame admission and
exception translation remain at the write boundary.

The manifest allocates 3,104 fewer bytes per run, from 99,640 to
96,536 bytes, or 3.12%. The JSON summary saves 128 bytes. Peaks
match on all five scripts in the allocation comparison. Five
CPython goldens pass on both 32-file images. Windows and Ubuntu
each pass 1,338 focused .NET 11 Release checks.

The quiet Azure F4as_v7 ran five lanes with Ubuntu 24.04.4 and .NET
11 RC3 on November 24, 2026 UTC. Two workers per source warmed
separately for five seconds and ran seven rotated batches each.
All 140 batches, both quiet gates, worker joins and fixture cleanup
passed. Elapsed uses prepared code, mediated RAM I/O, verification
and prior-result cleanup. Wider CPU also includes fixture reset
and request framing. Ranges span pairings of worker medians.

| Workload | API | Elapsed change | Wider CPU change |
| --- | --- | ---: | ---: |
| Configuration manifest | Run | −0.11 to +0.36% | −0.26 to +1.79% |
| Configuration manifest | RunAsync | −1.72 to +0.13% | −2.87 to +0.68% |
| Expanded JSON Lines | Run | −3.03 to +1.73% | −3.33 to +0.00% |
| Default JSON Lines | Run | −2.78 to +6.85% | −1.94 to +5.00% |
| Quoted CSV invoice | Run | −1.07 to +1.34% | +0.00 to +3.57% |

Retain the allocation reduction and smaller implementation. Both
manifest APIs are timing-inconclusive. The directional default JSON
Lines regression from B1617 and B1618 does not repeat, with a
7.82% candidate worker spread. The adverse pairings stay in the
record. Leave elapsed and CPU attribution open for the next coherent
file checkpoint.

[Quiet summary](quiet-summary.json) · [Quiet observations](quiet-observations.tar.xz) ·
[Terminal receipt](quiet-terminal-status.json) · [Allocation data](allocation-summary.json) ·
[Source patch, goldens and logs](procedures.tar.xz) · [Earlier variants](../README.md).
