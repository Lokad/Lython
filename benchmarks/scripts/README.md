# Complete representative scripts

These ordinary Python jobs cover CSV conversion/filtering/aggregation, Unicode
text counting, newline-delimited JSON aggregation, and mediated directory/file
copying with a deterministic manifest. They publish complete output files.
Text reports explicitly use LF on Windows and Linux. Each run needs fresh input
files and a writable working directory; the file job creates its output directory.

`RepresentativeScriptTests` supplies independent fixtures and checks exact file
bytes at 0, 16 and 1,024 repetitions, through synchronous and genuinely delayed
asynchronous hosts under a 256 KiB execution cap. Cancellation controls interrupt
real host reads. Publication controls pause each complete job's report write,
cancel it, and verify that the previous file bytes remain intact. The file job's
earlier successful binary copies remain published: atomic replacement is per
host operation, rather than a transaction over the whole job.
These tests complement retained-data, alias and preflight gates;
they do not qualify embedding latency or comparative performance.

Alias controls retain one CSV note, JSON kind or Unicode line across the complete
scan and source closure, collect before projection, and compare independent
report bytes. Retaining all emitted values rejects at the same 256 KiB cap.
White-box CSV/JSON success/error controls check acquisition/parser reservations
and funded escaped storage after abandonment. A CSV body-conversion error leaves
the reader resumable and its scratch funded until EOF or collection; this evidence
does not establish GC-independent reclamation. Caller-retained readers may resume.

Check the same source and fixtures with an installed CPython:

```powershell
python tools/verify-representative-scripts.py --output-dir <new-receipt-directory>
```

The driver preserves inputs, outputs, source/fixture/output hashes and Python
identity. It normalizes standard-output text line endings while comparing all
published file bytes exactly. Its per-child deadline is 10 seconds. This is a
correctness check. The comparison harness stages identical file fixtures and
validates complete output bytes in both workers; its measured boundaries and
remaining timing qualification are described in [COMPARISON.md](../COMPARISON.md).
