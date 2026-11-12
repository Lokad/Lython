# Final-byte text storage and bounded output batches

2026-11-12. **Refined design retained; full delivery qualification pending.**
Reference f4f44caf/09ad is fully qualified, unchanged by receipt32f26ba9. Frozen
production sources and precommit core metadata identify each form. Core stays
managed/safe; normal optional adapter0.4 and common fec79 worker unchanged.

The first form shares codec sizing/writing, removes normalized UTF8 staging,
three position counters and flush-time encoding/payload reservation. It passes
1542focused actual11/10 and four complete scripts/bothAPIs/compile. UTF8 allocation
is flat; UTF16 report allocation **increases6.84..6.87%**, peak+208026B, from geometric
whole-file capacity. Desktop sync UTF16+20..23% and adverse/variable controls remain.
Old local-helper capture is a **value type**; no removed heap allocation is claimed.
Original architecture-specific failures and the independent Python golden failure
survive with corrections. Frozen f4 also reports encoding errors against original
rather than newline-translated subjects/indices; corrected cold behavior and
eight independent CPython3.13.2 goldens are independently valuable.

Batching completes the storage design: existing file ownership admits all working
capacity before the first batch; later chunks reuse it. Explicit flush/close
releases storage. Full-input strict validation precedes effects. BOM/payload and
multiple batches preserve logical accepted-prefix failure handling. Ordinary and
exposed writes, writelines and CSV/JSON await genuine I/O with actual input/scratch
and cleanup still funded. Filled staging can publish before explicit flush. This
is not an output ceiling or general guest-reference reclamation. No hidden
allowance, tracing, refused-admission retry, pool or new owner is introduced.

This form passes **580internal/1707public per actual11/10 =4574checks**, plus four
whole scripts through bothAPIs and separate compilation. Tests cover bounded
repeated UTF8/UTF16 output under192KiB, terminal refusal before mutation, accepted
BOM prefixes and genuinely pending writes from five producer routes through late
cancellation/actual close. Physical report allocation falls **12.68%UTF8 /19.16%UTF16**
versusf4; UTF16 admitted peak493734B lower. CSV/text and compilation allocation are
flat. All desktop results remain, including textAsync+18..23%(control4.31%) and
reportcompile+9.2..9.6%(control0.34%). Allocation does not establish CPU improvement.

## B607 quiet source pair

Ten jobs finish in **277.347s** on quiet AzureF4as_v7/Ubuntu24.04.4/.NET11RC3,
ordinary GC/tiering. Two independent processes per image,5s warmups and seven
rotating150ms rounds. Negative is less time; four process-median pairings are not
confidence intervals. All identity/output/file/allowance/quiet/join/RAM/persistent-
archive gates pass. Terminal cached; no remaining workers or fixtures.

| Script/API | Elapsed change | Wider CPU change |
| --- | ---: | ---: |
| UTF16 report/Run | -2.95..+3.51% | -3.69..-0.78% |
| UTF16 report/RunAsync | -0.21..+2.18% | -3.59..+2.50% |
| UTF8 report/Run | -3.50..-0.47% | -1.96..+4.49% |
| UTF8 report/RunAsync | -2.24..+4.69% | **+2.00..+8.61%** |
| CSV/Run | -3.87..-0.03% | -3.03..+3.43% |
| CSV/RunAsync | -0.18..+0.89% | 0% |
| Ordinary text/Run | -1.08..+3.92% | -1.52..+3.85% |
| Ordinary text/RunAsync | -6.53..-1.48% | -3.94..-0.80% |
| Report/compile | -2.22..+0.61% | -5.81..+3.13% |
| Ordinary text/compile | -0.46..+0.70% | -6.58..+0.25% |

Additional async UTF8 report CPU debt remains material; reference control5.47%
also leaves elapsed uncertain. Do not compound prior debts with different bases.
A source-justified refinement keeps completed receiver calls outside an async
guard/boxing state machine, retaining only actual pending operations. It needs
focused checks and a bounded affected/control comparison before retention. This
is not unchanged retiming or a fixed-percentage veto.

Warm includes public invocation, mediated RAM-file I/O, full validation and prior-
result cleanup; excludes startup/compile/reset/protocol/final cache disposal. Wider
CPU includes reset/protocol. Desktop includes reset/immediate disposal. No new
CPython/RSS/embedding-tail/large-file-throughput claim. Publication stays held.

First staging inventory falls311296B short of the unchanged free-space floor and
stops before mutation. Additional exact committed raw records provide headroom
under fresh idle/UID/path/inode/all-hardlink proofs. Superseded0e715aliases and exact
B606raw/catalog/B605records retire only after canonical committed Git/SHA checks;
selected09ad and prototype cores stay protected. All original failed conditions
and adverse data remain outside product documentation.

[First form](first-form/diagnostic-identity.json),
[batching source](batching/diagnostic-identity.json),
[quiet summary](batching/b607/summary.json),
[raw observations/core](batching/b607/observations.tgz),
[terminal receipt](batching/b607/archive-receipt.json).

## Refined completed-operation entry: retention decision

The same guard now consumes completed receiver operations directly and retains
only an actually pending operation. File-write boxing likewise takes a completed
path. This removes `CanSuspend`/the separate async receiver guard without another
dispatcher or ownership layer. Focused checks broaden to **947 internal/3361
public per actual .NET11/10 =8616**, all passing. Four complete scripts through
both APIs and separate compilation pass outputs, files, allowance and cleanup.
Report allocation remains about **−12.7% UTF8 /−19.16% UTF16**, encoded peak
**493734B lower**. Compilation/control allocation stays flat.

B608 finishes six affected/control jobs in **169.344s**, all identity/output/
ownership/quiet/join/RAM/archive gates passing, with the same four-process design.

| Script/API | Elapsed change | Wider CPU change |
| --- | ---: | ---: |
| UTF8 report/RunAsync | −3.77..+1.37% | −5.88..−3.96% |
| UTF16 report/RunAsync | −1.22..−0.61% | −5.88..−2.44% |
| UTF8 report/Run | −3.55..+0.44% | −2.00..+4.49% |
| UTF16 report/Run | −3.07..−0.07% | −5.88..+2.38% |
| Ordinary text/RunAsync | −3.11..+3.65% | −3.15..+3.17% |
| CSV/RunAsync | +0.03..+1.52% | 0% |

Retain the coherent final-byte/bounded-buffer migration with the completed-call
refinement. Accept small CSV elapsed debt and uncertain sync/text clocks for the
storage simplification, report allocation reduction and supported async report
CPU result. Separate collections cannot establish that the refinement caused
B607's adverse CPU result to disappear. There is no general CPU/equivalence claim.
Different-base earlier debts are not compounded. Publication remains postponed;
full retained-source platform/stress/installed-consumer checks follow.

All refined desktop adversity survives, including report Run+9..15% with5.18%
unchanged control and CSV compile+3.85..5.75% with1.82% control. Original whole-file
and first batching evidence is unchanged. B608 retires only exact committed
initial B607 core aliases/raw/catalog/records after fresh lease/UID/path/inode/
all-alias/canonical Git/SHA proofs; selected09ad protected, free261931008B.
Terminal cached, no remaining users/RAM fixtures.

[Refined sources and checks](refined/diagnostic-identity.json),
[decision](refined/decision.json), [quiet summary](refined/b608/summary.json),
[raw observations/core](refined/b608/observations.tgz),
[terminal receipt](refined/b608/archive-receipt.json).
