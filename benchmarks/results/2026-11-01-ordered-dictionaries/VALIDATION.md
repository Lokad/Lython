# B23 validation

Pinned Release builds, Windows, actual .NET 11 RC3 / .NET 10.0.8.

- Initial focused native: 452/452 pass, including storage, protocol keys,
  namespaces, refusal, capacity funding, saturated replacement and cleanup.
- Full ordinary source coverage: 3,942 native pass per framework; 15,300 public
  pass per framework, with only the two finite 4,096-row CSV cases failing their
  old 8 MiB fixture. The net10 no-build sweep still used that old test assembly;
  runtime source was current. This is recorded, not presented as a green sweep.
- Exact-input ownership diagnostic establishes the new 8,420,669-byte peak.
  After supplying finite CSV headroom, focused net11 has 133/133 batch/wrapper
  checks passing; rebuilt net10 has 213/213 batch/wrapper/order/type-hash/Counter/
  function-protocol checks passing. Rows, full output goldens and 512 KiB
  sustained-batch allowance remain unchanged.
- Frozen CPython 3.13.2 goldens cover both APIs through public tests. All fixed
  comparison jobs validate stdout/files and end execution/compiled ownership.

Raw logs are under checks/. Full platform/original-stress/package qualification
for this runtime source is a separate delivery checkpoint; T02's terminal green
41255777943 qualifies its preceding source, not this change.

Explicit Release pack at **472d64ef** and installed consumers pass on actual
.NET 11 RC3 / .NET 10.0.8, including dependency/assets inspection and mediated
filesystem behavior. Only core with Lokad.Parsing is packaged. Published 0.10
package/symbol hashes and v0.10.0 remain unchanged. Full Windows/Ubuntu/both-target/
original-stress/package checkpoint **41258876567** is queued/running; do not
attribute its terminal result before completion. No package is published.
