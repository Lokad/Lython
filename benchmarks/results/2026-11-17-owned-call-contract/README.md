# Owned callable arrays and borrowed scratch

R487/B904,2026-11-17UTC. Corrects the
[failed first delivery](../2026-11-17-call-copy-ownership/delivery-first-failure/README.md).
Qualifieddbdf8262/frozena95ff2a4 versus four-file source onf2cb4008/frozen6c808cd8;
585production/2382build-test inputs. No public API change or wider argument view.

The owned callable ABI permits retaining a fresh array after return, failure or
cancellation. Reserve before allocation and keep its charge with the existing
execution/result owner. Never clear/refund at call return. Ordinary wide calls
instead use the borrowed entry, with scratch funded through actual completion;
constructors keep their multi-stage owned packet. Direct function binding stays.
The incorrect completion wrapper, pending-owner flags and temporary-reservation
objects for retained copies disappear. No new lifetime layer, classification
switch, tracing, retry, quota or abandoned background work.

Conservative retained-capable copies accumulate charges until owner cleanup,
even if a particular callable did not retain its array. This can deny earlier in
one long command. Bounded host executions remain the ownership boundary; exact
withdrawal recovery is not claimed. Borrowed storage still reclaims after use.

Final nonincremental Release build succeeds. Actual Windows11/10 each pass
99internal/239public focused cases, including the unchanged retained-array checks,
ten copy ownership cases, constructors/protocols, real suspension, cancellation,
terminal refusal and cleanup. A preliminary check overlapped compilation and is
preserved separately, not used as final qualification. Both actual copied32file
workers pass catalog goldens versus CPython3.13.2 and owner-reuse diagnostics.

| Complete workload / phase | Elapsed change | Wider CPU change | Reference/candidate control spread |
| --- | ---: | ---: | ---: |
| Bound-wide max diagnostic / warm | +2.19..+3.38% | +3.47..+6.73% | 0.85/0.31% |
| Same / compile and run | +2.11..+4.40% | -0.52..+4.49% | 0.88/1.35% |
| JSON-lines helper aggregation / warm control | -0.28..+0.09% | -1.96..+0.73% | 0.12/0.25% |

Affected1025bound calls allocate essentially the same bytes (−24B), while accounted
peak rises3079→298471B because retained copies now stay charged. JSONallocation
changes−118B and accountedpeak+64B. All owner baselines restore. Accept this required
funding/retention cost explicitly; no CPU improvement claim or unchanged campaign.
Ranges combine four independent process medians, not confidence intervals.

Quiet AzureF4as_v7/Ubuntu24.04.4/.NET11RC3, normalGC/tiering,3jobs116.43seconds.
Six-second rotated warmups/five samples/four processes. Mediated I/O, complete
goldens and prior-result cleanup included; compilation only fresh. Startup,
fixture reset/protocol and final cache teardown excluded; wider CPU includes
request work outside the guest clock. All quiet/golden/Closed/exit/join/archive
gates pass; RAM archived/SHAverified before unmount, terminal cached.

Original disk-floor refusals occurred before mutations/measurement and remain
preserved. Staging repair retired only exact committed replicas after fresh
path/UID/hash/link/all-alias and idle-lease proof, including the failed384core and
one historical archive replica. Qualifieda95/toolchains/canonicals and254/252MiB
floors remain. Full corrected platform/stress/installed-package delivery is pending.
Publication held; the broader performance objective remains active.

[Decision](decision.json),[sources](candidate-sources.tar.xz),[catalog](catalog.json),
[summary](summary.json),[allocation](allocation-summary.json),[raw](observations.tgz),
[identity](identity.json),[checks/procedures](procedures-and-checks.tar.xz).
