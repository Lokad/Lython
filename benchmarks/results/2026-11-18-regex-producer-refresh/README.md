# Changed-producer development adapter check

R521,2026-11-18 UTC. Frozen committed producer **7b194e55**, current corrected
Lython **9e8eaba1 / 84b1a159**. Source archive/hash retained; the sibling working
tree was not modified. This is development compatibility, not normal adapter
promotion, immutable released-package qualification, policy certification or a
backend performance comparison. Core dependencies/normal nonpackable0.4pin stay.

Producer removed bulk byte materialization and detailed/collector APIs. Lython's
R507 cursor adapter already avoids them. Its old seven-argument Compile API no
longer exists: initial build failure preserved. The only adapter source change
uses the producer's consolidated policy with an explicitly infinite match timeout;
default unlimited invocation settings/default construction backstops/infinite
construction timeout are unchanged. No new Lython quota or policy layer. Existing
bounded UTF-8 windows, captures, flags, completion metadata and coarse ownership
bridges remain; no text/detailed-match fallback was restored.

Both actual.NET11/10 pass363public regex regressions against identical private
parent/child core/adapter/producer images. Includes semantics, real in-flight
backend cancellation, first refused admission, cleanup, retained owners and
shared-budget reuse. Current core/full44250743374 qualification is independent.
No CPU/allocation claim; the older changed-producer check is not replayed.

Remaining producer gaps: native UTF-8 named-group enumeration; cursor creation/
preparation token detached from later pulls; separate per-match result staging
admission. Existing adapter-local name/current-token/coarse-funding bridges stay
explicit. Compile tokens are not stored in Lython's cached Pattern. Creation/pull
limitations still apply to actual backend cursors. AllowUnsafeBlocks=false builds,
but producer contains Unsafe.SizeOf calls: no safety-policy compliance is inferred.
Lython adds no unsafe C#/Unsafe APIs/native probing. Publication postponed.

[Exact producer facts](source-facts.json), [images](image-identity.json),
[checks](checks.json), [sources](producer-source.tar.xz),
[adapter/procedures/original failure](adapter-and-checks.tar.xz).
