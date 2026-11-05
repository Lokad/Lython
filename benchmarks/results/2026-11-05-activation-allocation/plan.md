# D36 bounded complete-script allocation and activation gate

2026-11-05, retained a8e476bf/core402ae9f1, documentation c7500764. D34 full
41977587599 delivery active; earlier7efe fullyGREEN/cached. B260 complete439.56s/
16jobs/all gates: ordinaryCSV warm/fresh1.71/1.68×CPython versus nativeCSV1.46/1.43×;
text2.17/2.15×. Different scripts are not a one-variable call-cost subtraction.
Existing CPU profiles show real but limited frame/binding leaves and larger
overlapping instruction/collection work. Boxing alone does not explain the gap.

One new diagnostic dimension, not another CPU/elapsed capture campaign: reuse
installed BenchmarkDotNet diagnostic-client/TraceEvent assemblies in a private
external tool. CLR GCAllocationTick sampling only, no heap walk/dump, rundown,
forced collection, root inventory, production instrumentation or new dependencies.
Normal GC/tiering, existing protocol2 complete text/ordinaryCSV,6s guestwarmup,
1024 captured invocations each, <=60s owned collection/90s service bound. Sample
type/size/allocated-since-tick/thread/time; never raw addresses. This is sampled
allocation attribution, not exact counts/live heap, CPU gains or RSS evidence.

Capture setup/verification/batch/end boundaries, event loss, actual highest-CPU
guest thread from existing /proc snapshots and all other threads separately.
Preserve raw events/Ready/core/full stdout+file goldens/quiet endpoints/Closed/
exit0/worker and collector joins/fixture removal/free lease. Persistent evidence,
RAM guest fixtures, actual users finish before cleanup. No production source change.

Hypothesis/selection: complete ordinary activation or boxed-scalar traffic may
contain substantial repeated allocation worth removing coherently. Map actual
constructor/storage/lifetime work against sampled types and existing CPU evidence.
ExecutionContext ALREADY inherits ExecutionFrame, and PyFunctionBase retains the
actual defining ExecutionContext. Naively merging it into a transient interpreter
would conflate escaping lifetimes. Arguments/locals already share backing; names
are lazy; native helper returns already bypass frames. Preserve those delivered
paths and slower call-arena/control lessons. Text file reads already use native
UTF-8 windows and text helper is lower().split(), no hypothetical UTF-16/strip
intermediate to remove.

Stop after this two-case capture plus one actual construction/lifetime inventory.
Select a concrete whole replacement/deletion or record the missing proof; do not
automatically introduce a tagged-value system, int cache, owner ledger, pool or
dispatcher. Any selected redesign needs affected complete scripts, actual deleted
work, Python identity/alias/cell/default/evaluation/error semantics, genuine async
protocol behavior, effective cancellation and funded pending/escaping users before
code. Then one bounded controlled source comparison with warm/fresh/compile where
affected and explicit debt decision. No fixed-percentage veto or permanent closure.
