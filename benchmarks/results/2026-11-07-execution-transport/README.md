# Canonical execution transport — R29 source gate

2026-11-07 UTC, current production c4729a9f/source-identical at3b7512d8,
frozen R269e1dbc38 and unchanged b94fa98c worker/host/optional0.4 provider.
This is CPU attribution, not a source-pair timing, allocation result or new full
qualification. The existing R26 delivery receipt remains valid.

One maintained text/helper script on the quiet AzureF4as_v7/Ubuntu24.04.4/.NET11RC3,
ordinary GC/tiering. Existing complete checked requests include mediated RAM I/O,
output/file goldens and prior-result cleanup; compilation/startup/staging/reset/
final teardown are outside. Six-second warmup,2s settling,6s wall capture; the
worker records5.586s job clocks and actual guest thread5.51s CPU.199Hz cpu-clock/fp
and PerfMapEnabled3, with selected JIT disassembly from that same process. These
diagnostic settings do not establish ordinary-clock performance.

All1,095 actual guest leaf samples are retained;233 fall inside the prefix and
map to one unique exact-size Tier1 listing (11,107B). No malformed map lines or
unmapped prefix samples.76 unknown leaves remain. Mapping uses native range/offset
and basic-block boundaries, retaining raw addresses/code. Sampling skid, inlining
and small counts prevent treating every attributed instruction as causal cost.

| Observed group | Samples /all1,095 | Implication |
| --- | ---: | --- |
| Loop entry/reload, fetch/check block and switch dispatch (IG07/08/09) |118 /10.78%| Includes required cancellation/refusal checks; not wholly removable. |
| Whole instruction prefix |233 /21.28%| Includes useful operand, call, iteration and control operations. |
| CompleteLocalResult |36 /3.29%| Publication also owns cleanup and a GC-lifetime fence. |
| BeginPendingCall |14 /1.28%| Existing native helpers can complete without a function frame here. |
| ResumePendingCall |14 /1.28%| Completed helpers still manufacture state and reenter the prefix. |

Select a coherent completed-call phase migration: move the existing exact ordinary
positional/unbound native attempt into TryExecuteNativeCall and delete its branch
from BeginPendingCall. Keep the same native evaluator, input borrow and stack/
recursion/exception checks. Other bindings/protocols and genuine suspension retain
the existing runner. This avoids synthetic pause/completion for operations proved
unable to suspend; it is not a new dispatcher, caller arena, value representation
or explanation of the whole text gap. No speedup is predicted. Detailed ownership,
read-order and displaced-code obligations are in [source-contract.md](source-contract.md).
Implementation/focused checks and controlled complete-script comparison remain
active; neither the source gate nor successful profiling completes the objective.

Loaded core/auxiliary hashes, pre/post verification, quiet endpoints, final funding,
Closed/exit0, joined users, removed guest fixtures and persistent archive verification
under the owned lease all pass. Acknowledged02:59:09UTC; observation at03:02:14UTC
is after the full120s owner+30s stop+30s margin. No later service queries. Trace,
maps, code, all threads, source inventory and pre-capture plan survive in
[observations.tgz](observations.tgz); [receipt](archive-receipt.json),
[offline mapping](prefix-blocks.json), [mapping source](map-prefix.py).

Before staging, exact withdrawn R28 expanded images and their static candidate
aliases are retired only after backup commit/push3b7512d8, current hash/user/link
validation. Exact binary bits and all original/adverse captures remain committed.
This source gate does not reopen backend qualification or publication.
