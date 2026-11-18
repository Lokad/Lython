# Current original/helper call-entry attribution

R514/B941,2026-11-18UTC, committed10ee56c7/measured2ede09fc, now fully qualified.
Corrected batch256; same8guestsecond warmup/5second capture per case/12guestsecond
total capture guard. Original[128x64failure](../initial/README.md) remains separate.
32-file hashes/actual custom two-case goldens pass; defaultGC/tiering plus perf
maps only. All users joined, RAM evidence SHA-archived/verified before unmount.

Original:988all-thread samples/5,888invocations/4.641guestseconds; helper-list:
1,016samples/4,096invocations/4.821guestseconds. Boundaries include reset, mediated
I/O, checks, protocol and cleanup, wider than execution clocks. Unknown leaves:
206/186. This is source attribution, not a controlled performance comparison.

Ordinary calls already bind final slots/borrow operands. The341helper inclusive
function-entry samples contain body/receiver work and cannot be treated as a
removable stage. The hottest StringMembers .cctor b__81_7 wrapper actually calls
expandtabs, confirmed against the exact measured[IL](sampled-wrapper.il). Sampling
and inlining do not justify deleting safety guards or selecting a complete new
call controller. Heuristic overlapping groups are navigation aids; broad
ExecutionState matching is not isolated invocation-setup cost.

Next bounded construction milestone uses existing known-size facts for private
drains: remove initial geometric growth/copy/admission for fixed-size sequences;
preserve live mutable iteration, underestimates, first refusal and awaited cleanup.
No new arena/owner/cache/buffer framework. Controlled source-pair allocation and
CPU checks decide retention; no gain is established here. Wider roadmap remains
active; normal adapter0.4/core dependencies unchanged, publication postponed.

[Decision](decision.json),[attribution](attribution.json),[raw](observations.tgz),
[identity](identity.json),[checks](actual-worker-preflight.json),
[custody](archive-receipt.json),[procedures](procedures.tar.xz).
