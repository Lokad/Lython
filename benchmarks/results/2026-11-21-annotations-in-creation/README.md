# B1291 eager annotations inside callable creation

This prototype reads consecutive global and constant annotations
directly into the final funded record during callable creation. The
record and callable are admitted together after decorator expressions
and defaults, before the first annotation read. Admission can refuse
at that earlier point. The private record is refunded if a read,
cancellation or later construction aborts before ownership transfers.
Other annotation expressions keep their original evaluation path.

The three eager fixtures each lose four instructions and four
annotation operands. Their defining helper workspaces fall from eight
to six slots. Callable creation absorbs the reads and needs no extra
opcode or operand slot. Postponed factories and the fully supplied
helper keep the same instruction shape. Local warm allocation is
effectively flat. Accounted peaks match, and the shared allowance
returns to baseline in every case.

Reference `256c5e651` and prototype `595798ffa` ran on the quiet Azure
F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3 on November 21, 2026 UTC.
Two workers per revision warm independently for eight seconds and
supply seven rotated timed batches each. Ranges cover all four
pairings of process medians. Warm elapsed includes prior-result
disposal. Process CPU covers the wider request. Process startup and
final teardown have separate boundaries.

| Complete script | Warm elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| Eager rules | −11.51% to −1.83% | −11.73% to −2.43% | 8.50% / 2.25% |
| Eager nested factories | −2.91% to +0.12% | −2.55% to +0.91% | 1.14% / 1.96% |
| Postponed nested factories | −3.77% to +0.25% | −4.49% to +1.64% | 2.58% / 1.56% |
| Fully supplied helper | −0.46% to +14.67% | −0.46% to +14.77% | 0.13% / 15.05% |
| Read every annotation dictionary | −4.68% to +1.77% | −4.07% to −0.22% | 3.65% / 3.01% |

The eager rules are faster in all pairings. The magnitude is uncertain
because the reference process medians differ by 8.50%. For the
unchanged helper, one candidate worker is near reference and its twin
is roughly 15% slower. Both were individually stable within their
timed batches. Keep the prototype private pending confirmation of
both lanes and eager time to result. The nested cases and the
annotation observer leave smaller elapsed effects unresolved.

[B1292 confirmation and retention decision](confirmation/README.md).

Windows .NET 11 and Ubuntu .NET 10 each pass 363 white-box and 455
public Release checks. The new failed-read regression preserves the
name-error span and refunds the unpublished definition. Both complete
32-file images match all five workload goldens and two CPython
cases covering evaluation order, dynamic lookup and dictionary alias
identity. Compiler inspection verifies that both semantic cases
exercise the packed reads. All 140 timed batches pass and the archive
verifies under the VM lease after all workers join and guest fixtures
are removed.

[Timing summary](summary.json) · [Compiler shape](annotation-shape.json) ·
[Local warm allocation](local-allocations.json) · [Raw observations](observations.tgz) ·
[Source, tests and procedures](checks-and-procedures.tar.xz).
