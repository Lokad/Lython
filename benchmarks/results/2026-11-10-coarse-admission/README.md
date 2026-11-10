# R202 — coarse host grants (withdrawn), native index correction retained

2026-11-10. Isolated source on2cc36b45, beforeed5dbf70/candidatefedd1de7.
Both cores contain the same wide-enumerate correction and metric comments;
all568 production source hashes differ only in MemoryGovernor. Both images were
forced nonincremental builds after source replacement; auxiliaries are identical.
Neither frozen source is a final platform-qualified release image.

The candidate rounds the existing host reservation to4KiB and caches admitted
capacity. Logical preflight/commit/refund remains; small allocations avoid host
resize calls. It adds a cached/unknown-capacity invariant, rounded reentry checks,
admitted headroom and earlier tiny-budget rejection. Scope creation stays empty;
no new budget, owner, collector, pool, option, execution counter or unsafe code.

B554: six jobs219.812s on F4as_v7 Ubuntu24.04.4/.NET11RC3, normal GC/tiering,
four independent source/control processes,5s warmup/role and7rotated400msrounds.
All identity/output/file/ownership/quiet/join/RAM/archive/localSHA gates pass.
[Raw observations and both cores](quiet/observations.tgz),
[receipt](quiet/archive-receipt.json), [medians and controls](quiet/summary.json),
[source/tooling/original failures](prototype-evidence.tar.xz).

| Complete script | Candidate elapsed change | Wider process CPU change |
| --- | ---: | ---: |
| Report, sync | −4.39..−2.16% | −5.30..−2.96% |
| JSON-lines helpers, sync | −4.41..−1.52% | −5.17..−4.11% |
| CSV validation, sync | −2.40..−0.25% | −2.44..0% |
| Text helpers, sync | −2.26..−1.80% | −4.47..+0.72% |
| Tiny config, sync | −1.91..−0.66% | −1.01..+0.61% |
| Text helpers, async API | −4.26..+0.53% | −5.38..+1.51% |

Ranges compare both candidates with both references, not confidence intervals.
Allocation is flat. Report/JSON have supporting CPU evidence; text/config CPU and
async text remain uncertain (identical async candidates spread4.00%). The modest
synchronous gains are preserved. Desktop CSV+7.57..10.10% and variable other
controls remain under desktop/; they are diagnostic and were not reproduced here.
Clocks include mediated RAM I/O, capture/projection and prior-result cleanup;
exclude compilation, fixture reset, verification and protocol. External CPU is
wider and coarsely ticked. These are source-pair results, not new CPython rows.

Focused tests pass18governor/reentry and241public workload/lifetime cases on actual
.NET11/10; native enumeration tests also pass. The affected native storage gate
passes1263/1491, leaving228 failures across80classes. Partial source classification
finds exact-charge, small-threshold and per-allocation-callback assumptions; this
does not prove all failures are policy-only. Independent refusal-before-allocation,
side-effect, reentry, alias-funding and cleanup assertions must not be discarded.

Decision: withdraw the global grant policy. It removes host crossings but retains
the existing logical per-storage machinery while adding cached capacity, a second
estimate level, new failure boundaries and a broad contract migration. The modest
measured benefit does not justify that complexity here. This is a design/debt
decision, not a fixed percentage veto or a claim that the favorable measurements
failed. Contended-host benefits are unmeasured. A simpler owner-level design or
evidence of dominant host-budget cost could justify a future bounded reassessment.
No alternative runtime mode or global quantum remains in the product.

The investigation confirmed an independent correctness gap: wide enumerate index
increments allocated before admission. Refused65537B magnitudes allocated66624Bsync
and67672Basync in the warmed native observation. Reusing the existing admitted
integer-addition path for wide increments lowers failure bookkeeping to1552/2176B.
Ordinary counters keep direct increments; late duplicate old-index charging is
removed. Tuples fund old indices, while the retained next magnitude is admitted
before construction. Source pull/exhaustion order remains. Retained-alias and
full-budget exhaustion checks pass, with independent CPython3.13.2 boundary values
and actual suspended source I/O. This correction and useful tests are retained;
these allocation numbers describe refused operations, not a whole-script speedup.

The VM run is terminal-cached with no users/RAM. Staging retired only four exact
Git-archived raw catalog/confirmation copies from B551/B552 under idle-lease,
path/UID/link/SHA checks. Qualified039 and priorE294 survive. Publication stays held;
remaining runtime performance and independent regex work continue.

Cleanup complete: [local retirement](local-retirement.json) restores/verifies all568
production files before deleting the isolated worktree. [VM retirement](vm-retirement.json)
verifies both fedd hardlink aliases and the exact committed archive copy before
removal. Corrected ed5 baseline, qualified039 and priorE294 survive. Private frozen
diagnostics remain outside project inputs; no grant mode or quantum fixture edits
remain in the product. The [retained source checks](retained-validation.json) pass
1491 native storage and241 public workload/lifetime cases on each actual runtime;
full platform delivery is tracked separately.

Retained correction f6798fd7 subsequently passes the complete delivery boundary:
Windows/Ubuntu, both actual target frameworks, original Stress and installed Release
consumers. [Cached terminal CI proof](retained-delivery-ci.json); no repeated gate.
