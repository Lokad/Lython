# Direct range construction — R293 / B648

2026-11-13. Reference: qualified2d91eb1a, core161cd935. Candidate:8c584c2d plus
R293, core9e53b9fa. Exact570-file source inventory, built binary, patch, test
sources/raw logs and driver observations are in [source/check evidence](source-checks.tar.xz).
Both images use the same immutable fec79 benchmark worker and optional adapter0.4.

## Change and ownership contract

One existing execution-owned word-range cache now serves private cased iteration
and larger ordinary whitespace/separator splits. Collection grows directly in its
loans; replacements are admitted while old backing is still funded, then the old
loan returns. This removes growing scratch reservation/counter plumbing and the
private cursor's final inventory copy:12 net production lines removed, no new
pool, owner, configuration or backend. Small ordinary splits retain stack storage.
Private iteration starts with an eight-range loan, including empty input.

Idle inventory capacity stays charged until execution cleanup; oversized reuse and
minimum capacity can reject tighter allowances earlier. Final words/list backing
remain eagerly admitted before loop effects. Retained strings are independently
funded after cache shutdown. Active cursors keep loans until their actual users
finish, including cancellation and pending cleanup. First refusal remains terminal.

Focused actual.NET11/.NET10 checks:114 internal /431 public per target for affected
semantics, plus714 internal /183 public per target for broader admission/ownership
and cancellation. These are overlapping suites, not unique-test totals. Full seven
scripts pass sync/async, compile and compile-to-result goldens, zero final balances
and owned cleanup. Four new interrupted-growth cases and larger overlapping loans
pass. Two fixture adaptations preserve exact post-cache refund balances and Python
binding observation; refund tests no longer require an internal three-slot size.
The first fixture failures survive. A separate bounded cancellation-allocation
probe passed without an extra guard and is preserved only in experimental evidence.

## Controlled comparison

Quiet Azure F4as_v7 /Ubuntu24.04.4, actual.NET11RC3, normal GC/tiering. Four independent
reference/candidate/control processes, seven rotated500ms batches per job;12-second
captured-text warmup (five seconds for compilation/other controls). Complete text
input3,072 lines /35,840B; CSV5,120 records /68,625B. Compilation is separate. Warm
and compile-to-result include mediated RAM-file I/O, full stdout/file validation
and prior-result cleanup, excluding process startup, fixture reset/protocol and
final cache shutdown. Wider CPU includes outside-clock worker work. Ranges are all
four process-median source pairings, not confidence intervals.

| Complete script /lane | Elapsed change | Wider CPU change | Reference /candidate control spread |
| --- | ---: | ---: | ---: |
| text-summary-helpers.nonlocal-counter.large /warm | -3.04..+4.23% | -3.42..+4.76% | 4.29% /2.82% |
| text-summary-helpers.nonlocal-counter.large /warm-async | -6.34..+2.28% | -5.77..+1.43% | 4.36% /4.43% |
| csv-validated-records.large /warm | -0.53..+5.75% | -0.80..+8.01% | 0.97% /5.28% |
| file-search.large /warm | -4.49..+4.16% | -4.01..+3.80% | 8.13% /0.19% |
| text-summary-helpers.nonlocal-counter.large /compile | -1.49..+1.05% | -2.15..+2.61% | 1.77% /0.77% |
| text-summary-helpers.nonlocal-counter.large /compile-run | -9.06..-3.61% | -7.67..-4.37% | 2.64% /3.27% |

Whole-script allocation is effectively flat; source compilation allocation is
unchanged. The controlled compile-to-result row supports a roughly4–9% elapsed
improvement, with CPU support. Warm text crosses zero. CSV/search controls move
substantially; CSV elapsed−0.53..+5.75%/CPU−0.80..+8.01% does not establish equivalence
or isolate a regression. Preserve that uncertainty rather than claiming a universal
CPU win. No new CPython comparison is inferred from this source pair.

Retain the common construction/deletion and source-supported compile-to-result
benefit. Warm/CSV uncertainty remains delivery debt for later coherent checkpoints;
it does not trigger another small timing campaign or finish the performance goal.
Full platform/stress/consumer qualification passes on917e01d8; [cached delivery](delivery/README.md).
Publication remains held.

## Custody

Six jobs complete in320.712s under420s owner/30s stop/30s margin. Quiet gates,
outputs/balances, Closed/exit0, all joined users, RAM unmount and independently
verified persistent/local archive SHA pass. Input binary/sources are separately
preserved above; the raw archive does not duplicate those large inputs. Under a
fresh idle lease, exact task-owned archive/profile duplicates already committed in
Git were retired with path/UID/inode/link/size/SHA proofs. Current reference images
were protected; OS240MiB/evidence12MiB floors stayed unchanged. Terminal state is
cached; final owned free space265629696B, no workers or RAM fixtures remain.

[Raw observations](observations.tgz), [summary](summary.json),
[receipt](archive-receipt.json), [terminal status](terminal-status.json),
[exact identities](config.json). No RSS or embedding-tail claim.
