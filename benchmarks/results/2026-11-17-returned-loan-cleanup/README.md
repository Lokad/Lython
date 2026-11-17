# Returned private-loan cleanup: first delivery

Committed0d2a6ab7 fixes a compiler cleanup bound: consumed/forwarded helper calls
can own private iteration loans without a GetIter in the callee. Array-backed
cancellation cleanup omitted those results; the new regression fails before the
fix while native cleanup passes. Include those slots in the existing bound and
delete native cleanup's separate final full scan/proxy. Eight independent public
continuation cases retain effects, aliases, refusal, held I/O cleanup and reuse.

Focused actual11/10 each1,007internal/4,217public, then29affected internal checks,
pass. [Full44168196734](https://github.com/Lokad/Lython/actions/runs/44168196734)
**fails Windows.NET10stress**: CachedTextKeyProbeObservesConcurrentCancellation
(read). Both platforms' ordinary/package checks, Windows11stress and all Ubuntu
stress pass. All16summaries and the original exact inputs/logs are retained.
This is not full qualification; latest prior fully qualified runtime is33bd7efd.

The fixture read the token in a finally after the dictionary probe returned.
Cancellation can arrive between those events; that snapshot cannot order return
against cancellation. Source review confirms explicit context forwarding and4KiB
text scan checks. This explains a possible false positive, not a measured Windows
scheduling trace or proof of the exact triggering interleaving. Production stays
unchanged. Correct the invalid inference: allow completion to win, retain actual
cancelled results/provenance, join the canceller, then require a cached-key probe
to reject the cancelled caller without mutation. Keep large concurrent input,
timeouts, funding and cleanup assertions. Focused corrected4cases pass each actual
11/10. A subsequent coherent delivery must qualify the corrected fixture.

The tests demonstrate observable cancellation/side effects, not atomic cancellation
at return or exact checkpoint timing. Command-level cancellation remains unchanged.
No CPU gain or rebuilt-binary equality claimed. Publication held; performance
objective active. Three adverse shared-runner prototypes remain outside production.

[Receipt](qualification.json),[original inputs](inputs.tar.xz),[logs](run-logs.zip),
[before-fix failure](before-fix.log),[focused checks](checks.json),
[procedures](focused-procedures.tar.xz).
