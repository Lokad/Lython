# D17 / B228 completed prepared-target operations, before code

2026-11-04. Base f6e51d1c / frozen df206f47 (D16 full delivery41919086629 pending).
No B228 source edits yet. This follows the retained declaration milestone, not a
claim that the substantial CPU objective is complete.

Evidence/source gate:
- Reanalyzed existing5c827e68 CPU stacks without a new capture. Text47 interface
  leaf samples:24 first managed caller RetireTo,19 generated prepared-target
  MoveNext,4 elsewhere. This is observed useful work too, not removable CPU.
- Native dictionaries already use IndexOf+SetAt and have no key wrapper to remove.
  Mutation interfaces have genuine workbook/worksheet/environment/ChainMap/list/
  deque implementations; do not delete them just to avoid probes.
- CheckExecution is already inline/no locks; small integer additions already skip
  magnitude admission. CLR integer ingress is supported; universal numeric/stack
  rewrites are not justified by the measured 2.35%AddExactIntegers leaf alone.
- Immutable mutation index-order hypothesis disproved: installed isolated CPython
  and current Release probe both call __index__ for tuple/str/bytes/range mutation.
  Preserve these probe outputs and ordering; no correctness fix inferred.

Concrete selected replacement:
ExecutePreparedTargetOperationAsync and DeletePreparedTargetAsync currently enter
async state machines for completed stores/reads/unpack/deletion even in Run(false).
Use one ordinary shared switch returning ValueTask, invoking the same existing
sync/async child operations. Consume completed ValueTasks once and finish current
publication/retirement immediately; use small continuation methods only when the
child really suspends. Retain every member/subscript/slice/read-retained/unpack/
raise/assert/delete case, same current context/spans/evaluation order. Do not add a
native type whitelist, alternate execution route, mode or general dispatch framework.
Existing lower helpers and protocol dispatch remain; no scope/ambient removal
without a separate lifetime proof. Hot code-object and ordinary call paths unchanged.

Cleanup contract:
- Store operands remain in the same stack slots until actual operation completion;
  then retire exactly once on success or error. Do not put successful retirement
  inside an outer catch that would retry it if disposal throws.
- Typed read completion publishes through existing stack methods; failed reads
  retain their original RetireTo(first) behavior. Retained user slice keys are
  created once and used by the later store unchanged.
  Await into a local BEFORE invoking the mutable struct stack: an await in its
  method argument risks lifting a receiver copy. Corrected during source review
  before any new tests/freeze; force a final nonincremental rebuild.
- Materialization lease stays owned through unpack publication and disposal,
  including actual suspension/exception. Keep current arity/starred ordering.
- Member assignment/deletion bool results preserve exact failure messages/spans.
- Native pending disposal stays awaited by the existing instruction driver;
  retained aliases, coarse capacity funding, first refusal and cancellation remain.
  No abandoned work, new owner/pool/ledger, tracing, GC, counter/fuel/quotas or unsafe.

Hypothesis: remove common async wrapper/state construction and transport around
the heavily used target family in CSV/text/JSON aggregation and field edits.
No predicted speedup; native interface checks are still useful and not all removed.
Source must retire displaced always-async paths, not wrap them in another layer.

Validate focused Release target/assignment/slice/member/deletion/unpack/for/augmented
and native-storage/ownership/refusal/cancellation cases. Preserve index-hook ordering
(including immutable error cases), reentrant length changes, actual pending setters/
index hooks, escaped operands and budget baseline after awaited cleanup. Existing
fixtures first; add only missing externally observable failures, no checkpoint counts.
Then one bounded affected/control complete-script source pair with exact binaries,
ordinaryGC/tiering, quiet/full goldens/owner/joins. No CPU claim from allocation alone.
No full platform/package rerun until coherent new delivery. D16 CI remains separate.
