# Native text-key cancellation — R172

2026-11-09, baseline43350e4f-equivalent corecc596abc, source2cba273c plus linked
final change. A contextual dictionary probe computed a cancellable hash but then
used a context-free whole-span string equality scan. Cached hashes expose this
gap. All three native candidate comparisons now forward the active context to
existing chunked text equality. CLR comparer callbacks keep their original body;
custom-key dispatch and async protocol behavior remain. No token cache, policy,
owner, traversal infrastructure or public API change.

The new assembly against frozen R167 reproduces a read returning after cancellation
was requested. Its write legally cancelled; the original proof script incorrectly
required both cases to fail. Retain that assertion/log and correct analysis to the
one demonstrated regression without rerunning. The same prototype assembly passes
current checks. Final unchanged test source is rebuilt against the refined core;
its binary differs, so this is not represented as a final-assembly frozen proof.

Final27internal/825public Release checks pass per actual.NET11/10, including native
text/hash/scalar comparisons, dictionary protocols, CSV and first refusal. Internal
includes2new256MiB cached-key Stress probes; final public excludes Stress. Original
837public per framework accidentally included existing long CSV Stress cases and
passed; retain their evidence. Workers join before input cleanup, cancelled writes
preserve their value, current tokens/first refusal/alias funding and zero working/
final shared balances remain. Legal completion before the request checks its value.

One reused five-script complete-job desktop diagnostic compares warm sync/async
with two identical original controls. All goldens, funding, auxiliary isolation and
cleanup pass. Initial source routed CLR comparison through the new context-aware
body: async config+6.76–10.22% elapsed, textsync−0.98..+6.46%, controls drifting.
Preserve all adversity. Refine only the source boundary: leave the context-free
body untouched and use a separate current-context entry for native text probes.
This restores config within controls but CSVsync+9.21–10.96%/async+6.15–6.51%
remain adverse. Source review finds that the explicit entry also lost the original
identity shortcut, adding checks for a constant-time same-object key. Restore that
shortcut before text scanning. Final focused checks and one last changed-source
comparison close this bounded batch; preserve both prior attempts. Exact final
results remain in desktop.json;
no CPU gain or equivalence is claimed from shared-process desktop observations.
No quiet-VM rerun or new CPython comparison for this correction.

Final sync CSV+5.13–5.86% and async text+3.78–12.72% remain possible costs;
async CSV−5.17–7.94% and sync text−10.19–15.68% do not establish gains. Other
controls drift, including async report−1.24..+13.55%. Substantial allocations are
unchanged; tiny config differences remain visible in raw bytes. Accept possible
elapsed costs for effective cancellation, with uncertainty/debt carried to the
next substantial controlled checkpoint. This is not a finding of equivalence or
an automatic veto based on a fixed percentage, and later justified investigation
remains possible. Do not remove cancellation to recover a benchmark number.

Retain effective cancellation and the confined comparer change; this correctness
milestone does not complete the whole-script performance objective. General
context-free CLR operations still have no token; this does not promise cancellation
inside every native type comparison. Full platform/stress/package qualification
batches at the next delivery boundary; R167/R171 CI does not qualify this source.
Run42672245421/source230a0572 passed all Ubuntu framework/stress/package stages,
but ended in overall failure with no Windows job/check and no failed-job log.
[Cached partial qualification](ci-terminal.json) is not an all-platform pass;
Windows remains pending. Publication remains postponed.

[Final source identity](identity.json), [final diagnostic](desktop.json),
[original identity](original-identity.json), [original adversity](original-desktop.json),
[intermediate identity](refined-identity.json), [intermediate adversity](refined-desktop.json),
[frozen regression and analysis correction](regression-proof.json),
[focused qualification](qualification.json), [exact sources/raw/logs/initial proof binary](source-checks.tar.xz).
The reused driver's historical R157 label identifies its original implementation,
not a new whitespace change. All three attempts use ordinary workstation GC/tiering,
shared-process JIT/GC and identical auxiliary images; timings remain diagnostic.
