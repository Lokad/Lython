# Native JSON token construction — B122/B123

2026-11-02; baseline217ebb65/runtimec3fa26cc, retained source patch/snapshots and
exact loaded hashes below. Publication remains postponed. This is a Lython source
pair, not a new CPython ratio or universal parser redesign.

The existing token scan already establishes closure, escaping and controls. Plain,
closed, valid UTF-8 with no forbidden control now constructs its independent
admitted PyString directly. It skips a redundant backend parse and a4x temporary
reservation that funded no actual decoding scratch. Lenient plain controls need
no rewrite. Genuine escapes/invalid inputs keep the existing decoder, working
storage and original-coordinate diagnostics. Selection happens before admission;
there is no retry/fallback after refusal. No new parser stack, view, owner, cache,
pool, allowance, configuration or per-object ledger.

## Complete-script results

Quiet F4as_v7 Ubuntu24.04/.NET11RC3, normal tiering/workstation GC. Existing checked
workers/protocol, independent baseline/candidate/unchanged-control processes,
four-second warmup per image, nine interleaved approximately200ms batches. All
stdout/file goldens, identities, quiet gates and joined fixture/worker cleanup pass.

| Final source / warm lane | Candidate elapsed, blocks0/1 | Candidate process CPU | Unchanged elapsed / CPU controls |
| --- | ---: | ---: | --- |
| Complete JSON-to-Markdown report |−10.06% /−8.38%|−11.60% /−10.63%|−0.80%/−0.26%; +2.58%/+0.18%|
| Complete JSON-lines/helpers |−9.08% /−6.10%|−6.97% /−10.84%|−0.89%/+1.15%; +0.21%/−3.70%|

Report elapsed is about9–11% lower relative to its controls in both blocks, CPU
about11% lower. JSON-lines elapsed about6–8% lower, CPU about7–8%. Retain the
native construction path and the correctness refinement. This does not close
the substantial performance objective or earlier CSV/Unicode regression debt.

Initial source, before the cancellation correction, measured report elapsed
−9.23%/−6.84% and CPU−13.08%/−9.64%; controls−1.21%/+6.69%, −1.29%/−2.81%.
JSON elapsed−6.15%/−7.51%, CPU−7.92%/−11.68%; controls+1.98%/−1.88%,
+0.63%/−1.92%. These observations remain complete, not rewritten as final source.
Do not attribute the difference between independent initial/refined clocks to
the crossing-check change.

The initial unchanged CSV workload tracks controls: elapsed−0.60%/+1.54%, control
+0.43%/+1.97%; CPU+0.19%/+0.86%, control+1.31%/+2.49%. Text is inconclusive:
elapsed+1.42%/+0.02%, controls−3.31%/+2.20%; CPU−2.01%/−3.32%, controls−3.87%/0.
Keep the adverse first text point. No general unaffected-script speedup claim;
unchanged CSV/text jobs were not repeated for the focused correctness refinement.

CPU is external Linux TGID user+system through response, weighted over nine
batches; runtime/JIT/response included, not guest-only CPU. Warm timing includes
staging/reset, actual mediated file I/O, full output/file checks and prior-result
cleanup. Last result/final compiled/process/fixture cleanup is outside batches
and joined. Compilation/startup excluded. Existing named CPython points remain
separate; no inferred external multiplier from these source-pair medians.

## Allocation, cancellation and ownership

Existing complete-job allocation diagnostic,192 warmups/image and9x32 complete
jobs, isolated Lython images sharing CoreLib and a process-wide precise allocation
counter covering worker threads. Full goldens, hash witnesses, owner balances and
final cleanup pass. Its clocks are diagnostic; the timing decision above uses
independent processes.

| Final source / complete workload | Baseline allocation/run | Candidate | Change | Unchanged control |
| --- | ---: | ---: | ---: | ---: |
| Report |10,246,462B|9,754,684B|−4.80%|−0.0007%|
| JSON-lines/helpers |6,573,541B|6,181,309B|−5.97%|+0.0160%|

Initial source separately measured−4.80%/−5.97%; raw counters remain in initial/.
All final copy capacity/metadata is admitted before allocation. Input buffers are
not borrowed by decoded strings, and aliases/results retain funding. Lower budget
thresholds for plain strings reflect removal of unused scratch, not uncharged
storage or cumulative allocation accounting. Actual escaped scratch/replacement
overlap, callbacks, terminal refusal and cleanup remain funded.

Review found a real pre-existing cancellation gap: two-byte escapes can skip all
modulo-equality checkpoints. Token scanning, lenient rewriting and error-position
translation now check crossed input windows, bounded to1024 bytes plus at most
one skipped byte. No timer, instruction fuel or checkpoint-count observer.
Individual backend decoding/copy calls remain atomic; no hard latency guarantee.

The native-scanner regression uses the real helper independently of its caller's
entry check. After admitting input it fills remaining budget, cancels, and asserts
structured cancellation with no decoding admission failure. The old source fails
with SharedMemoryAllowance instead of Cancelled; corrected source passes. The first
test draft wrongly expected CLR OperationCanceledException; actual runtime
cancellation is LythonRuntimeException/Cancelled. Both that mistake and the
corrected before-source failure are preserved. No public exception contract changed.

## Validation and delivery

Final focused Release170 internal/736 public tests pass on both actual.NET11/10:
plain/escaped/lenient UTF-8, malformed input/offsets, raw_decode prefixes, duplicates,
callbacks/suspension, managed depth, cancellation, refusal, independent input
copies, retained results and owner cleanup/reuse. One new cancellation regression;
no redundant syntax-count or exact-checkpoint tests.

Two old fixtures assumed scratch on every token. The output-admission fixture now
leaves only final-copy capacity for plain input and real scratch plus copy for
escaped input; original denial/origin/unwind assertions remain. The lenient-growth
fixture adds an actual escape to its5000 raw controls, preserving both100000/
30000 success/failure budgets and testing real rewrite storage. Expected decoded
length becomes5001. Old failures remain; no timeout/containment-bound relaxation.

Initial timing153.253s, allocation33.526s; final affected-workload timing76.635s,
allocation33.239s. Every owned service exit0/inactive and lease free. No broad
profiling or ten-minute lane. [Source gate](source-gate.md),
[refinement boundary](refinement-plan.md), initial/refined assessments, compressed
raw observations/catalogs/source snapshots, complete-job driver, focused and
before-source failure logs preserve all conditions. Decompress into the named
retrieved folders to rerun the unchanged analyzers.

This source is focused-qualified; full platform/stress/installed-consumer checks
will be batched at the next coherent delivery. The separately corrected formatting
delivery41504778956 is cached FULLY GREEN foraa6cf3cb/runtimec3fa26cc, outside this change.
Original41501504578 is cached failure (Ubuntu full PASS, Windows preflight test
window failure), with its independent correction/evidence retained separately.

Subsequent delivery: FULLY QUALIFIED by cached terminal41519435155 at90785737.
Windows/Ubuntu ordinary Release.NET11/10, original Stress both and installed
consumers PASS. [Delivery receipt](../2026-11-02-native-call-contexts/delivery-ci-status.json).
This covers the retained source here; later prototypes require their own checks.
