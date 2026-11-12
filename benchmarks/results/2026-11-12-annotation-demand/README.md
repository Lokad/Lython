# Shared frontend annotation demand — retained for delivery

R270, 2026-11-12. Four frontend files over committed8233c969. The existing lexical
walk records whether annotations need checks and whether regex bindings require
full conservative flow. Ordinary scripts skip the duplicate Prepare traversal and
body-position dictionary. Regex scripts enter full flow directly; annotated
scripts retain their real last-check preparation. Two transient ScriptSyntax
flags add eight bytes; no new pass, runtime cache, analyzer, option or public API.
Cancellation/refusal checks accompany the shared walk. The displaced rediscovery
work disappears, with no weakening of dead/nested-scope analysis.

816 focused checks pass on actual .NET11/10 (37internal/371public per target),
including annotation/default/return/class diagnostics, lexical facts, constant
regex imports/aliases/provider ownership, cancellation and refusal. A new test
initially expected an unused unannotated return to be statically resolved;
that is not the existing annotation analysis route. The corrected case uses a
local pattern assignment, preserving nested-import coverage. Original failing
expectation/log remains. No unrelated semantic expansion is claimed.

Five complete scripts pass separate compilation, compile-plus-one-run and warm
execution, with full stdout/files, shared balances and cleanup. Actual annotated
search and regex/config controls have independent CPython3.13.2 goldens. The
existing JSON helper is itself annotated and remains a genuine full-flow control.
Compile allocation falls432bytes/report(0.38%),456bytes/untypedsearch(0.52%) and
208bytes/regexconfig(0.14%); annotated controls add only the eight-byte root flags.
Warm allocation is essentially flat. Desktop compile/fresh controls move up to
19–44%; report warm+7% and regex warm+13–16% remain adverse. No desktop CPU claim.
The phase/boundary clarification preserves all raw common-prose metadata and
actual clocks; no retiming followed from a label correction.

## B621 combined quiet confirmation

Fixed fec79 worker/adapter0.4, normalGC/tiering, Azure F4as_v7 Ubuntu24.04.4/.NET11RC3.
Reference is qualified da9/a552; candidate includes both R269 and R270. These
observations do **not** isolate the frontend change. Four complete jobs finish
127.504seconds;5swarmups per process,7rotated250msrounds, two processes per image.
All identities/goldens/balances/quiet/join/RAM/archive/localSHA gates pass.

| Complete script / phase | Elapsed change | Wider process CPU change |
| --- | --- | --- |
| Untyped search / compilation | −1.89..−1.25% | −3.58..+1.86% |
| Regex/config / compilation | −2.80..−0.55% | −5.16..+0.65% |
| Report / warm Run | −0.74..+5.91% | −2.44..+5.30% |
| Native search / warm RunAsync | −4.81..+2.53% | −3.72..+3.68% |

Ranges span all four process-median pairings, not confidence intervals. Compilation
controls are tighter; CPU crosses zero. Report candidate spread6.11% and native
async reference5.51% leave warm impact unresolved. These do not confirm earlier
desktop adversity or prove equivalence. Warm includes mediated RAM I/O/full
validation/prior-result cleanup; excludes compile/startup/reset/protocol/finalcache.
Compilation includes disposal. Wider CPU includes reset/protocol and sharedJIT/GC.

Retain the shared fact/demand contract and actual removal of repeated work. Accept
explicit warm uncertainty for this milestone; do not claim a substantial whole-
script speedup or attribute the combined observation to one change. Final platform/
stress/installed Release consumers remain pending. The broader performance
objective continues; existing scalar/container/operand work is not replayed.

The first read-only staging inventory lacked151552bytes for redundant before-
source inventory plus metadata. No remote worker or mutation occurred. VM staging
now retains the complete current inventory and points to the full before/local
source archive, omitting only redundant metadata; OS240MiB/evidence12MiB floors
stay unchanged. Original config/error is kept. Withdrawn a90 prototype and exact
B620 duplicates were retired with canonical committed Git/SHA, fresh idle lease,
UID/path/inode and complete known-alias/link-count proof, without a broad inventory.
Current a552/newcandidate are protected. Terminal B621 is cached; no live users or
sole RAM evidence remain. Publication remains postponed.
