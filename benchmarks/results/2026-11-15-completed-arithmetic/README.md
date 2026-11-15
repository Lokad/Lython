# Completed arithmetic — active coherent prototype

2026-11-15, uncommitted **9e890d18** versus fully qualified **51e3c2c1 /22e5b72c**.
R405 separates completed builtin arithmetic from generic asynchronous operand
handling. It reuses the existing integer/chain kernel, shares remaining builtin
arithmetic and operand reads across execution forms, and removes duplicate native
selectors/reads and the old numeric probe in the generic handler. Dynamic
operators retain the existing suspending path. No new controller, owner, value
layout, option or dependency. The prototype is **not retained or fully qualified**.

| Complete workload | Elapsed change | Wider process CPU change |
| --- | ---: | ---: |
| Invoice | −3.84..−2.29% | −3.30..−2.14% |
| Fulfillment | −1.30..0.00% | −1.56..+2.00% |
| Captured text control | −6.12..+6.87% | −4.09..+5.02% |
| JSON control | −0.20..+1.08% | −2.00..0.00% |
| Protected search | −7.61..+0.03% | −6.02..+0.45% |
| Helper-rich configuration, compile-to-result | +0.71..+11.31% | −5.53..+20.39% |

B790 finishes six jobs in172.203s on the established quiet F4as_v7 Ubuntu24.04.4/
.NET11RC3 VM. Two processes/source, three-second full-request warmup (eight for
text), five rotated approximately0.5s batches. All process-median pairings stay.
Invoice controls are stable (reference0.68%, candidate0.92%spread). Text controls
move6.49/6.91%; search reference6.84%; configuration candidate9.43%.
The invoice benefit is modest; do not call the other rows stable gains or dismiss
the adverse configuration process. One same-bits text/fresh confirmation with
longer full-request warmup is selected before the retention decision.

Warm elapsed includes synchronous Run, mediated RAM I/O, full stdout/file goldens
and prior-result cleanup. It excludes compilation, startup, reset/protocol and
final cache teardown. The existing1877B six-helper configuration/manifest source
includes compilation and compiled-owner disposal; it is not the earlier empty
configuration fixture. Wider process CPU also includes reset/protocol. R402's
async-default correctness correction is retained alongside R405; this source pair
is not isolated causal attribution. No new CPython, pending-I/O, heap/RSS or
embedding-tail comparison is claimed.

The unchanged Windows complete-job allocation driver compares qualified6e276759/
45dd2da1 separately and is effectively flat for all four workloads. Shared
arithmetic does not claim an allocation reduction. A small Windows config JIT
diagnostic captures a92IL-byte shared reader and its optimized version, but no
optimized prefix within that short capture. Code size and tier observations do
not establish the CPU cause of the moving controls; source stays unchanged for
the selected confirmation. Every original request/dump remains.

Corrected focused Release passes132internal .NET11,164internal .NET10 and401public
on each actual target, including exact promotion, identities, Python operators,
actual reflected-operator I/O, cancellation, terminal refusal and retained funding.
An attempted locals-field removal causes51public failures: that field also holds
the binding-loan return handle after operand retirement. The original handle is
restored; assertions stay and no new owner/lifetime abstraction is added. Original
failed TRX and build/procedure conditions remain. Full platform/original-stress/
installed-package qualification follows only a retained runtime decision.

All VM identity/golden/quiet/ownership/Closed/exit/join gates pass. Observations
are archived/verified under lease and RAM fixtures removed. Exact archived
working aliases are retired with all-alias/inode/link/UID/hash proof; canonical
evidence and qualified images stay. B790 is terminal cached, never requeried.
The original early local retrieval guard failure does not restart or observe the
service; it remains preserved. Raw identity inherits one old descriptive commit
annotation; the linked identity corrects it and original raw metadata stays.

[All medians and controls](summary.json), [allocation](allocation-summary.json),
[source mapping](identity.json), [current decision](decision.json),
[original VM observations](observations.tgz),
[exact core/source, manifests, checks and procedures](sources-checks-and-procedures.tar.xz).
