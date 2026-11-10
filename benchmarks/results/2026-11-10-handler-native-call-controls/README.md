# Bounded ordinary-workload confirmation

B524 checks the adverse report and Unicode controls from B522 with the same
frozen cores, source, toolchain, fixtures and included I/O/validation/prior-result
cleanup. No runtime or GC/tiering change was made. Two jobs, four processes/two per
source, five-second role warmups and seven rotated 150 ms rounds completed in
59.644 seconds.

| Workload | Elapsed change | Wider process CPU change |
| --- | --- | --- |
| JSON-to-Markdown report | +0.14–2.01% | −2.33% to +6.25% |
| Unicode index control | −2.72% to −1.28% | −2.39% to +4.97% |

This does not reproduce B522's +3.35–9.84% report/+2.25–7.25% Unicode elapsed
magnitudes. Report process spreads are 1.14% reference/0.72% candidate, but maximum
IQR is 6.50%; a small remaining elapsed cost and CPU uncertainty remain. No
equivalence or universal speedup is established. Original B522 results are
[preserved independently](../2026-11-10-handler-native-calls/README.md).

All output, balance, quiet-host, joined-worker, RAM-unmount, owned-archive and
independent local SHA checks pass. No workers/fixtures remain; terminal status is
cached. Archive SHA-256:
`8a8afcbb1d92f1adc3a1e50c6f72862d3b36469eaac4737a7948bf36f679c303`.
The finite owner was 120 seconds, with 30-second stop/margin. Existing verified
core/auxiliary files were reused by hardlinks. Exact duplicate compressed-copy
recovery leaves canonical tracked evidence/raw trees/frozen binaries intact.

B523 failed before guest work because its shell script had CRLF line endings.
Inactive ownership/no workers were verified and the empty RAM fixture was removed.
The fresh B524 setup asserts Unix line endings. A local summary initially expected
five jobs; only that assertion was corrected to the intended two. Neither issue
discarded timing observations or justified retiming the runtime.
[Original setup/checks](setup-and-checks.tar.xz), [summary](summary.json),
[owned raw archive](observations.tgz) and receipts preserve all conditions.
