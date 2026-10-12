# Complete representative scripts

These ordinary Python jobs cover CSV conversion/filtering/aggregation, Unicode
text counting, newline-delimited JSON aggregation, and mediated directory/file
copying with a deterministic manifest. They publish complete output files.
Text reports explicitly use LF on Windows and Linux. Each run needs fresh input
files and a writable working directory; the file job creates its output directory.

`RepresentativeScriptTests` supplies independent fixtures and checks exact file
bytes at 0, 16 and 1,024 repetitions, through synchronous and genuinely delayed
asynchronous hosts under a 256 KiB execution cap. Cancellation controls interrupt
real host reads. These tests complement retained-data, alias and preflight gates;
they do not qualify embedding latency or comparative performance.

Check the same source and fixtures with an installed CPython:

```powershell
python tools/verify-representative-scripts.py --output-dir <new-receipt-directory>
```

The driver preserves inputs, outputs, source/fixture/output hashes and Python
identity. It normalizes standard-output text line endings while comparing all
published file bytes exactly. Its per-child deadline is 10 seconds. This is a
correctness check; the existing comparison harness still needs file fixture
staging and output validation before these jobs become fair timed lanes.
