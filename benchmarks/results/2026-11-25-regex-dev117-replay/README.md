# Optional adapter replay with PythonRe 0.5.0-dev.117

The exact local PythonRe and Core development packages contain producer
revision `d657d7e61`. Lython `55c73e564` uses the existing private adapter
bridge for whole-match and single-capture results. Package and assembly
hashes identify the inputs and verify the binaries in the public test and
cancellation subprocess directories.

After locked restores, 90 targeted Release checks pass on Windows .NET 11
and Ubuntu .NET 10. They cover adapter semantics, capture ranges, cursor
lifetime and in-flight cancellation. The core restore has no Utf8Regex
dependency. The first Windows build lacked the benchmark worker sources
linked into the cancellation fixture. The replay restores those exact
sources from the same Lython revision before building.

The development package preserves the tested bridge behavior. The production
adapter pin remains at 0.4.0. Reversed anchored windows, staged parser/compiler
work and the remaining backend metadata requirements still need final
released-package qualification. This replay has no backend timing result.

[Package and test identities](identity.json) · [Procedure and logs](procedure.tar.xz) ·
[Earlier bridge evidence](../2026-11-25-regex-050-artifact-replay/README.md).
