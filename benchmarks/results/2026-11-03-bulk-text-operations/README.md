# Bulk ASCII text work — B151

2026-11-03. **Retain bulk lower/upper only; withdraw both whitespace scanners.**
The final production change replaces the existing per-byte ASCII lower/upper
mapping with managed BCL span operations in the same admitted output window.
Unicode/context/expansion and title/capitalize/swap rules retain their existing
pipeline. No new cache, metadata, scanner state, backend, options or owners remain.
The larger scanner prototypes are evidence, not dormant runtime implementations.

Baseline39061f172463ccc7a9db63879760d334020f7da4 (CSV milestone, fully qualified
by41593775667). Frozen baseline core66120849c6cc2ecd6e3605630501d177c179f2edb43ee9c0ce95a2e3d8883bcd.
Final candidate corec24af844ac1e266f149e57fd903253ae2a61caf9955a01c793365142b4d4bbde;
exact source hashes and all three prototype patches are in each variant directory.
Source-pair worker/adapter bits are identical; no auxiliary normalization was needed.
Committed rebuilds may have different metadata; these hashes identify measured images.

| Complete workload / phase | Initial elapsed / control Δ | Refined scanner elapsed / control Δ | Casing-only elapsed / control Δ |
| --- | ---: | ---: | ---: |
| Canonical text warm |−0.87 /−1.76%|−3.79 /−2.16%|−3.01 /−0.13%|
| Canonical text compile-run |+3.33 /−0.92%|+2.26 /+4.80%|−4.88 /−2.90%|
| Short ASCII text warm |+3.80 /−1.84%|−4.42 /−6.66%|+4.17 /+4.40%|
| Unicode text warm |−5.56 /+1.20%|+9.10 /−0.12%|−2.83 /−2.77%|
| Long ASCII text warm |−35.66 /+1.62%|−41.00 /+4.45%|−11.26 /−2.56%|
| Unchanged CSV warm |+0.97 /−1.27%|+1.81 /+1.75%|−0.01 /−3.28%|
| Unchanged JSON warm |+2.81 /+0.12%|+0.52 /−2.21%|−4.29 /−4.56%|
| Unchanged report warm |−1.05 /−1.34%|+0.88 /−0.17%|−2.81 /−0.38%|

All deltas compare their own baseline; negative means lower. Independent controls
are the same baseline source, not an adjusted clock. Keep drift visible rather
than subtracting it into precise claims. Final canonical warm external process CPU
−2.50%, control−0.28%; long CPU−19.58%, control−14.55% is less precise. Fresh CPU
control+7.75% limits attribution. No causal report/JSON gain is claimed. Unchanged
CSV's final control is3.3% faster than baseline/candidate: an adverse observation,
not evidence that the unchanged CSV parser improved. Short ASCII/Unicode final
changes follow controls closely. These are modest diagnostic gains, not a solution
to the representative CPython gap or a sustained portable multiplier.

Initial dense ASCII/non-ASCII membership metadata harmed ordinary short text.
Refinement removed that metadata and reused known-ASCII windows, but Unicode
became9.10% slower with a stable control. Both scanners were removed rather than
retaining another speculative strategy. One final source-isolation comparison
keeps only the simpler managed casing substitution. Architectural simplification,
measured elapsed and allocation outcomes are distinct.

Allocation is effectively unchanged: final differences under0.01%; controls under
0.05%. [Accounted execution/root peaks](accounted-peaks.json) are exactly identical
across final images for all four text cases. This is not a physical-RSS assertion.
All outputs, file goldens, hash witnesses, balances, image isolation and fixture
cleanup PASS. The allocation tool is the unchanged B150 helper: its raw purpose
label still says CSV; actual text IDs, catalogue, hashes and samples define this
measurement. It records process allocations including fixture reset and one
untimed initial invocation's admitted peaks. Its elapsed/CPU are diagnostic only,
not the timing verdict.

Quiet Azure Standard_F4as_v7/Ubuntu24.04.4/.NET11RC3, ordinary GC/tiering. Canonical
text is3,072 lines/35,840B; added ASCII and Unicode cases1,024 lines, long case8
lines/131,088B. The exact catalogue/goldens are compressed here. Each variant uses
existing protocol2 complete warm/fresh workers, four-second warmups, nine rotated
~200ms batches, baseline/candidate/unchanged control. Timed execution excludes
fixture reset, startup, transport and final owner/process/fixture teardown; includes
actual guest I/O, full checks and prior-result cleanup. Fresh also compiles/disposes
each invocation. External Linux process CPU includes runtime/JIT/response and reset,
not guest CPU alone. Local isolated CPython3.13.2 and VM3.13.16 verify added goldens.

Initial and refined services finish238.835s/223.750s; final219.064s. All are terminal
exit0, before/after quiet gates pass, workers joined and lease free. One refined
launcher had CRLF and exited2 in1ms before any job. Its log is preserved; line-ending
correction did not discard or repeat measured work. The refined initial local
CS0136 compile failure is retained with the corrected build log.

Final source was nonincrementally rebuilt after scanner restoration:0 warnings.
Focused Release263 internal and1,493 public cases per actual.NET11/.NET10 PASS:
Unicode inventories/context, casing boundaries, split/rsplit/count/remainders,
cooperative cancellation, terminal admission refusal, aliases and zero balances.
Broader public ASCII-control and chunk-edge regressions remain independently useful.
Full Windows/Ubuntu, original Stress and installed consumers batch at delivery;
qualification status will be recorded separately. Publication remains postponed.
