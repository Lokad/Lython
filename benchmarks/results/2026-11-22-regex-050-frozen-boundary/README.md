# Utf8Regex 0.5.0 frozen candidate boundary

On November 22, 2026 UTC, the local producer candidate feed contains
the frozen Release packages from `61228b20f`. The separate `artifacts/nuget`
copies with the same version identify older source `dcfe880ab`. The
probe verifies the candidate package and assembly hashes against its
producer manifest and loads the exact assemblies on Windows .NET
10.0.8 and 11.0.0.

- UTF-8 capture names, original-prefix lookbehind, bounded full match,
  byte offsets and last-closed capture metadata pass.
- Cancelling from inside the producer's working-storage admission hook
  results in `OperationCanceledException` with all scratch returned. A
  refused reservation raises the producer's memory admission exception
  once, with its requested size. These observations cover the direct
  producer path and leave general adapter interruption qualification open.
- Both direct match and its cursor reject a reversed anchored window.
  CPython 3.13.2 returns the empty whole match and participating capture
  at the independent start. PythonRe still exposes no exact compiled
  program key. Core still uses `Unsafe.SizeOf<T>` in capacity estimates.

The producer's recorded local package qualification covers its selected
package consumers. The optional Lython adapter stays at 0.4.0. Final
qualification remains scoped to a compatible released backend and its
integration with Lython's shared ownership and cancellation contract.

[Candidate manifest](producer-candidate.json) · [Direct probes](checks.tar.xz) ·
[Previous probe](../2026-11-21-regex-dev117-boundary/README.md).
