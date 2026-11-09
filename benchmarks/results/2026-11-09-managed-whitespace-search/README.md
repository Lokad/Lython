# Managed ASCII split-boundary search

R157/B508,2026-11-09. Baseline3fb8025d (production78fd7bb4 fully qualified42615908221),
fresh nonincremental coree731a415, versus final candidatecfeb99b0. Exact source,
binary and common worker/adapter hashes are retained. This is a source-pair
comparison, not a CPython comparison or a claim about later binaries.

Replace the shared per-byte ASCII category-run loop with managed `SearchValues`
searches in both directions. Two fixed immutable metadata inventories derive from
the existing classifier. Unicode decoding, casing context, Python whitespace,
maxsplit/rsplit,64-byte cancellation windows and admitted final fragments remain.
No custom SIMD, unsafe code, new runtime dependency, public option, pool or policy.

Retain this bounded managed implementation **without a demonstrated CPU gain**.
Ordinary and compilation allocation are unchanged. Focused Release checks pass
178internal/219public per actual .NET11/10. Six existing public boundary scripts
independently pass CPython3.13.2, covering all128ASCII bytes, Unicode/context,
forward/reverse limits and63/64/65/4095/4096/4097 boundaries. Existing cancellation,
refusal, retained-alias and cleanup coverage is reused. No duplicate tests added.

One86.826s quiet collection on the established F4as_v7/Ubuntu24.04.4/.NET11RC3 VM:
three jobs, four independent processes,5s warmup each, seven rotated150ms rounds,
normal GC/tiering. Ranges below compare each candidate role with both baselines.

| Whole script/API | Elapsed change | Coarse process CPU change |
| --- | --- | --- |
| Text sync |−4.99% to+0.86%|−1.92% to+2.48%|
| Text async |−3.89% to+0.95%|−2.13% to+2.17%|
| Unaffected CSV sync |−0.33% to+1.77%|0% observed|

Text baseline/candidate process spreads reach3.96%/2.58%; coarse CPU is mixed.
Mostly lower elapsed does not establish a CPU improvement or equivalence. Includes
mediated RAM-file I/O, full output validation and prior-result cleanup; excludes
startup, compilation, fixture reset/protocol and final cache teardown. Desktop
compilation/allocation are separate; static metadata startup was not timed.

The original desktop image15e49 is a different binary: async text+5.9–10.1%,
unchanged search controls25.5% apart. Preserve this unexplained adversity. The final
refinement derives both inventories from one classifier; do not relabel original
timings as this source. Larger32-job local batches did not settle elapsed uncertainty.

The reused archiver validated actual three-job records but wrote stale literal
`jobs=5`; setup also retained the old `allFiveJobsValidated` name. Initial retrieval
refused that discrepancy. Original code/receipts/failure survive. Verify archived
exact schedule, four identities and28samples per job directly, retaining a separate
[receipt qualification](receipt-qualification.json). No capture restarted or raw
receipt rewritten. The offline assessor's old count was corrected before use.

Identity/quiet/golden/ownership/Closed/join/RAM/archive/localSHA gates pass. Finite
150s owner,30s stop,30s margin. Fresh exact tracked-copy/SHA/single-alias/owner/group/
mode/parents/pre-mutation proof recovered only five redundant remote compressed
archives. Raw trees, receipts, frozen workers and canonical Git copies survive;
original240MiB OS floor and12MiB trace/archive headroom unchanged. No users remain.

Full Windows/Ubuntu, original Stress and installed-package qualification for this
change is deferred to the next coherent delivery checkpoint. Broader whole-script
performance work and publication deferral remain active.

[Quiet summary](summary.json), [original local results](local-summary.json),
[measured sources](final-source.json), [focused checks/decision](delivery-source.json),
[raw workers/observations](observations.tgz), [original conditions and preparation](preparation.tar.xz),
[space proof](space-recovery.json), [terminal status](terminal.json).
