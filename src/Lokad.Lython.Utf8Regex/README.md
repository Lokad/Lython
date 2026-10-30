# Lython Utf8Regex adapter

Optional PythonRe 0.4.0 integration for Lython, kept as a non-packable project in
this repository. No adapter NuGet package is produced or published. Applications
can reference the project directly. The core package has no dependency on this
adapter or its backend. Bind `Utf8RegexProvider.Instance` explicitly when
constructing `LythonEngine`.

Compilation and actual matching/shaping receive the active CancellationToken.
Cached patterns retain no caller token. A cursor owns its cancellation source;
each advance registers the current token only for that operation, drains the
registration and finishes backend cleanup before returning its working allowance.
There is no background matching, timeout/work quota or retry. Individual CLR
encoding/allocation calls remain atomic; cancellation is cooperative.

Each compilation returns a fresh disposable pattern handle with a child scope
borrowing the supplied owner. Dispose cursors, then subjects, then the pattern;
finish all operations and metadata borrows first. Pattern disposal drops managed
backend references and refunds program/replacement-cache funding without closing
the borrowed parent. It is idempotent; a closed pattern cannot prepare new work.
Prepared working scopes
share that allowance and refund on disposal. Iteration discovers one match at a
time and funds current scratch/snapshots instead of all future matches. Non-ASCII
subjects fund a byte-to-scalar map once; captures borrow original UTF-8 slices.
Replacement-cache capacity stays on the compiled-pattern owner after subject
cleanup, conservatively funding sixteen possible plans and replacement overlap
at the largest requested template size. This is a capacity estimate, not cumulative
allocation counting; small/many-template workloads may reject early. Large subjects,
captures and expansions also reserve conservative peaks before backend work.
No independent allowance is imposed when the supplied root is unbounded. Hosts
retain acquired inputs until all users finish and allow headroom for incidental
backend/framework storage; these estimates are not an enforced backend heap bound.

Matching uses Unicode scalars, including supplementary characters. Captures keep
Python opening-order numbering and the backend's last-closed capture metadata.
Native cursor progression handles adjacent empty/nonempty matches, including
`.*?`, without a pattern-specific implementation. Effective flags include global
prefixes across ignored comments/verbose whitespace; scoped flags do not leak.
Lython retains its documented str-pattern subset; bytes, LOCALE and Unicode-name
lookup are excluded. Backend construction backstops and internal safety remain
separate from core qualification; selecting this provider does not certify its
internals or couple core releases to them.

Validation separates the [cooperating-provider contract controls](../../tests/Lokad.Lython.PublicApi.Tests/RegexProviderContractTests.cs)
for forwarding/unwind from [real-adapter capability controls](../../tests/Lokad.Lython.PublicApi.Tests/RegexAdapterCapabilityTests.cs),
[isolated in-flight/lifetime checks](../../tests/Lokad.Lython.PublicApi.Tests/RegexAdapterLifetimeTests.cs)
and [independent CPython goldens](../../tests/Lokad.Lython.PublicApi.Tests/RegexAdapterSemanticsTests.cs).
The public-API suite references this adapter project directly; the
[Release package gate](../../tests/README.md#delivery-checks) qualifies only the core package.
The [historical integration-cost diagnostic](../../benchmarks/results/2026-10-19-regex-boundary/README.md)
is unqualified timing, not backend cancellation or safety certification.
See [runtime regex providers](../../docs/RUNTIME.md#regex-providers) for the current provider and adapter contract.
