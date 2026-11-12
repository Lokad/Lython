# Shared receiver operations — first form, 2026-11-12

Frozen eight-file prototype over fully qualified `2ec9d67f` production (HEAD
`d4bdd605` at freeze). The source/core identities, original failed fixtures,
complete diagnostic observations and B601 raw results are preserved here. This
form is superseded by a one-switch shape-proof refinement, not fully delivered.

One canonical operation selector, shape fact and invocation serves prepared native
helpers and ordinary executable prefixes. The ordinary `not text` guard shares
conditional truth eligibility. Guarded helpers can avoid unnecessary invocation
contexts; dynamic protocols/keywords still use ordinary binding and suspension.
The tests uncovered a separate correctness fix: async string replacement must await
its count's guest `__index__` and validate old/new strings before that callback.

Focused Release: 1,650 internal / 631 public tests per actual .NET 11/10 (4,562).
Seven complete scripts, both APIs and separate compilation preserve all full-output,
file, allowance and cleanup goldens. CPython golden corrections preserve the
original erroneous huge-count fixture as well as the successful corrected output.
A first protocol test timed out because replacement's async path was missing;
the corrected test passes without changing its timeout.

Guarded-search physical allocation falls 71.28% Run / 71.19% RunAsync. Other and
compile allocation is flat. Desktop elapsed is diagnostic, including adverse
report Run +8.30..+15.23%, CSV async +6.06..+9.57%, guarded compile +9.83..+11.56%
and captured Run +1.63..+1.91%, with their unchanged controls preserved.

B601 completes nine targeted jobs in 251.531s on the existing quiet F4as_v7 Ubuntu
VM / .NET11 RC3 / fixed worker and adapter 0.4. Four process-median pairings:

| Script / API | Elapsed change | Wider process CPU change |
| --- | ---: | ---: |
| Guarded helper search / Run | -23.51..-19.46% | -20.21..-16.71% |
| Guarded helper search / RunAsync | -27.38..-24.00% | -26.33..-16.33% |
| Existing native helper search / Run | +1.99..+8.37% | +4.51..+8.47% |
| Existing native helper search / RunAsync | -10.89..-3.35% | -6.25..+0.70% |
| Captured text / Run | -2.65..+2.51% | -3.03..+4.12% |
| Captured text / RunAsync | +3.19..+4.05% | +3.23..+10.82% |
| Report / Run | +1.50..+3.64% | +1.96..+6.25% |
| CSV / RunAsync | -0.47..+1.45% | 0..+3.33% |
| Guarded helper compilation | -2.12..-0.97% | -1.44..-0.20% |

Negative is less time. These ranges are not confidence intervals. Warm clocks
include mediated RAM-file I/O, full validation and prior-result cleanup; exclude
startup/compilation, staging/reset and final cache disposal. Wider CPU includes
protocol/reset. Desktop includes fixture reset and immediate disposal. No new
CPython comparison, universal speedup or embedding-latency claim is made.

All identity, goldens, balances, quiet-before/after, joined users and RAM cleanup
gates pass. Persistent raw archive SHA is independently verified. The initial
space inventory refused before mutation; its original stderr and the exact
committed duplicate/inode retirement proof remain. No headroom was relaxed.

Decision: inspect the redundant double operation dispatch in the shared shape
check and consolidate it once before delivery. Retain original regressions and
source; no inference that allocation reduction proves CPU benefit, or that an
unresolved measurement vetoes the architecture permanently. Publication deferred.
