# Filename filtering and asynchronous iteration

R148,2026-11-09. Original7af1c86b reproduces an async-hosting defect:
`RunAsync` reports that open_file cannot run synchronously while `fnmatch.filter`
consumes a file-backed generator. The synchronous mock-host control succeeds.

Replace the sync-only ToSequence loop with one existing cursor loop. Acquisition,
pulls and disposal use the active execution API; genuine async iteration and cleanup
are awaited. Native UTF-8 wildcard matching remains independent of regex providers.
No thread/task wrapper, alternate matcher, new dispatch/ownership framework or quota.

305 focused public Release checks pass per actual.NET11/10, including10 new cases
and four independent CPython goldens. Covers generator/user __iter__/__next__ I/O,
cancellation through gated awaited cleanup with storage still funded, retained
iterator continuation after an invalid item, name identity and terminal admission
refusal despite guest suppression. Cleanup permits the same allowance's next run.

This is a correctness/capability fix, not a measured speedup. No elapsed or allocation
gain claim and no repeated producer/performance campaign. Full platform/Stress/
installed-package checks batch at the next coherent delivery. R145's full successful
qualification describes58d3, not this later change. Publication remains postponed.

[Validation/source status](validation.json), [independent Python goldens](python-goldens.json),
[original failure, source patch and focused logs](evidence.tar.xz).
