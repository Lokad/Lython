consolidate arithmetic/bitwise precedence parsing (prototype).**
Hypothesis: every ordinary operand currently descends six fixed precedence
layers. Parsing is a prominent current compile component; two layer dispatch
routines have38 self samples of613, not a removable CPU fraction. Replace this
fixed descent with bounded precedence climbing for bitwise-or/xor/and, shifts,
additive and multiplicative operators. Delete their chain of forwarding methods,
layer-transition switch and duplicate token dispatch. Leave Python boolean/not,
comparison chains, unary/power, lambdas, comprehensions and postfix parsing with
their existing semantics. No generic parser framework or AST/cache/value redesign.
Affected: compilation of complete CSV/text/configuration agent scripts; warm
execution is the unaffected phase. No predicted speedup or warm-gap attribution.

Source/semantic acceptance BEFORE code: identical AST precedence/associativity,
Python values/effects/order under both APIs, chained comparisons, short circuit,
unary-vs-power and matrix multiplication. Preserve diagnostic codes/messages/
spans for malformed operands and the existing per-precedence operand bounds,
managed nesting safety, token-based cancellation and coarse compilation admission.
No removal of supported syntax or independent resource checks. Recursive descent
must depend on the finite number of precedence levels, never operator-chain length.
Existing semantic/diagnostic/stack/cancellation/refusal tests plus independent
mixed-precedence goldens; no checkpoint-count or architecture-only assertions.

Comparison: freeze fresh nonincremental before/candidate core identities with
byte-identical auxiliary binaries. One fixed before/candidate/identical-before
collection on complete CSV/text/config sources, compile-only primary phase;
complete fresh config and warm text as separate controls, existing tooling.
<=30s per child /90s aggregate plus owned cleanup, ordinary tiering/full goldens/
quiet gates. Counts frozen BEFORE collection: compile512 warmups /7batches of4,096
compilations per case; fresh config and warm text4warmups /7batches of128
complete jobs. One child per role/case/phase; no tuning or retiming.
Require >=10% lower compilation process CPU against BOTH controls on two complete
scripts; no >5% regression on any other phase, <=5% CPU /0.2% allocation baseline
drift, resolved CPU batch granularity. Allocation alone is not success. Remove
prototype if semantic criteria fail or material target is missed; retain evidence
and independent correctness tests. Full delivery only if this source is retained.
Progress: prototype removes30 net parser lines and five precedence wrappers,
layer-transition and two-input token dispatch. All713 focused .NET11 regressions
pass, including23 new independent precedence/diagnostic/protocol cases. Final
nonincremental build and same focused actual.NET10 checks running. No timing
candidate frozen/launched yet; no performance acceptance or source commit claimed.
Source backup/fresh baseline/pre-code criteria under .git/agent-notes/review-20261031/precedence-b46.
