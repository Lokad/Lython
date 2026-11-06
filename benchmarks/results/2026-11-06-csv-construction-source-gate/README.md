# Current CSV construction source gate — 2026-11-06

Source **dd76df36**, core **b16fc0d7**, unchanged comparison worker **b94fa98c**;
quiet Azure F4as_v7 Ubuntu24.04.4/.NET11RC3. This is a source decision and a
diagnostic CPU capture, **not an elapsed/allocation improvement or CPython ratio**.
[Declared contract](source-contract.md), [summary](summary.json),
[raw capture, scripts, identities and source snapshot](observations.tgz).

The static gate inspects existing compiled code without production instrumentation.
Primary text/JSON/report helpers already have native-return bodies; ordinary CSV's
`accumulate` has one stable9-slot frame. Exact-capacity workspace churn therefore
does not explain these complete workloads. No pool/cache change was implemented.

The complete4096-record CSV job ran1536times after6guest-second warmup, with
mediated RAM-file I/O, full output/file validation and prior-result cleanup.
Compilation, staging/reset, transport and final teardown are outside the captured
execution boundary. Normal GC/tiering; PerfMapEnabled is the sole diagnostic
runtime override. The owned75-second supervisor completed successfully; quiet
endpoints, verified frozen bits, first/last goldens, Closed/final funding, joined
worker/profiler and removed fixtures all passed. The initial inline SSH launcher
failed on quoting before any guest work; invoking its unchanged uploaded file
succeeded. That failed condition is retained in the summary.

1,540 actual guest-thread samples over7.75CPU seconds contain10.06%unknown and
one partial perf-map line. Initial and final maps remain in the archive. Leading
self samples include instruction prefix187, dictionary insertion56, record
finalization49, parser feed47, DictReader37, binding28 and frame creation16.
These include necessary/inlined work; they do not establish removable fractions
or attribute all write barriers/TLS/unknown samples to frame construction.

Decision: do not revive saved-call-control/arena designs or change idle capacity
matching on this evidence. A remaining concrete bulk boundary is DictReader's
complete row array followed by final dictionary construction and row release.
Before selecting direct materialization, account for EOF finalization before
source disposal, mutable/duplicate/non-string field names, source reentry,
QUOTE_NONNUMERIC field-error ordering, private scratch, retained fields and true
asynchronous cleanup. Earlier CSV results and adverse controls remain separate.
General lookup/operand transport also remains measurable work; no single cause
or new production redesign is claimed by this investigation.
