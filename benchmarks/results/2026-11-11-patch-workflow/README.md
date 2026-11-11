# R229 — complete patch-generation workload

The existing representative catalog now includes `patch-generation.py`: read two
UTF-8 Python files, generate `difflib.unified_diff`, publish `change.patch` and
report line counts. Empty,16-line and1,024-line inputs use one central replacement
between unique lines. Exact patch goldens are independently constructed in CLR
from that edit, including three context lines, headers, UTF-8 and LF bytes.

**Correctness qualified; controlled comparison complete.** On2026-11-11, Release solution
build20.26s/0warnings/errors;83file-workerchecks pass per actual.NET11/.NET10.
Existing replay/validation tests include all three new cases; added lane controls
cover compile, compile-to-result and async execution. Shutdown requires the shared
budget to reach zero. Core source remains byte-equivalent to fully qualified4aa;
normal0.4 adapter unchanged. [Identity](identity.json),[checks](focused-checks.json).

Independent supervised stockCPython3.13.2 and Lython workers pass all3sizes against
the same source/fixtures/exact files and stdout, with before/after verification,
compile/warm/compile-run smoke, Closed and joined shutdown. Two-invocation clocks
are deliberately unqualified; no speed ratio, CPU or pending-I/O latency follows.
[Semantic receipt](semantic-cpython.json),[frozen catalog](catalog.json).

The existing comparison protocol/host/runner is reused; no new framework or runtime
shortcut. Current difflib already reuses native match-length dictionaries, while
private patch generation uses Python-facing matcher/index/match/opcode machinery.
Their source existence does not establish dominance. The controlled comparison
below does not justify a difflib redesign for this fixture. Preserve mutable public matcher tables, cache aliases,
Python tie/junk/popular rules, callbacks, cancellation and admitted ownership.

This artifact adds workload coverage, not a new runtime delivery or a reason to
repeat unchanged platform/package qualification. Exact sources/commands/logs/TRXs
and original smoke clocks survive in [preparation](preparation.tar.xz) with its
[archive identity](archive-identity.json). Publication remains postponed.

## Controlled comparison

Qualified runtime `4aa2d46d`, frozen core `9ee7e700`, all 568 production-source
hashes equal the source at workload commit `e5262da5`. The existing fixed `fec79afd`
worker consumes the new catalog; no worker/runtime/adapter substitution. Stock
CPython 3.13.16, 2026-11-11, quiet Azure F4as_v7/Ubuntu 24.04.4, four AMD EPYC
9V45 vCPUs, 16 GiB/no swap; actual .NET 11 RC3 with normal GC/tiering.

Four jobs finish in **114.763 seconds**. Four independent engine/control processes
per job, five-second warmups and seven rotated 150 ms rounds. All identities,
exact stdout/file goldens, shared-budget balances, quiet, Closed/zero-exit/join,
RAM cleanup and remote/local archive SHA gates pass. [Medians, IQR and wider CPU](quiet/summary.json),
[raw observations](quiet/observations.tgz), [receipt](quiet/archive-receipt.json).

| Workload / phase | Lython | CPython | Lython / CPython | Status; L/P control / max IQR |
| --- | ---: | ---: | ---: | --- |
| Patch / compilation | 33.46–33.91 µs | 33.86–34.21 µs | 0.98–1.00× | Diagnostic; 1.34%/1.06% /0.62% |
| Patch / warm | 0.497–0.520 ms | 0.576–0.580 ms | 0.86–0.90× | Variable; 4.44%/0.74% /1.16% |
| Patch / compile-to-result | 0.556–0.558 ms | 0.647–0.651 ms | 0.85–0.86× | Diagnostic; 0.20%/0.65% /1.40% |
| Unchanged config / warm control | 77.83–78.46 µs | 61.56–61.98 µs | 1.26–1.27× | Diagnostic; 0.81%/0.69% /2.41% |

Patch input is two 1,024-line UTF-8 files, 67,597 B total. Ratios include all four
pairings of independent medians, not confidence intervals. Warm and complete
include mediated RAM-file I/O, exact output validation and prior-result cleanup;
exclude fixture reset, protocol, startup and final cache disposal. Compilation
includes construction/disposal. Phase medians need not add. Wider process CPU
includes surrounding work and coarse tick resolution; no internal CPU attribution,
individual-optimization gain, RSS, pending-I/O or embedding-tail claim follows.

The fixture is a single central edit between otherwise unique lines; it does not
establish arbitrary diff throughput. No new difflib profile or shortcut is selected
from this result. The captured-helper text gap remains a stronger separate lead.
Earlier Windows correctness smoke uses a different CPython version/conditions;
its clocks remain unqualified rather than being merged with this comparison.

The bounded owner has exited successfully; [terminal status](quiet/terminal-status.json)
is cached and must not be queried again. Final free space 265,469,952 B preserves
the fixed 240 MiB floor. Setup retired only the exact committed B572 compressed
archive and both aliases of obsolete `f6c8fd22` core after canonical SHA/path/UID/
link/inode proofs. [Recovery](quiet/space-recovery.json). Those remote paths are
off limits; qualified `9ee7`/`c271`/`9dadd` images survive unchanged. No sole evidence
was removed. [Source proof](quiet/source-identity.json), [preparation](quiet/preparation.tar.xz),
[archive hashes](quiet/archive-identity.json). No repeated unchanged CI or package qualification.
