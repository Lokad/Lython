# R210 — immutable development producer 0.5.0-dev.66

Development compatibility only; the normal non-packable adapter remains pinned
to 0.4.0. Core has no producer dependency. Immutable PythonRe package SHA
**e208d443b5a28b854d44ef285bf6904ae7751d62dfebb5225623ee289f02f155**,
producer commit **b478f853f722b7c34de83bdcd14693ee594ca053**. The moving sibling HEAD
was not substituted. [Package identities](identity.json), [checks](qualification.json),
[source/configuration/logs/loaded binaries/packages](qualification.tar.xz).

The unchanged development bridge builds with zero warnings/errors and passes
**86 internal / 524 public focused regressions per actual .NET 11 and .NET 10**.
Coverage includes regex semantics, frontend analysis, fnmatch/glob, cancellation,
ownership/refusal and cursor admission. This is focused compatibility qualification,
not a new backend performance measurement or final released-adapter certification.
All 29 normal source/pin/lock/output identities remain unchanged; see
[isolation proof](normal-state-proof.json).

The new Utf8InputValidation option defaults to Validate. AssumeValid requires the
entire borrowed subject to be valid UTF-8 and stable for its lifetime. The public
Lython provider subject currently validates bounds/scalar boundaries rather than
guaranteeing whole-subject validation and immutability, so this gate keeps Validate.
No new core precondition or silent validation bypass is introduced. XML documentation
differences describe documented members, not a complete public-API inventory.

Three final native capabilities remain unqualified: a separate single-match
working owner, native UTF-8 group-name enumeration, and token-independent cursor
creation while preserving cancellation during preparation. Per-move tokens already
exist; creating with CancellationToken.None is not proof of cancellable preparation.
Temporary bridges stay outside core. The producer source uses Unsafe.SizeOf<T> for
size/accounting calculations; no blanket source-policy compliance is claimed by
this development test. Core architectural work remains independent of that assessment.

No producer pin promotion, release publication, final safety certification or claim
that the three missing capabilities were delivered. These precise limits defer
only the affected final adapter checks, not Lython development.
