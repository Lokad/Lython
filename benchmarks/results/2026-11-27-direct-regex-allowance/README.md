# B1884 Give one-shot matching its working reservation directly

The optional adapter's synchronous `TryMatch` already aggregates backend
working storage in one reservation. The call now owns that reservation
directly under the borrowed budget. This removes creation and teardown of
a child scope per match attempt. All backend users and capture copies
finish before final disposal. Compiled patterns and cursors keep their
separate scopes. Host refusal is terminal, and the active token reaches
the same backend calls.

The reference is `683e4eb2a`. The candidate adds the archived adapter
patch. Only `Lokad.Lython.Utf8Regex.dll` differs in the two 32-file images.
The core DLL and signed 0.5.0 backend bytes match. Each image matches four
independent CPython 3.13.16 output and file goldens. Both log scripts
scan 3,072 lines from an 86,954-byte file.

| Workload | `Run` CLR bytes saved | `RunAsync` CLR bytes saved | Peak shared bytes in both images |
| --- | ---: | ---: | ---: |
| Compiled-pattern log search | 221,184 | 221,128 | 1,178,381 |
| Module-level log search | 221,358 | 221,358 | 1,178,293 |
| Cursor control | 0 | 0 | 523,444 |
| CSV invoice control | 56 | −56 | 9,040,266 |

The Windows Release allocation witness saves 11.0–12.2% on the two logs.
All commands restore their shared allowance baseline. Windows and Ubuntu
WSL each pass 744 focused Release checks per target on .NET 10 and 11.
The added one-shot test cancels during working admission, checks token
identity and reruns against the same funded pattern and budget.

The local Ubuntu WSL timing uses .NET 11 RC3, one logical CPU, two workers
per image, at least five seconds of warmup per worker and seven rotated
batches. Warm elapsed includes mediated RAM-backed I/O, result cleanup
and golden verification. Process CPU also covers fixture reset and framing.
Both clocks exclude compilation and startup. Ranges span all pairings of
worker medians.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Compiled-pattern log search | `Run` | −13.54% to −2.10% | −4.36% to +4.47% |
| Cursor control | `RunAsync` | −1.79% to +2.26% | −2.91% to +6.42% |
| Module-level log search | `RunAsync` | −23.02% to +15.30% | −9.04% to +19.39% |
| CSV invoice control | `Run` | −32.92% to +19.14% | +1.07% to +7.69% |

Module reference elapsed workers differ by 27.48% and CSV reference workers
by 55.49%. Keep the allocation reduction and removal of the child
scope. The CPU and elapsed magnitude remain uncertain. All 112 timed
batches verify. Workers join and guest fixtures are removed before the
archive verifies. The first report parser expected the optional adapter
in the worker's abbreviated Ready list. The corrected parser checks the
launched directories and the full before/after image hashes.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Image identity](identity.json) · [Observations](observations.tar.xz) ·
[Exact source and checks](procedures.tar.xz).
