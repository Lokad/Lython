# Cursor-owned cased capacity — changed-source refinement

R408/B795,2026-11-15, **0edc0c1c** versus qualified **43bce13a/9e890d18**.
Restore the general byte builder byte-for-byte. The existing cursor directly owns
its private byte array and admits conservative capacity/replacement overlap only
between lines. Old scratch is not copied; written length stays in scanner state.
A standalone builder object and unrelated growth/call changes disappear. Final
ranges/independent eventual words, eager funding and the single scan/case pass
remain. No new owner, allocator, pool, option or dispatcher. **Not retained yet.**

| Complete workload | Elapsed change | Wider process CPU change |
| --- | ---: | ---: |
| Captured text / warm | +0.75..+5.34% | +0.45..+7.51% |
| Captured text / compile-to-result | −7.91..+0.08% | −8.79..+0.28% |
| Direct text / warm | −7.65..−3.96% | −7.94..−2.55% |
| Fulfillment control / warm | −0.45..+1.05% | −1.96..+0.43% |
| JSON control / warm | −2.95..+0.94% | −4.02..+0.84% |

All process-median pairings stay. Captured warm reference/candidate spreads are
3.57/0.95%; fresh4.72/3.77%; direct1.09/2.88%; fulfillment0.55/0.95%; JSON2.78/1.21%.
Direct text supports a useful complete-script gain, captured warm remains adverse,
fresh/control magnitudes are uncertain. Mixed JSON does not prove that the earlier
adverse source recovered because of a specific method change. Allocation remains
effectively flat (+128B/+0.029%captured text). Several-times-output scratch and
earlier long-line denial remain explicit conservative ownership tradeoffs.

Five jobs finish259.695s with the same12sfull-request text/5scontrol warmup,
five rotated0.5s batches, two processes/source, scripts, inputs, full goldens,
VM/runtime/host and clock boundaries as the [original collection](../README.md).
No original process or failed condition is discarded.196internal/439public per
actual.NET11/10 include the original3×64MiB stress cases, Unicode/context,
retained funding, actual suspension, cancellation, first refusal and cleanup.
The full nonincremental solution build precedes these checks and immutable images.

**Bounded same-bits confirmation selected:** captured warm/fresh and JSON only,
original0ed/9e images, before the retention/debt decision. Do not repeat the stable
direct-text comparison, the entire five-job matrix or unchanged correctness/full
qualification. Useful gains, adverse captured warm and wider-capacity costs must
all inform the decision; a fixed-percent gate does not decide it automatically.
No new CPython, startup, heap/RSS, pending-I/O or tail claim.

All identity/golden/budget/quiet/Closed/exit/join/archive gates pass; no users or
RAM fixtures remain. Only the exact committed archived previous candidate and
working observation duplicates are retired with path/UID/hash/link/lease proof.
Current qualified images, source/evidence and toolchains remain;254/252MiB floors
stay. B795 is terminal cached forever. Full delivery/publication is pending.

[All controls](summary.json),[allocation](allocation-summary.json),[image/source identity](identity.json),
[raw](observations.tgz),[cleanup](archive-receipt.json),
[exact refined core/source/checks/procedures](sources-checks-and-procedures.tar.xz).
