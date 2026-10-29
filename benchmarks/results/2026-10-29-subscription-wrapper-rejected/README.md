# Rejected completed class-subscription wrapper

Base **3eb6c3b897f5f45710475ac01e74ae536904600a**, uncommitted patch SHA256
**9bfcf294a7a9b2fbf19e499f23618eecf213a43275113bbb76703633bf854f63**.
This is a frozen rejected experiment, not current runtime code or a speedup.

The [Ubuntu CI failure](https://github.com/Lokad/Lython/actions/runs/40929314122/job/123643196316)
times out two newly added stack-guard fixtures at their unchanged 25-second bound.
Each makes three identical default-depth recursive failures after actual host
suspension. Windows passes; dedicated Ubuntu fixture times are about 18–19 seconds.

Hypothesis: remove the new subscription helper's async state for completed
binding/invocation, including completed faults, and retain awaited tails only for
actual suspension. Both-target builds and Windows focused **84 internal + 424
public checks per target** pass. The candidate adds three helper methods.

One unchanged-fixture comparison on the dedicated Ubuntu Standard_F4as_v7,
.NET 11 RC3, ordinary GC/tiering, full three failures/recoveries and the same
25-second owned-process bound gives:

| Scenario | Control seconds | Prototype seconds |
| --- | ---: | ---: |
| class-getitem | 17.542 | 15.820 |
| class-getitem-descriptor | 18.193 | 18.055 |

All four processes complete with genuine suspension, success and result 3.
These are single diagnostic observations, not qualified performance claims. The
descriptor witness barely changes: the added machinery does not establish a
solution to CI headroom. **Reject and restore the production helper.** The live
prototype worktree is removed. No runtime constant, deadline or test is weakened
to promote it; no retiming or extra wrapper workaround follows.

[prototype.tar.gz](prototype.tar.gz) preserves the exact patch, driver and raw
comparison with core hashes. The subsequent test correction uses one full-depth
rejection plus three successful same-protocol calls, a stronger admission-reset
check than native len(). It changes fixture work, not runtime performance, and
retains the ordinary deadline, default stack/nesting bounds and original stress.
