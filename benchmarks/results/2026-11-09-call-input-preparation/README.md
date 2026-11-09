# Ordinary call input preparation — R175

2026-11-09. Qualified cac4acf8-equivalent R173/e833 freeze versus two private
source variants after f9a84542. **Both variants withdrawn completely.** All six
production files match the qualified baseline; retained semantic coverage passes.
No public API, regex provider, memory policy, stack or cancellation change ships.

## Work and implementation

The existing ForwardValues pass forwards pure adjacent local/constant operands to
arithmetic. Extend the same pass to complete ordinary call argument suffixes;
retain source read spans and original funded packet capacity. Materialize the
packet in the call instruction with per-argument cancellation, then use existing
binding/callback/async paths. Existing LiveExtent identifies pending packets after
context promotion; no invocation flag, new array, cache, policy or engine.

Initial metadata added a source int beside the native proof input. Refine to reuse
the existing prepared-input int, with one compiler-only call-site fact and ordinary
forwarding restricted to code without NativeBody. Remove the duplicate argument
field and preserve closed helper borrowing/proof domains. Wider cold read-span
keys support arguments after the fourth. Each variant removes module instructions:
CSV93→79, text74→65, JSON report58→50, Unicode-index64→56, config67→60.
This static census establishes fewer dispatches, not a removable CPU percentage.

Initial checks:537internal/2831public each actual.NET11/.NET10; full16931public.NET11.
Refined source:576internal/3886public each target. Independent trusted CPython3.13.2
goldens verify target/keyword order, positional aliases and successful constructor/
function output; guest top-level return is explicitly wrapped for that reference.
New public cases observe original unbound spans beyond argument four, keyword
identity, genuine delayed host calls/constructors, cancellation, retained results,
joined cleanup and budget reuse. Nothing relaxes assertions or containment.

## Measurements

Same five complete trusted scripts, same worker/adapter0.4 and auxiliaries; both
APIs and two identical original desktop roles. Runtime allocation effectively flat;
compilation allocation initial+0.41..0.86%, refined+0.29..0.66%. Compilation timing
includes construction/disposal and driver overhead, no execution or I/O. Original
summary divided by zero execution time in compile-only data; all lanes had already
completed. Offline analysis fixes the clock selection without rerunning anything.
Desktop elapsed remains diagnostic, with all original/refined adversity retained.

Two separate quiet F4as_v7/Ubuntu24.04.4/.NET11RC3 batches, ordinary GC/tiering:
B519141.267s and revised-source B520141.365s, five jobs each. Four independent
processes (two per source),5s warmup/role, seven rotated150ms rounds. Warm includes
mediated RAM I/O, full stdout/file validation and prior-result cleanup; excludes
startup/reset/protocol/final caches. External process CPU is coarser/wider. Ranges
span all four median pairings, not confidence intervals or CPython comparisons.

| Complete script/API | Original elapsed change | Refined elapsed change |
| --- | ---: | ---: |
| CSV/sync |−1.66..−0.09%|−2.03..+2.17%|
| Text/sync |−2.66..+0.22%|−4.81..+1.09%|
| Text/async completed I/O |+2.71..+5.14%|−1.38..+1.55%|
| JSON report/sync |−3.10..+0.11%|−3.25..+2.10%|
| Unicode-index control/sync |−3.49..+3.61%|−0.79..+5.30%|

Original async text has tight0.40% reference spread,1.92% candidate spread, a
demonstrated adverse result. Revised source overlaps controls; it does not establish
recovery, equivalence or general CPU gain. Revised text reference spread4.19%,
report4.38%; Unicode external CPU0..+7.86% and elapsed retain possible cost.
All identities/goldens/ownership/quiet/Closed/joins/RAM-unmount/archive-under-lease/
localSHA gates pass. Both owners terminal; no workers/fixtures remain. Revised
console labels retain B519 in a reused print string; exact B520 unit/identity/raw
records identify the revised collection, not the original run.

## Decision and preserved failed conditions

Withdraw the dispatch-only implementation. It keeps argument reads/copies in a
new preparation loop and grows compiler allocation; these controlled results do
not justify that additional machinery. This is a bounded engineering decision,
not a fixed-percentage veto, failed semantics or permanent rejection of direct
operands. A later coherent call/frame migration must remove actual packet/workspace
work while preserving native helper allocation gains and suspension ownership.

Restore all production bytes exactly using fresh timestamps, force nonincremental
builds, then42retained public contract checks pass per actual11/10. Runtime source
remains covered by previous full42684724223; no unchanged full platform/Stress/
package replay for discarded prototypes. The broader performance objective stays
active; the primary external benchmark guide is unchanged.

First B519 space proof refused before mutation: a path substitution incorrectly
renamed its first candidate, and B505 was genuinely absent. Both failed command/log
survive. Exclude known-absent B505, correct substitution ordering; fresh exact SHA,
single-alias/metadata/parent/receipt proofs remove only B518/B486/B485 compressed
duplicates. B520 separately removes only B480/B483/B479/B517 copies. Canonical
tracked archives, raw trees, receipts and frozen workers survive;240MiBOS/12MiB
evidence floors unchanged. Reuse the exact persistent R173 baseline by hardlink;
only one new core is staged per run. No broader storage audit or old-terminal poll.

[Initial quiet](original-quiet.json), [revised quiet](refined-quiet.json),
[initial desktop](original-desktop.json), [revised desktop](refined-desktop.json),
[qualification](qualification.json), [independent goldens](python-goldens.json),
[initial identity](original-identity.json), [revised identity](refined-identity.json),
[initial raw/cores](original-observations.tgz), [revised raw/cores](refined-observations.tgz),
[exact source/tests/desktop/failed conditions](source-checks.tar.xz).
