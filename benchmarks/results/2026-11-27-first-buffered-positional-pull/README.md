# B1883 Buffer the first positional generator pull

Starred calls and `starmap` now let a same-command generator fill the
existing admitted argument buffer from its first yield. Each row avoids
one private iteration cursor. A four-yield row enters the generator twice
instead of three times, including the final exhaustion check. Capacity
growth still occurs after the generator returns control. Refusal, cleanup
and callback borrowing continue to use the existing owners.

The reference is `ab883268b`. The candidate is `2c8d98059` with the
archived production patch. Only the core DLL differs between the
32-file images. Both verify all three independent CPython 3.13.16 output
and file goldens. The iterable report processes 2,048 four-yield rows from
the 27,350-byte shipment CSV fixture.

| Hosting mode | Reference CLR bytes | Candidate CLR bytes | Peak admitted bytes for either image |
| --- | ---: | ---: | ---: |
| `Run` | 2,825,934 | 2,645,710 | 4,304,400 |
| `RunAsync` | 2,826,118 | 2,645,950 | 4,304,400 |

The Windows Release allocation witness saves about 180 KB or 6.38% per
command. The wide and file-fed controls stay within 230 CLR bytes and
retain their peaks. Every case restores its shared allowance baseline.

On November 27 under Ubuntu WSL on one logical CPU, two workers per
image each warmed for five seconds and contributed seven rotated batches.
The warm stopwatch includes mediated RAM-backed I/O, output verification
and prior-result cleanup. Process CPU also includes fixture reset and
request framing. Compilation and startup remain outside both clocks.
The table spans all pairings of worker medians.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Iterable shipment | `Run` | −9.09% to −3.44% | −9.33% to −4.12% |
| Iterable shipment | `RunAsync` | −4.86% to −2.12% | −8.33% to −1.00% |
| Wide shipment control | `RunAsync` | −4.43% to −1.80% | −4.86% to −1.57% |
| File-fed map control | `RunAsync` | +1.38% to +3.63% | −1.96% to +5.70% |

The unchanged wide report also improves and the file-fed map takes
longer elapsed. Treat these times as diagnostic. Keep the change for
the eliminated cursor and frame entry, the allocation reduction and
the matching admission peaks. All 112 batches verify in 137.49 seconds.
Workers and fixture cleanup complete before the archive verifies.

Windows and Ubuntu WSL pass 186 focused Release checks per target on
.NET 10 and .NET 11. They cover iterator protocols, starred calls, shared
admission refusal and cleanup. The added cancellation case interrupts a host read
inside the row generator, verifies the callback never starts, joins file
cleanup and returns the allowance to baseline.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Image identity](identity.json) · [Observations](observations.tar.xz) ·
[Exact source and verification](procedures.tar.xz).
