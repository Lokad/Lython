# B113: composed native helper returns

2026-11-02. Baseline `8f58934c` (runtime `bc0d9823`, fully qualified41460677410)
versus the recorded uncommitted source pair. Final source snapshots and exact
core/auxiliary hashes are retained here. These are Lython-to-Lython diagnostics,
not new CPython ratios. Publication remains postponed.

| Complete script | Warm allocation | Fresh CPU versus baseline | Important limitation |
| --- | ---: | ---: | --- |
| Text aggregation, 3,072 lines | –24.63% | –7.8 to –10.1% | Warm second unchanged control also improved substantially; no precise warm CPU multiplier. |
| Native-helper report, 3,072 lines | –35.7% | –9.4 to –12.0% | Independent CPython3.13.2 full stdout/file goldens; new diagnostic workload, not a published external comparison. |
| File search, 1,025 lines through early exit | –47.5% | Inconclusive | CPU controls unstable, including +204%; elapsed improves roughly3–5% in final blocks, controls also move. |
| CSV helper aggregation | Unchanged | +3.7 to +4.3% | Warm CPU about4–7% worse against controls. Explicit regression debt. |
| JSON helper aggregation | Unchanged | +2.6 to +3.0% | Warm CPU about2–4% worse against controls. Explicit regression debt. |

Fresh includes compilation and its disposal. All comparisons include actual
mediated file I/O, fixture reset, full output checks, result disposal and balance
checks. Warm excludes compilation/startup. CPU is process CPU, including JIT and
runtime work; it is not isolated guest CPU. No claim assigns the CPython gap to
these helpers or treats allocation reductions as CPU improvements.

## Change and retention decision

Replace the B99 enum/one-operation classifier and matcher with immutable links
into existing executable instructions. Whole-expression type/callable guards
finish before native work. Native string/numeric/conversion/read expressions
compose without child interpreter, operand/cache arrays, argument repackaging
or per-invocation workspace. Real function context/binding and the ordinary
engine for unsupported/deeper/protocol/protected/captured bodies remain. Private
expression-depth eligibility16 bounds CLR recursion; it is not a language limit.

Retain this migration for meaningful complete text/report allocation and CPU
benefits. The general shape/guard work costs the previously native CSV/JSON
helpers. This debt is explicitly accepted at this boundary, not described as a
universal speedup. Next call-transport milestone must address that cost together
with complete activation/operand work or decide reversal; no series of new
four-instruction exceptions. The substantial performance objective remains open.

## Controls, refinement and uncertainty

Reuse the existing complete-job driver on the quiet F4asv7 VM, .NET11 RC3,
ordinary GC/tiering, three isolated images sharing CoreLib. Exact same auxiliary
DLLs in all images; only the core differs. Two forward/reverse blocks:192 warmups
and9×32 complete warm jobs;96 warmups and9×24 fresh jobs per image/case. Full
hash witnesses, stdout/file goldens, compiled/execution balances and owned
fixture removal pass. Original service268.071s, refined221.042s; both exit0,
quiet before/after, terminal service inactive and shared lease released.

The original source reduced allocation but CSV CPU regressed about8%; all adverse
data and original source remain in `initial/`. One source-backed refinement
removes unnecessary callable shape classification and repeated block lookup.
It improves the design without restoring a separate B99 matcher. One transient
compile error from a duplicate local declaration is preserved; no runtime/test
contract was changed to correct it.

Original fixed96 compile warmups were only around10ms. Unchanged controls drifted
up to71%, and JSON/report candidate clocks were3–5× baseline. Preserve those
results as unresolved unconditioned observations, not a failed design or a
disproved historical result. A bounded measurement correction uses the established
persistent-worker protocol:4s timed warmup per independent process,9 interleaved
~100ms batches, original hash/golden/cleanup checks. Final compile microseconds:

| Script | Baseline | Candidate | Unchanged control |
| --- | ---: | ---: | ---: |
| CSV | 88.69 | 87.61 | 88.75 |
| JSON | 79.20 | 79.00 | 77.96 |
| Helper report | 75.03 | 75.86 | 75.11 |

Final conditioned compilation is effectively flat; frontend source is unchanged
by the runtime guard refinement. Correction service49.304s exits0, quiet gates
pass and all persistent workers/fixtures are joined/removed. The original adverse
conditions are retained, not silently replaced.

## Correctness and delivery

Initial focused actual.NET11/.NET10:134 native cases after a fixture correction;
634 public cases after the deep-chain fixture correction. Refined:134 native and
23 new/affected public cases per actual runtime, plus634 public.NET11. All19
public case goldens independently pass isolated CPython3.13.2. Tests cover
defaults/rebinding/error/evaluation order, unknown mapping results, real protocol
suspension/cancellation, source/active-exception attribution, refusal after earlier
native text work, retained source ownership and cleanup/budget reuse.

Two new fixtures initially failed:40 calls exceeded the existing64-operand
frontend bound;20 still exceeds optimizer eligibility and remains supported.
The refusal fixture initially expected source-owned fresh slices refunded with
the execution; conservative source ownership correctly lasts longer. Original
existing assertions remain intact; the corrected fixture proves funding until
the input owner ends and then zero accounting. Failed logs are retained.

Full platform/Stress/installed-package delivery qualification is pending at this
source snapshot; consult the subsequently recorded delivery status. No new
backend dependency, quota, owner infrastructure, unsafe code or publication.
