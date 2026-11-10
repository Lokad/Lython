# Immutable dev64 adapter compatibility

2026-11-10, consumer62e945a9 (runtime3a413c59, fully qualified separately).
Supplied Core/PythonRe0.5.0-dev.64 packages record repository9af7141f. Canonical
package bytes were checked twice and copied to an isolated feed; PythonRe pins
exact Core[0.5.0-dev.64]. No sibling build/edit or producer republishing.

86 internal / 524 public focused Release checks pass per actual .NET11/10. The
existing dev59/dev55 provider and cursor-admission bridge is byte-identical.
Separate assets/locks/outputs/cache keep normal0.4 pins/provider source unchanged.
Checks cover frontend flags/metadata, runtime re/fnmatch/glob, captures/offsets,
replacement/errors, current tokens, terminal refusal, joined cleanup and actual
cursor working admission. This is development contract/semantic compatibility;
it does not certify final released-adapter safety or backend performance.

The623 documented Core/PythonRe member names match dev59 exactly. This inventory
is not an API compatibility proof; the consumer checks supply the semantic evidence.
The remaining three gaps persist: DetailedData has no separate working owner,
UTF-8 group names lack enumeration, and cursor creation retains its token despite
per-move tokens. Their adapter-local bridges remain. Core releases/development
are independent; these gaps defer only affected final adapter qualification.

The dated NuGet flat-container observation still lists0.4.0 as the newest published
version; it is not a claim about a future gallery. No package pin is promoted.
An initial setup expected old XML at the earlier recipe root; reading it from the
verified dev59 package fixes that inventory path before any build. Original
conditions survive. No timed comparison or performance gain is claimed.

[Package/revision/bridge identity](identity.json), [loaded files and checks](qualification.json),
[member inventory](api-difference.json), [exact bridge](Utf8RegexProvider.cs),
[cursor admission checks](CursorAdmissionTests.cs), [isolated recipe](qualify.py),
[logs/locks/XML/nuspec/setup evidence](observations.tgz).
