# Immutable development adapter snapshot — R170

2026-11-09. Consumer 220732b7 (R167 production). Immutable producer 0.5.0-dev.59,
repository d6120197de6bdeef688836561a7b299dcdaa4096. Core/PythonRe packages copied
from the supplied canonical artifact feed, checked twice by SHA256 and nuspec
revision; PythonRe pins exact Core [0.5.0-dev.59]. No sibling source/build changes.

**86 internal and 524 public focused Release checks pass per actual .NET 11/10.**
The existing dev55 provider/cursor allowance bridge is byte-identical. Isolated
assets, locks, outputs and cache keep normal 0.4 package pins untouched. Checks
cover regex/fnmatch/glob, frontend flags/analysis, captures, offsets, replacements,
errors, caches/current tokens, terminal refusal, cleanup and actual cursor working
admission. This is contract/semantic compatibility, not final released-adapter,
managed-safety or backend-performance certification. No speedup is claimed.

Remaining bridges: DetailedData has no distinct working owner; native group-name
enumeration still uses GetGroupNames plus adapter encoding; cursor creation retains
its token despite per-move tokens, requiring stable current-token forwarding.
Native UTF-8 patterns/windows/captures and cursor working allowance are available.
Producer improvements do not remove these remaining API-shape limitations. They
defer only affected final adapter qualification; core development stays independent.
The optional adapter remains nonpackable; the core has no producer dependency.

An initial transient candidate-feed path disappeared during promotion, before any
consumer build. Preserve that failure; use the canonical immutable artifact path
and verified bytes for these checks. No timed collection or backend comparison.

[Identity/package hashes](identity.json), [loaded files and counts](qualification.json),
[exact bridge](Utf8RegexProvider.cs), [cursor admission checks](CursorAdmissionTests.cs),
[isolated recipe](qualify.py), [logs/locks/nuspec/XML and original failed path](observations.tgz).
Producer packages are not republished. Release observation is dated in identity.json;
it does not establish what a future gallery will contain.
