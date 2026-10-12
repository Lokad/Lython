# Consistent primitive storage accounting

List, tuple, named-tuple, set and property storage now use the same numeric
classification: Boolean, every supported CLR integral ingress type, double and
decimal. The change removes **10 net production lines** across four files,
including list's duplicate fee constant and three partial numeric classifiers.
Existing RuntimeMemoryEstimates and the integer ingress predicate carry the
rule; there is no new state, identity map, canonical-value layer or public switch.

The original process-isolated measurement retains 4,096 distinct Boolean boxes.
Those boxes alone occupy **98,304 bytes**, while their governed list accounted
**65,600 bytes**. Debug and Release reproduce the same undercharge. The shared
64-byte allowance per numeric slot fixes it: the unchanged backing plus 4,096
allowances gives a conservative 327,744-byte estimate. The same retained-box
coverage check and all 39 numeric-kind storage controls now pass.

Repeated numeric aliases pay per occupied list/tuple field; set equality
duplicates pay once. Boolean object identity and integer/Boolean equality stay
unchanged. List/set release fees depend on the actual stored type, independently
of payload registration. Large integers keep their existing separate owners;
immutable/property storage keeps its size estimate for unowned magnitudes.
Append, replacement, deletion, clear, attachment and failed construction use the
existing funding/snapshot machinery. Boolean-heavy scripts and other formerly
omitted CLR integer types can require more headroom or deny earlier under tight
budgets. Nonnumeric storage retains its policy.

Implementation: 9db09229; test refinements: 0c67f2c5 and b602a4fe.
Qualified production: f62c6b9cbe24f97f1b6cd14f4aede74993699c70.
The [evidence](evidence.json) records source trees, actual binary build producers,
Debug/Release DLL hashes, original observations and terminal test receipts.

Validation covers **10,284 complete Debug tests**: 2,086 white-box and 8,198
public. The public scope completed before two white-only test refinements; its
production/public-test trees and actual binaries match the final qualification,
so it was reused. The complete final white suite passed separately. Focused
Release passes **372 checks** (199 white, 173 public), with groups taking
1.5–4.5 seconds including cleanup. Earlier focused Debug passes 146 white and
173 public checks. Coverage includes all integral kinds, aliases, saturated
replacement/growth denial, Boolean/integer set twins, constructor/attachment
refunds, large owners, existing host interoperability, bounded Boolean turnover,
retained-slot denial and real delayed-host suspension.

Original controls distinguish 24 proposed-fee failures from the measured
held-box undercharge; 15 already supported numeric-kind controls pass.
The first box diagnostic hit a serializer field-name collision; that harness
failure is retained separately and contributes no accounting conclusion.

The initial candidate's three focused failures were old Boolean fee oracles.
The first full white run recorded two further cases. The original eight-Boolean
32-byte test now denies at attachment rather than backing promotion; its cap
and input remain as an explicit denial control, with separate nonnumeric
promotion coverage at the same cap. No memory cap was relaxed.

The other failure assumed exhaustion relief always collects every dropped
target. An isolated unchanged-baseline schedule demonstrates the existing
early-sweep behavior: after 100 targets were collected and another 100 dropped,
a 25,000-byte reservation fits in the original 2,000,000-byte envelope while
100 uncollected entries remain, with no generation-2 collection during relief.
Controlled collection and sweeping then reconcile all entries and charges.
The refined test checks exact residual accounting and successful admission
before controlled collection, then requires complete reclamation. A separate
retained-target control prevents releasing live charges. MemoryGovernor and
ChargeReclamationPool production code are unchanged.

This round establishes resource correctness and consolidates classification.
No performance collection or comparison lane was run. Forced-GC independence,
remaining wrapper-construction ownership and broader runtime/benchmark work
remain pending.