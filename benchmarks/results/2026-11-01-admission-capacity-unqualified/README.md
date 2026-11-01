# Shared-reservation capacity trial stopped before qualification (B63)

2026-11-01. Prototype removed; production remains delivered35d8942c.
The declared source/compatibility gate stopped this trial before any benchmark.
No speedup, allocation reduction or demonstrated funding bug is claimed.
No tests, budgets, timeouts or containment assertions were changed.

## Hypothesis and proposed contract

Existing native JSON/CSV origins show distributed monitor/reservation work, not
an established removable fraction. The original aggregate governor resizes its
borrowed host reservation for every nonzero reserve/refund. This trial keeps
reserved/committed balances but admits aggregate capacity in4KiB units and calls
the host only when that capacity changes. Whole unused units return promptly;
zero storage returns to zero. One capacity field and arithmetic helpers; no new
budget/owner/pool/registry, mode, collector, weak references, quotas or forced GC.

The proposed compatibility tradeoff was up to4,095 extra charged bytes per owner,
earlier tiny-budget rejection and delayed sub-unit refunds. Scope creation remains
zero, standalone remains unbounded, all headroom uses the same borrowed root,
retained storage stays funded and no refusal retries a smaller request. Peak
reporting includes admitted headroom; first failure/span/original storage estimate
remain terminal. [Plan before code](pre-code-plan.md), [exact patch](source.patch.gz).

## Gate result

Zero-warning Release solution build. The initial net11 native selection passes
703/803 and fails100. Failures span tiny initial funding, exact balance deltas,
retained-payload funding observations and refusal/cancellation fixtures that force
a new host resize at a specific storage change. In several cases, already admitted
headroom means the fixture never reaches its intended refusal. These failures do
not prove that the proposed capacity arithmetic underfunds storage, nor establish
that its migration is worthwhile. They show a much broader contract/coverage
migration than this bounded trial allowed.

The plan explicitly requires restoration if broad changes are needed. Do not
weaken independent alias, pre-effect refusal or cleanup assertions to make this
prototype pass, or call the failed fixtures disposable architecture tests as a
group. No public test, stress run or timing collection follows the failed gate.
The prospective>=7.5% CSV/JSON target was never evaluated and is not relaxed.

Original governor restored byte-for-byte. Fresh nonincremental core/worker/test/
probe copies agree with the frozen baseline. The same803 native cases pass on
each actual.NET11RC3/10.0.8 after restoration. [Identities](baseline-identity.json),
[summary](summary.json) and [complete initial/restored logs](validation-logs.tar.gz)
preserve all outcomes. Existing full runtime checkpoint41319281716 is cached green;
no unchanged full platform/package validation or producer probing was repeated.

## Related source gate (B62)

Before this trial, current source disproved the planned B62 optimization premise:
Executable.Interpreter already routes exact binary/augmented operands, native
unary operations and common indexing through synchronous kernels. JumpIfFalse
uses synchronous truthiness for non-instances. Resolution.IsTruthyAsync is already
a non-async wrapper for native values. JSON strings/numbers already decode native
UTF-8. No duplicate native frontier, JSON conversion path, test or timing was added.
The bare async helper signatures alone did not describe executable hot paths.

Further work needs a concrete different ownership/storage design and bounded
acceptance, not a new quantum, favorable retiming or unqualified test migration.
