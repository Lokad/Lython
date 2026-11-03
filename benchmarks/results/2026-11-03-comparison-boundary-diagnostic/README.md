# Included fixture cost — B144

2026-11-03, retained runtimef7dd0d3a, exact B143 core2f78a866. Read-only component
diagnostic; no runtime improvement. Existing complete-job driver plus component
clocks, three identical isolated images; CPython same invoke with reset/check clocks.
[Assessment](assessment.json), [driver](component-driver.cs), [CPython clocks](python-components.py),
[frozen fixtures](catalog.json.gz) and [raw observations](raw-components.tar.gz).

| Complete script | Lython full / Run / fixture reset ms | CPython full / reset ms |
| --- | ---: | ---: |
| Search |1.840 /0.358 /1.459|0.260 /0.112|
| Text helpers |5.028 /3.589 /1.421|1.321 /0.112|
| Report |8.114 /6.194 /1.637|2.824 /0.180|
| ASCII filename filter |2.129 /0.633 /1.453|0.333 /0.126|

Medians come from nine32-invocation batches; individual phase medians are not
additive. CPython remaining time includes capture/exec/checking, so it is not an
exact Run-only counterpart. Full output bytes, zero allowance balances and removed
fixtures verified; ordinary GC/tiering and before/after quiet gates PASS. Owned
service46.392s, exit0/inactive, lease free. Sparse process CPU figures include JIT/
other-image activity: no CPU attribution or portable speedup claim.

A small standalone file-operations confirmation isolates recursive Directory.Delete
at about1.40–1.45ms/reset, creation about0.016ms and writing about0.044–0.047ms.
Changing File.WriteAllBytes to FileStream does not correct it. Original and split
measurements remain [fileops](fileops.json)/[split](fileops-split.json), with traces.
No fallocate/fsync/fdatasync was observed in the two-operation trace. The exact
runtime/filesystem cause of deletion latency remains unexplained; investigating it
further is not required for command timing. The first owned service wrote its
JSON to journald rather than redirected stdout; its existing completed output was
retrieved, not restarted. All original conditions are retained.

Decision: protocol-v2 comparison clocks exclude fixture reset/staging symmetrically
but continue executing it for every invocation. Actual guest file I/O, host checks,
compilation when selected, full output verification and prior-result cleanup remain
included. Wall deadlines continue covering all staging/checking/cleanup. Reject
mixed v1/v2 workers. Retain all v1 absolute observations as historical included-
staging measurements; the corrected clock is not a runtime optimization. Refresh
representative current ratios once after focused worker checks, then return to
source-backed whole-script redesigns. No replacement benchmark framework.
