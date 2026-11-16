# Exact subprocess identity for regex cancellation qualification

R443,2026-11-16UTC. The real-adapter cancellation subprocess now reports SHA256
of its loaded core,adapter and two producer libraries. The parent requires exact
equality with its own loaded images before accepting cancellation evidence. The
runtime/provider contract is unchanged; this is controlled fixture output only.

The prior R409 private-parent script copied development assemblies into its test
image but located the normal repository WorkerFixture through Directory.Build.props.
It did not qualify the development producer in those three isolated child checks.
Their exact historical child bytes were not recorded,so do not infer them now.
Historical parent semantics/allocation/timing evidence remains; private-producer
in-flight cancellation was not established by that receipt. This correction is
linked alongside the original evidence without overwriting its observations.

A deliberate dev105parent/normal-child negative control fails all3stages on the
library-hash assertion. Full isolated images then provide their own root marker
and matching subprocess files: both the old immutable0.5candidate and newly
accepted0.5.0-dev.105 pass363public regex checks each on actual.NET11and10,
including real compile/match/cursor interruption,original tokens,zero cleanup
balances,flags/windows/captures,refusal and retained ownership. Normal0.4passes
all6lifetime cases each target. No timeout,assertion or guest contract is relaxed.

Both private adapters use identical R409native source. Dev105packages are copied
and SHA-verified before extracting target assets; mutable producer build outputs
are never consumed. PythonRe package89ad65d7/core49c0d0c2 are distinct from R409's
9370a922/42f1c4f9. No normal adapter pin/core dependency changes. Native names,
preparation-token plumbing and separate result-stage ownership remain final
adapter limitations. New affected-script comparison and cumulative platform/
stress/package qualification are tracked separately; publication held.

[Checks and deliberate failure](checks.json),[asset/image identity](identity.json),
[source,immutable assets,logs,TRX and procedures](sources-assets-checks.tar.xz).

[Cumulative normal-adapter/core delivery passes](../delivery/README.md): c18babe9/43869939863,
Windows/Ubuntu actual11/10,original stress and installed Release consumers. Private
dev105focused checks are Windows-only; Linux VM workload checks are separate.
