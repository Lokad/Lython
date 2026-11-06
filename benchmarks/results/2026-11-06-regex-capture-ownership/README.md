# Capture ownership — U04

Retain the simplification. `TryMatch`/`TryNext` fill one caller-owned admitted
buffer. Delete `LythonRegexMatch`, its per-success arrays, per-match core delegates
and eager `ReCapture`/group materialization. Retained matches snapshot immutable
byte/scalar ranges over the original funded subject. Partial groups are admitted
on access; full-subject groups reuse the original. Expansion consumes ranges.
Add `regs`. No new pool, collector, memory layer, policy or core dependency.

## Complete script

2026-11-06, quiet Azure F4as_v7 / Ubuntu 24.04.4 / .NET 11 RC3, CPython 3.13.16.
Unchanged b94fa98c worker/protocol, normal PythonRe 0.4 producer, normal GC/tiering.
Fully qualified b6650f09 reference 54b53c7f; ffa17260 plus U04 candidate edf02bae.
Role-specific adapters implement the respective contracts; all hashes and sources
are archived. No producer change explains the comparison.

Read a 23,014 B UTF-8 file / 1,024 records, match named/empty captures, access
selected groups, aggregate numbers/scalar endpoints and check complete output.
Actual mediated RAM-file reads and prior-result cleanup are timed. Warm excludes
compilation; fresh includes compilation/disposal. Staging/reset, startup and final
teardown are excluded. Two identical current roles, five-second warmups, seven
rotated 150 ms batches. Output/funding/quiet/Closed/join/fixture cleanup pass.
B357 finishes in 87.09 s, exit 0; archive verified under its lease and after
retrieval. Terminal; never query it again.

| B357 workload | Reference | Current, two roles | Change | CPython |
| --- | ---: | ---: | ---: | ---: |
| Regex extraction, warm | 2.997 ms | 2.842 / 2.889 ms | −5.17% / −3.62% | 0.409 ms |
| Regex extraction, fresh | 3.189 ms | 3.043 / 2.985 ms | −4.58% / −6.40% | 0.447 ms |
| Unchanged text/helpers, warm | 2.193 ms | 2.195 / 2.146 ms | +0.10% / −2.13% | 1.150 ms |

Current-role spreads: 1.64% / 1.95% for regex, 2.28% for the control. These limit
precision. Coarse process-CPU ticks remain in assessment.json; no CPU, RSS,
live-heap, embedding-tail or pending-I/O claim. Optional-adapter regex remains
6.96–7.07× CPython warm and 6.68–6.81× fresh. This does not refresh the five
general workloads or qualify backend safety. The performance objective stays open.

## Separate allocation diagnostic

Same normal producer, full 64-match byte/capture goldens and zero final funding.
Cursor creation/advances/conversion/disposal include the candidate caller buffer
and admission once per drain. Baseline/current/current/baseline processes measure
32,088 → 25,560 B: **−20.34%**. Prepared subject/compilation are outside clocks;
retained guest matches are not exercised. Busy Windows elapsed −3.87% is diagnostic.
Do not present this as a whole-script allocation percentage or combine it with
historical native-return measurements.

## Correctness and original conditions

Normal focused checks: 476 public / 84 internal per actual runtime, plus the new
group-copy cancellation check. Full ordinary Release: 4,262 internal / 16,195
public per actual .NET 11 / .NET 10. The independent CPython golden covers retained
matches after cursor disposal/purge, aliases/defaults, scalar/lookbehind windows,
unmatched/empty groups, lastindex/lastgroup, regs and expansion through both APIs.
Competing shared funding refuses the actual group copy, preserves its first origin
through catch/finally-return, drains cleanup and permits reuse. Existing real
in-flight interruption and retained-cache controls pass.

The private immutable dev44 bridge also migrates while keeping native high-water
working admission: 84 internal / 482 public checks per runtime, including 16,384
matches under 256 KiB, independent owners and actual interruption. Normal 0.4 pin
stays unchanged. Final released-producer and managed-safety qualification remain
separate; development source/props/locks/checks are archived.

Preserve all original failures and corrections:

- B355 omitted fixture size metadata; rejected before Ready, with no observations.
- B356 corrected metadata and completed all three jobs in 86.89 s. An early SSH
  status observation limits its timings to diagnostic. Its archive list named an
  absent setup.py; owner exit 1 happened after cleanup/quiet gates. Recover persistent
  evidence under a new lease without retiming, preserving journal/script/partial
  archive. B357 confirms once without observer interference and fixes the archive
  list; no SSH/status/profiler query occurs before its owner bound.
- Setup disk gates stop before work. Retire only exact expanded copies after
  verifying local/remote archives and backups; inspect dependencies before retiring
  unused prototype images. Preserve active images, all archives, 6 MiB setup
  headroom and every timeout/check. Workers/catalogs/raw files stay persistent.
- The first local full run used an unpinned child launcher: 33 subprocess failures,
  16,162 public passes. Explicit SDK environment correction passes without source,
  timeout or containment changes. Original logs remain.

Detailed receipts, scripts, failed conditions and snapshots are in
source-and-check-history.tgz. Platform/original Stress/installed-package delivery
is tracked separately; publication remains postponed.
