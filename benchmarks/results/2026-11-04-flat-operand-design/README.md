# Flat operand design gate B206

2026-11-04, committedbf0d38f3 production/frozen454cd98c. Gate complete with a
concrete next prototype design, not an implemented compiler or timing claim.
No core change or repeated full/producer qualification follows from this model.
The larger performance objective remains open; B205 sync-text debt is not closed.

## Current source and managed layout observations

Reuse the current checked catalog and B199's read-only reflection inventory, add
managed Array.CreateInstance allocation-difference probes. Both actual.NET11 RC3
and10.0.8, Release: current instruction24B; three-int+opcode+span model24B; four-
and five-int+opcode+span models32B; current NativeReturnNode24B. No Unsafe, pointer,
Marshal.SizeOf, native probing, forced collection or production instrumentation.
The model is layout/allocation only, not CPU/elapsed or a validated compiler.

All five representative scripts plus the above-range loop compile successfully.
Current report render is now native-return eligible; the older B199 inventory
predates B201 and must not be called current. Existing helpers allocate no general
frame/operand workspace on a successful native proof. Bindings already are locals.

| Code | Instructions / transfer upper bound | Existing instruction+native-node payload | Optimistic flat payload | Existing slots / naive SSA slots |
| --- | ---: | ---: | ---: | ---: |
| CSV module |109 /50|2616B|1888B|20 /71|
| Text module |90 /39|2160B|1632B|19 /61|
| JSON module |99 /47|2376B|1664B|19 /64|
| Report module |67 /28|1608B|1248B|14 /48|
| Report render |19 /6|888B|416B|6 /14|
| Config module |80 /35|1920B|1440B|14 /52|

Transfer upper bound counts all local/constant loads and local stores. It is
optimistic, NOT safe elimination or a prediction. Snapshots, failable reads,
mirrored stores and source attribution can require keeping instructions. Payload
excludes array headers, source spans, call metadata and compiler scratch. Naive
SSA grows normal frame slots3-4x; do not adopt instruction-count workspaces blindly.
Native render14slots would matter only on genuine guard failure, not its normal
native path. Actual target sizing and budget/headroom must be established by code.

## Selected B207 prototype: one flat executable representation

Hypothesis: remove per-value push/pop/count/clear transport and safe leaf/store
dispatch, while replacing the auxiliary native-return graph with the same value
links. Target complete text/report/CSV/JSON and helper/ordinary-call controls.
No new engine, metadata input-reader plans, global tagged values, arena, policy,
public option or frame pool. B147/B185's slower metadata lookups stay withdrawn.

One32B instruction:opcode+span and five int roles (destination,first,second,
auxiliary,live operand extent). Roles are defined by opcode. Binary uses two value
addresses+operator; member uses receiver+name+cache; call uses the existing callsite
and a contiguous packet base. Variable-arity displays/slices/prepared operations
consume contiguous packets, not new per-operation descriptor arrays. Compiler
knows extents from existing shape analysis, including retained operands, exception
edges and generator entry. This is compiler structure, never execution fuel.

Use one admitted array with local prefix and reusable temporary slots for ordinary
frames. Nonnegative addresses index it; negative addresses denote constants. Keep
checked representation bounds rather than narrowing to a tagged30-bit index.
Reuse existing bindings and explicit frame ownership; establish cache/cell/result
owners and replacement overlap before allocation. Clear actual retired operands,
including cursor ownership, at current semantic boundaries; await all pending users.

For structurally closed native-return code only, assign temporary result addresses
to their defining instruction indexes. The same flat instructions then are its
native proof/evaluation graph: native guards/evaluation follow source addresses
without allocating general workspace. The ordinary engine reads the SAME fields
if pre-effect native guards fail; no alternate executable representation or retry
after work/admission. Call packets can use consecutive explicit copies so3-argument
native calls remain supported and recursively resolvable. Native copies/packet
addresses require a measured cost check; do not promise original speedups survive.

Ordinary code reuses physical temporary slots according to existing operand shapes;
it needs no producer graph because native evaluation does not accept it. Keep
function cells, mirroring and generator retention behavior. Native eligibility is
an existing compile fact, not a public/backend switch. Precise encoding, slot reuse
and proof-depth behavior must be demonstrated before any replacement is retained.

## Semantic fences and attribution

Read effectful names/closures/descriptors at their original point. Snapshot values
before later arguments, callbacks, rebinding or suspension; contiguous packets
must not defer evaluation. Keep failable local loads unless existing facts prove
initialization and ordering; preserve their original spans. Fold stores only
where cells/mirroring/namespace admission cannot be bypassed. Resolve any missing
operand-source attribution explicitly rather than silently broadening error spans.
Do not introduce another expensive general analysis solely to remove a few copies.

Cover global/nonlocal mutation during right operands and arguments; unbound reads
before side effects; custom arithmetic/subscripts; duplicate/reordered keywords;
identity/aliases; short circuit; exceptions/finally/with; closures/class cells;
comprehensions, imports, generators and genuine async host/protocol suspension.
First refusal stays terminal; no fallback after effects. Keep cancellation in
nonallocating loops and long operations, managed stack admission and funded cleanup.

## Implementation/deletion and completion boundary

1. Model all opcode/prepared-operation packet roles, branches/joins/exception
   extents and native links in compiler output. Keep production unchanged until
   supported families are represented and attribution/snapshot fences are explicit.
2. Replace the existing engine's operand access using those fields, preserving
   sync-prefix/async suspension routing, direct native borrows and existing binder.
   This is one replacement; no retained second backend or compatibility flag.
3. Remove displaced ExecutableValueStack push/pop/count transport, local-to-operand
   forwarding, NativeReturnNode and PrepareNativeReturn's retained graph. Preserve
   useful semantic helpers and actual cursor/owner cleanup, not dormant scaffolding.
4. Focused actual11/10 correctness, token/refusal/ownership/real suspension checks.
   One bounded source-pair whole text/report/CSV/JSON and ordinary-call controls,
   both APIs, independent old roles, compilation separate. IO/checks/prior-result
   cleanup included, startup/reset/hashing excluded; retain all adverse conditions.
   Compare against exact454 and explicitly retain/correct/withdraw remaining debt.
5. Full established Windows/Ubuntu, original stress and installed consumers at the
   coherent replacement boundary, then refresh representative CPython ratios.

Temporary regressions may be carried through these stages with source/conditions
and a completion decision. Timing percentages inform that decision, not veto it.
If32B encoding/native packet copies/snapshot attribution or reuse cannot satisfy
these invariants, revise or defer precisely before shipping. No open-ended audit,
unchanged retiming or new profiler/framework campaign is authorized by this gate.
