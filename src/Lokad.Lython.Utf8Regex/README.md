# Lython Utf8Regex adapter

Applications can reference this non-packable project and pass
`Utf8RegexProvider.Instance` to `LythonEngine`. The adapter uses pinned
PythonRe 0.5.0. Core ships independently through its Lython-owned provider
contract.

Compilation uses the active token and a program scope. A one-shot match owns
its working reservation directly under the caller's allowance. Cursors keep
their own working scopes.
The producer requests admission for its own storage and the adapter funds
copied UTF-8 group names. Each producer owner can reuse reservation capacity
until its last live claim ends. Dispose cursors, subjects and then the pattern
once their users finish. Admission refusal is terminal and cleanup refunds
each owner synchronously.

The original UTF-8 subject and separate byte bounds preserve lookbehind,
anchors and absolute capture ranges. The pattern borrows the subject for
each one-shot call. One-shot matches with zero or one capture group use
local range slots. A single group's participation determines
`lastindex`. Multi-group one-shot matches and iteration read last-close metadata
from their funded range snapshot and release it after copying. The active token
reaches compilation, one-shot matching, cursor creation and each advance.
Cached patterns hold no prior caller token.

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
