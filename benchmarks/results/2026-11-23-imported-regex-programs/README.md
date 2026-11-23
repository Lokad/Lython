# B1496 Reuse prepared regex programs in local imports

`63549c384` keeps provider-compiled programs from valid local modules in
the importing execution's existing regex cleanup. The runtime can borrow
an analyzed program on a wrapper-cache miss from that module, including
through an escaped function. The program outlives cursors and subjects.
Invalid or refused preparation closes unpublished programs. The guest
wrapper cache, requested flags and purge behavior remain independent of
the borrowed handles.

Compared with production `f39341953` on November 23, 2026 UTC on a local
Windows x64 i7-14700KF with .NET 11 RC3 and the pinned PythonRe 0.4.0
adapter. Each 35-file image verifies before launch and differs only in
its core DLL. An isolated CPython 3.13.2 run supplies the goldens.
The diagnostic compares Lython revisions only.

Both scripts read 96 Unicode records and write the same report through
the existing RAM-backed benchmark host. The regex variant imports a
module with 16 literal rules and named captures. Both scripts import a
fresh helper each run. The warm lane reuses the entry compilation.
Compile to result adds entry compilation and compiled-owner disposal.
Both include applicable I/O, byte-for-byte output verification, result
disposal and an allowance-baseline check. Startup and final teardown
are outside the sample. Two processes per revision warm for at least
two seconds and supply seven batches of 96 invocations. Ranges pair
the independent process medians.

| Workload | Boundary | Elapsed change | Process CPU change | CLR allocation per invocation |
| --- | --- | ---: | ---: | ---: |
| Imported regex rules | Warm | −47.87 to −3.39% | −35.78 to −1.23% | 7.25 → 4.13 MB |
| Imported regex rules | Compile to result | −47.78 to −33.46% | −49.21 to −34.15% | 7.28 → 4.16 MB |
| Imported text control | Warm | −28.84 to +5.26% | −33.33 to +20.00% | +784 B |

Keep the source change for the eliminated duplicate compilation. The
affected script allocates roughly 43% less with the same admitted peak.
Control allocation rises 784 B, consistent with one extra reference in
each execution context. Control admission matches. The warm regex worker
spread and moving text control leave elapsed and CPU magnitudes uncertain.
All 8,064 timed invocations match the CPython goldens and refund to baseline.

Windows .NET 11 and Ubuntu .NET 10 each pass 108 white-box and 847 public
focused Release checks. Cooperating-provider cases prove one compile per
import even across purge and a failed module body, a fresh owner on the
next run, and cancellation that drains the cursor before program refund.
Rejected analysis and terminal refusal dispose unpublished handles. The
first local launcher attempt omitted managed reference entries in its
dependency manifest and stopped before timing. The corrected build includes
the pinned dependencies and verifies both full images.

[Identity](identity.json) · [Summary](summary.json) · [Raw observations](observations.json) ·
[Procedures and fixtures](procedures.tar.xz) · [Exact reference core](../2026-11-23-forwarded-global-call/README.md).
