# B196 typed integer creation prototype

Source 2d46dc8e runtime / 3e863f40 evidence. B195 external checkpoint is complete;
5d8c7239 delivery repair is independent. This is a bounded candidate, not promotion.

Hypothesis: consolidate freshly produced integer boxing BEFORE object transport.
Use one creation function in existing PyNumberOps, with a fixed immutable -5..256
constant table; retain boxed BigInteger everywhere. Arithmetic, int/text/JSON
parsing and range/enumerate production use it. Existing-object ownership/alias
returns never canonicalize. Delete redundant OwnHeapInteger forwarding and eager
boxing at migrated sites. No tagged representation, dynamic value/identity cache,
refcounts, alternate binder, new ownership/pool abstraction or accounting precision.

Untimed CPython AST/JSON-token distribution verifies original five-script goldens:
CSV/JSON all4096 parsed integers are small, only414/6144 arithmetic results;
text1280/12288 arithmetic results, report1024/3072 parsed and256/3072 increments.
These are value distributions, not Lython allocation/CPU attribution. Miss-heavy
text/report and above-range counter controls are essential; do not predict a
whole-gap explanation or CPU gain. Existing B170 integer allocation is historical.

Ownership/compatibility: the fixed table is about10.5KiB of process-wide immutable
runtime constants, like existing ASCII characters/builtin definitions, covered by
documented framework/control headroom. It cannot grow or retain guest data/budgets/
tokens. This is not an independently capped cache or new allowance. Substantial
container capacity/major magnitudes/replacement overlap retain existing admission
and live owners; no magnitude refund or refusal retry changes. Python does not
promise distinct identities for separately created equal immutable ints. Existing
aliases, int(existing), positive unary and nonnegative abs preserve their exact
object. Internal representation/type/hash/bool distinctions remain BigInteger.
If that fixed-metadata contract is unacceptable in source/validation, defer the
constant table rather than creating a new ownership subsystem.

Acceptance: exact arithmetic, negative floor/modulo/bitwise, overflow/promotion,
large-value refusal before mutation, Python identity/aliases/projection, JSON
parse hooks, enumerate consumption order, terminal refusal and token cancellation
on both APIs/actual11+10. Existing public numeric/ownership and white-box magnitude
families, plus one focused externally observable alias/arithmetic regression.
No tests promising a public interning range. No return-shape changes to helpers
that need BigInteger as a value type.

One complete five-script warm pair both APIs plus small/above-range loop controls,
independent unchanged before/after roles, original goldens, normal GC/tiering,
separate allocation/CPU/elapsed, 3s warm/9~200ms rounds, roughly4min owned under420s.
Use existing protocol/tooling; no new framework or CPython refresh. Capture one
bounded confirmation/refinement only if concrete uncertainty/adverse evidence
warrants it; preserve all original data and failures. Retain, refine or withdraw
with explicit debt decision. Full delivery only if retained; do not overlap full
local checks with ongoing CI or claim candidate qualification from unchanged CI.
