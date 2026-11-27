# Lython Utf8Regex adapter

Applications can reference this non-packable project and pass
`Utf8RegexProvider.Instance` to `LythonEngine`. The adapter uses pinned
PythonRe 0.5.0. Core ships independently through its Lython-owned provider
contract.

Compilation uses the active token and a program scope. One-shot matching
and cursors use separate working scopes under the caller's allowance.
The producer requests admission for its own storage and the adapter funds
copied UTF-8 group names. Each producer owner can reuse reservation capacity
until its last live claim ends. Dispose cursors, subjects and then the pattern
once their users finish. Admission refusal is terminal and cleanup refunds
each owner synchronously.

The original UTF-8 subject and separate byte bounds preserve lookbehind,
anchors and absolute capture ranges. The adapter fills the caller's capture span and
reads last-close metadata before releasing the funded range snapshot.
Bounded one-shot calls use a cursor to obtain last-close metadata. The active
token reaches compilation, cursor creation and each advance. Cached patterns
hold no prior caller token.

Reversed anchored `match` raises a capability error with this backend.
Reversed search, fullmatch and iteration return no matches. The adapter also
reports a capability error for staged parsing or compilation that requires
separate guest flag callbacks. Lython shapes split, expansion and replacement
from capture ranges. `Pattern.flags` preserves the guest's reported flags and
reflects any global inline prefixes. Scoped flags stay local.

The [runtime guide](../../docs/RUNTIME.md#regex-providers) describes the core
contract. [Adapter semantics](../../tests/Lokad.Lython.PublicApi.Tests/RegexAdapterSemanticsTests.cs),
[backend lifetime checks](../../tests/Lokad.Lython.PublicApi.Tests/RegexAdapterLifetimeTests.cs)
and [cooperating-provider tests](../../tests/Lokad.Lython.PublicApi.Tests/RegexProviderContractTests.cs)
exercise distinct parts of the boundary.
