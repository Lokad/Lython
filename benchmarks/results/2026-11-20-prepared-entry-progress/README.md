# B1225 Prepared function entry and binding progress

Compares `d948e0682` with private `eaf4d65c1`, matching `c0d49927c`
in production, tests and worker source. The quiet Azure F4as_v7 ran
Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-20 UTC. Two processes per
revision use seven-second warmups and five rotated rounds targeting
0.4-second batches. Ranges cover all four pairings of process medians.
Negative changes favor the candidate.

| Workload | Boundary | Elapsed change | Wider process CPU change | Duplicate elapsed spreads, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Eager nested factories | Warm | −2.76% to −1.08% | −4.75% to +2.13% | 1.27% / 0.45% |
| Postponed nested factories | Warm | −3.98% to −0.37% | −2.76% to −0.99% | 0.94% / 2.80% |
| Eager nested factories | Warm `RunAsync` | −4.52% to +2.42% | −6.49% to +0.37% | 0.50% / 6.73% |
| Expanded forwarding report | Warm `RunAsync` | −2.11% to +1.49% | −3.71% to +0.88% | 1.46% / 2.20% |
| File-backed CSV invoice | Warm | −0.54% to +1.44% | +0.87% to +1.67% | 0.89% / 1.09% |

Native body entry owns function recursion and checks the physical stack
at the managed entry. The enclosing interpreter retains its separate
logical admission. Only the owned continuation can inherit a pending
body's recursion depth. Default completion starts after the supplied
positional prefix. Asynchronous default lookup resumes at the first
unresolved keyword and reads each live default when needed. This
removes both keyword rediscovery scans through already bound slots.

The paired changes are retained. The synchronous nested cases run
sooner, with lower CPU across all postponed pairs. The eager
comparison has mixed CPU. The initial invoice CPU increase does not
repeat in [B1226](confirmation/README.md). Async timings still span
zero. Allocation is effectively flat across the four Windows
workloads. Accounted execution peaks agree and all shared allowances
return to baseline.

Warm includes mediated RAM-backed file I/O where used, checks and
previous-result cleanup. Compilation, fixture reset, startup and
final teardown sit outside the guest clock. CPU includes the wider
request. The `RunAsync` lanes use the existing comparison host.
Real suspension and terminal admission unwind are covered separately
by the focused tests.

Windows .NET 11 and Ubuntu .NET 10 each passed 287 white-box and 1,014
public Release checks. Both 32-file images verified four complete
workloads, plus eight independent CPython probes in both execution
modes. One initial native-entry prototype changed the internal test
call signature and stopped at build. The measured source keeps the
existing entry signature while owning recursion through disposal.
All 100 timed batches and both quiet checks passed. Two superseded
working DLLs were reclaimed after verifying their complete reconstructible
Git blobs, committed raw receipts and remote hashes. Both original
archives and source records remain available. Current workers joined
and the persistent archive was verified before retiring its lease.

[Timing](summary.json) · [Allocation](allocation.json) ·
[Raw](observations.tgz) · [Sources, checks and procedures](source-and-checks.tar.xz).
