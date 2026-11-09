# Native hash stress scheduling

R141,2026-11-09. CI42567067318/source23e passes full Windows/Ubuntu actual.NET11/10
and installed-core package consumers. Ubuntu original Stress both targets passes.
Windows.NET11 public Stress406passes, internal5pass/one text-hash failure; Windows
.NET10 Stress was skipped. Terminal/failed log retained, never silently rerun green.

The1ms timer fixture can finish a64MiB hash before the timer requests cancellation.
A local diagnostic confirms this on.NET10: completion flagfalse even after warming
the timer. That is a missed scheduling window, not evidence of ignored cancellation.
Keep the failed warm-timer experiment/logs and original fixture as evidence.

Replace timer/pool dependency with an already-started dedicated canceller. Its
one-ms delay follows the scan start signal. Use one256MiB buffer (same32MiB budget
headroom) so useful native work spans a scheduling quantum. This remains concurrent
Stress, not a deterministic scheduler guarantee.14small checks remain ordinary.
No timeout relaxation, ignored result or retry; a missed window still fails with
the token state at completion. Same exact cancellation reason/span, unpublished
cache, later caller, input contents, depth and memory balance assertions. Await
both workers on all paths before input/event ownership ends.

16native checks and all6internal original Stress cases pass peractual11/10. Mutation
removes only the two in-loop checks, retaining entry: both native cases fail on
both frameworks with cancellationRequestedAtCompletion=true. Restore exact runtime
bytes, force nonincremental builds, then repeat the relevant checks successfully.
No production code, checkpoint, memory estimate, API or performance claim changes.
Corrected [CI42573178034](delivery-ci.json), source7aa637bf, is terminal success:
Windows/Ubuntu actual.NET11/10, installed-core consumers and original Stress on
both targets pass. Original failures and mutation evidence remain. No further
query of that terminal handle. Publication postponed.

[Original CI](ci-terminal.json), [failed stress log](ci-failed.log),
[original fixture](original-test.cs), [change](source.patch),
[original failed warm attempt, mutation, fresh build and final test logs](validation.tar.xz).
