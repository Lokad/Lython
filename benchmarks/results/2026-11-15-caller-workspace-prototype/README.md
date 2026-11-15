# Borrowed caller workspace — withdrawn

R411,2026-11-15, private6b6cfe6f versus qualified1b82cd46/frozen0edc0c1c.
**Not retained. All586production inputs are restored.** The borrow route, extra
entry/stack helpers and six mechanism-specific tests are removed; no dormant
arena, controller, owner, option or changed contract remains.

The [matched decomposition](../2026-11-15-helper-boundary-decomposition/README.md)
establishes existing caller capacity6slots for a5-slot helper. The prototype lends
only the unused funded tail, preserving operands/unpacked-return windows. Real
continuations obtain independent funded storage before the borrow is cleared.
Fixed managed storage remains when capacity is insufficient. No growth/new pool.
It removes repeated buffer construction but adds routing/common calls and clearing.

Focused103internal/98public each actual11/10 pass, including new borrow guards for
completion/fault/named binding/continuation copying/first refusal, plus stack and
real suspension/cancellation/ownership. Original test evaluation-order failure
(expected AdmissionFailure read before invoking admission) and corrected assertion
are preserved; no assertion is relaxed. No full package qualification is inferred.

| Complete warm script | Elapsed change | Wider process CPU change | Reference / candidate spread |
| --- | --- | --- | --- |
| Captured text helper/counter |−2.39..+4.44%|−3.25..+3.93%|0.04 /6.96%|
| Helper without counter |+1.23..+3.79%|+0.98..+2.91%|1.33 /1.19%|
| CSV invoice |−0.52..+0.77%|−1.92..+0.62%|0.64 /0.65%|
| JSON helpers |+0.49..+1.83%|+0.71..+2.14%|0.49 /0.84%|

Separate local allocation diagnostic is flat: helper447,461.75B each; invoice
4,197,002.5 versus4,197,006B. No allocation reduction claimed.

Withdraw because stable plain-helper/JSON results are adverse, captured text is
uncertain, allocation is flat and added machinery offers no demonstrated benefit.
This cost/complexity decision is not a fixed-percent veto or proof that every
call/frame redesign fails. Clarifying captured variance alone would not remove
stable adverse evidence elsewhere; no unchanged retiming is selected. Different
complete designs remain eligible. The broader performance objective is open.

B803 finishes197.523s on quiet F4as_v7/Ubuntu24.04.4/four EPYC9V45vCPUs,16GiB/
no swap,.NET11RC3 workstation GC/normal tiering. Two processes/source,12s
full-request text warmup/5s controls, five rotated0.5s batches. Every pairing and
adverse control remains. All golden/budget/quiet/Closed/join/archive/cleanup gates
pass; terminal cached, no users/RAM fixtures remain. Warm includes Run/mediated
RAM I/O/goldens/prior-result cleanup; compile/startup/reset/protocol/final cache
teardown excluded. Wider CPU includes reset/protocol. No CPython, pending-I/O,
heap/RSS or embedding-tail claim.

Initial stage refuses unchanged254MiB floor before mutation. Correction omits
unused catalog cases only, preserving the same four jobs/source/input/output/
settings and fresh complete-image proof. Three aliases of one exact committed
archived historical38c0 binary retire after hash/path/UID/link/all-alias proof.
Current images/evidence/toolchains and252MiB archive floor remain. Original
refusal/payload/config are preserved, not relabeled as a passing initial stage.

Restoration force-builds the entire Release solution nonincrementally, then passes
97internal/98public each actual11/10. All586production inputs match committed
HEAD after explicit newline normalization. Fresh build hashes are not relabeled
as measured0ed. Existing fullR408runtime qualification remains applicable; no
repeated full CI or publication follows an abandoned prototype.

[Medians](summary.json),[allocation](allocation-summary.json),[identity](identity.json),
[source/binaries/checks/original failures](source-checks-and-procedure.tar.xz),
[raw](observations.tgz),[restoration](restoration.json).
