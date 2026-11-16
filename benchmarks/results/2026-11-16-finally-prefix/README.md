# Native work before finally cleanup

R423/B819, 2026-11-16 UTC. Retain **eee74a10** versus committed
**7d522f1f / 8f468f0a**. The reference's full qualification completed after staging;
the original staging identity remains unchanged. Only the core differs between
the copied, fully hashed images. Worker, optional released 0.4 adapter and all
auxiliary files are identical. No public API, backend or memory-policy change.

The compiler previously excluded every finally-containing helper from native
execution. Its existing instruction prefix now stops at the earliest physical
finally suite. Returns and abrupt jumps also transfer before observing or retiring
their inputs; direct tuple-unpack completion is disabled for those helpers. The
existing funded frame adopts the exact PC and live final slots, and owns all
cleanup, exception routing and actual suspension. Faults retain their original PC.
No second cleanup dispatcher, controller, argument arena or pending-exit state.
Context managers and generators retain their existing execution behavior.

This conservative frontier can stop before a particular control-flow path needs
cleanup, and any return in a finally-containing function transfers. It permits
CPU work before later frame admission, without promising identical tiny-budget
thresholds or transactional rollback of earlier effects. Pending users, aliases,
replacement overlap and the first refused admission remain funded/terminal.

## Complete-script comparison

Quiet Azure F4as_v7, Ubuntu 24.04.4, four AMD EPYC 9V45 vCPUs, 16 GiB/no swap;
.NET 11 RC3, normal tiering and natural workstation GC. Existing checked worker,
two processes/source, 5-second full-request warmups and five rotated approximately
0.5-second batches. Four jobs complete in 141.245 seconds.

The existing 4,096-record invoice script gains an observable `finally` counter in
its per-row `price_line` helper; every output/report byte retains its old golden,
plus an independently known count of 4,096. The whole new script independently
agrees with CPython 3.13.2. Ordinary invoice and JSON helper scripts are unchanged
controls. This is a source-pair diagnostic, not a new external timing baseline.

| Complete script / phase | Elapsed change | Wider process CPU change | Reference/candidate spread |
| --- | ---: | ---: | ---: |
| Invoice with finally / warm | −6.19..−4.78% | −7.09..−5.26% | 0.50/1.00% |
| Invoice with finally / compile-to-result | −2.36..+0.90% | −3.67..0.00% | 0.33/3.00% |
| Ordinary invoice / warm control | −0.39..+3.23% | −0.60..+1.37% | 0.36/3.27% |
| JSON helpers / warm control | +1.30..+3.47% | −0.56..+2.88% | 0.38/1.76% |

All four process-median pairings; positive means slower, not confidence intervals.
Warm protected-invoice improvement has supporting CPU evidence. Fresh timing is
inconclusive; adverse and moving controls remain unresolved/accepted debt. No
general helper speedup, control-regression recovery or isolated causal share is
claimed. A short local natural-GC allocation comparison independently establishes
**9,016,604 → 8,754,460 B/invocation (−2.91%)** for protected invoice. Ordinary invoice
is exactly flat; JSON's −448 B (0.022%) is not a meaningful reduction claim.

Warm includes Run, mediated RAM I/O, full stdout/file goldens and prior-result
cleanup; excludes compilation/startup, reset/protocol and final cache teardown.
Compile-to-result includes compilation, one execution and compiled-owner disposal.
Wider CPU includes reset/protocol. Local allocation includes full reset/goldens/
result cleanup but excludes compilation/startup. No pending-I/O timing, contention,
heap/RSS or embedding-tail claim. No CPython timings refreshed in this source pair.

## Correctness and delivery

Nonincremental Release solution build passes. Focused **105 internal / 223 public**
tests pass on actual .NET 11 and 10, including 16 new public cases. Eleven semantic
programs independently agree with CPython 3.13.2: fallthrough/early returns, nested
cleanup, alias mutation, overwritten tuple returns/faults, nonlocal effects,
break/continue, exception context, wide owned slots, handlers and private text
iteration. A supported `sys.exc_info()` probe preserves active caller-exception
identity on both frameworks; its original unsupported `sys.exception()` probe and
explicit diagnostic remain archived, not presented as a runtime regression.

Both APIs retain cancellation of nonallocating protected loops. Actual asynchronous
finally I/O preserves returned aliases and prefix effects, joins cancellation,
closes the file and refunds to baseline. Refused admission cannot be suppressed
by a finally return. Existing tuple, native arithmetic, protected execution,
managed-stack and ownership checks remain. Compiler facts identify the actual
invoice helper's native eligibility/frontier and unchanged controls; these source
facts establish routing, not timing.

Full image/golden/budget/quiet/Closed/exit/join/archive gates pass. Persistent archive
is hash-verified under the lease before teardown; no pending users or RAM fixtures.
Only five exact historical 0393331a replicas were retired, after committed archive
custody and fresh UID/path/hash/link/all-alias proof. Current 8f/eee images and
toolchains remain; 254/252 MiB stage/archive floors unchanged. The unexecuted
original launcher unnecessarily coupled development comparison to reference CI;
its bounded correction preserved all runtime/ownership/quiet gates and both sources.

[Full platform/stress/installed-package qualification passes](delivery/README.md); publication held.
The broader call/operand/collection/frontend objective remains active.
[Medians](summary.json), [allocation](allocation-summary.json),
[focused checks](focused-checks.json), [source/image identity](identity.json),
[raw observations](observations.tgz),
[source, binaries, original conditions and procedures](source-checks-and-procedure.tar.xz).
