# R10 selected migration / deletion list

Source gate: existing CallCompletion/prepared-value storage can retain the new
instance until initializer completion. No new runner/activation/owner is needed.
Default allocator is proven by the sealed engine slot; canonical builtin metatype
is proven by its self-metatype identity, not source names. JSON/dynamic allocator/
descriptor/keyword cases retain the shared generic constructor semantics.

Prototype A (not a delivery by itself): use the existing frame runner for ordinary
compiled positional __init__. Enter managed admission before allocation, allocate
once, then select the live initializer. Add a Constructor completion kind; reuse
the existing prepared-value field (renamed from its type-subscription-specific
name) to retain the instance. Preserve native-return/frame/caller unwind. If a
host admission hook changed init eligibility, hand the existing instance to the
shared initialization stage; never allocate again, retry or hide refusal.

Completion B, required before retaining/committing the milestone: retire R09's
PyType operand overloads, dual input representation and type switch in the shared
operand wrapper. Normal constructors use canonical frames; actual dynamic forms
use the existing general argument binder and shared allocation/initialization
stages. Preserve useful owned-copy admission and separate receiver binding.
No dormant compatibility methods or extra constructor backend left behind.

Focused gates: previous ten Python constructor goldens and genuine pending allocator/
descriptor/init tests; native cancellation/refusal and no-placeholder checks;
type subscription/native-return completion/exception/finally/generator/closure and
managed stack controls. Add a host-admission mutation test proving that a newly
ineligible initializer runs on the single existing instance. Both actual runtimes.

Only after B: one short class-record warm/fresh pair and unchanged CSV/text
controls versus exact installed933a7fff, same frozen worker/protocol/quiet VM,
full goldens/ownership/Closed/joins/persistent evidence. Record complexity and debt,
do not predict a large gain or infer allocation from deletion. Allow bounded
refinement for demonstrated source/measurement defects; no fixed-percentage veto.
Batch full platform/stress/installed consumers at this coherent boundary. Earlier
R09 CI42201490733 qualifies only933a7fff; no publication.
