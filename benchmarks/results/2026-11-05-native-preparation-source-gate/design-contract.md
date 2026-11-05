# D28 source decision: one prepared native-body representation

2026-11-05. Read-only census on restored cf3dc9cd source/current9a9fc5ec assembly.
No runtime prototype or speedup claim. The bounded source gate selects the
following complete replacement contract for implementation; storage alone is
insufficient justification.

## Concrete work and storage

Native helpers guard/evaluate ordinary opcodes repeatedly: classify source/member
instructions, select call arity/method names, walk separate formatted/value proof
routes and recognize the same lower/upper+split graph at every invocation.
NativeFirst/NativeSecond occupy8B in EVERY40B instruction, including large module
bodies that cannot use native return execution. Their callers are confined to
native preparation/guards/evaluation plus instruction-copy methods. Ordinary
calls already borrow evaluated storage; no packet/frame allocation is promised.

Replace that provenance with ONE compact, immutable prepared native-body program
only for eligible helpers. A proposed16B node contains operation kind, original
instruction index, first/second native inputs. Existing executable call-argument
entries retain their native input integer (rename its coordinate explicitly);
removing that integer would not shrink their16B aligned representation. Original
ordinary code remains required for actual dynamic protocols. No new runtime
value packet, frame, arena, pool, cache, owner or JIT.

Remove the two native fields/constructor-copy parameters/WithNativeSources from
ordinary instructions: expected40->32B stride, unchanged physical input/result/
extent positions. Replace NativeReturnRoot with the prepared-body reference;
compiler stores source indexes against its already-final straight-line block.
Do not duplicate ordinary instructions, syntax, argument forms or source spans.
Preclassify the complete supported operation set, including formatting and the
existing lower/upper whitespace pipeline. Share existing immutable string-method
metadata where needed; do not create a general dispatch framework. Dynamic unary
builtin identity and type/namespace/protocol checks remain actual invocation work.

The read-only census's hypothetical retained savings after16B nodes+56B private
array/owner overhead are CSV776B, text624B, JSON696B, report328B, config632B.
These are representation estimates, NOT measured allocations/admission/CPU.
The new program's purpose must also be eliminating repeated structural decisions.
Compiler scratch/metadata and actual array stride need measurement after code.

## Required deletion and preservation

Replace the old native preparation and structural guard/evaluation code as a
coherent unit. Delete runtime NativeSplitCasing recognition and displaced
formatted/value classification routes; do not wrap them in a second prepared
layer. Existing scalar/string/format/ownership operation helpers remain useful.
The old separate NativeReturnNode graph was deliberately removed in B207: this
must NOT simply restore it. A typed operation program is justified only if it
actually removes structural classification and shrinks ordinary instructions.
If those deletions do not materialize, stop/withdraw rather than retain a layer.

Keep original source spans/checkpoints, entry/frame/traceback rules and binding
order. No early snapshots of actual fields/globals across later effects or
admission; formatted value guards may still need their pure native lookup before
later evaluation. Do not claim duplicate lookups disappear without that proof.
All guard failure precedes work; no fallback after effects or refusal. Unknown
methods/invalid static native shapes go directly to the ordinary executable engine,
preserving Python behavior, async protocols, closures and error order. No supported
syntax is removed. Current casing fusion/direct UTF8 formatting remain mandatory.

Original private cursor/builder disposal, pending users, cancellation, exact
integer promotion and first-refusal latch stay on existing ownership routes.
No new value-array lifetime, public switch, fuel, hidden cap or GC machinery.

## Acceptance and bounded completion

Before freezing code: prove both native and ordinary paths with current semantic/
function/default/alias/error/closure/generator/async/cancellation/refusal/shared-
budget controls. Preserve native helper eligibility for maintained scripts and
real protocols through the normal engine. Verify source coordinate consistency,
retained program ownership and absence of old native provenance/parallel graphs.
Record actual retained/compilation allocation effects separately from CPU.

One <=300s existing whole-source pair: CSV/JSON warm, text warm/async/fresh,
report warm/async/fresh, textcompile and ordinary branching-call control. Two
unchanged baseline roles, normal runtime,6s guestwarmup/2ssettle/9rotated200ms;
identities/full independent goldens/quiet endpoints/zero balances/owned joins.
No new benchmark API/external ratios. Retain all adverse/control uncertainty.
Temporary debt needs an explicit completion milestone and final-debt decision;
bounded correction permitted for actual evidence, not a local hint series.
Full Windows/Ubuntu actual11/10/originalStress/installed Release consumer delivery
after a retained coherent implementation; publication remains postponed.
