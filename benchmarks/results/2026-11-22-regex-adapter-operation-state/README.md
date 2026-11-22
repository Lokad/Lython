# B1416 Pass adapter operation state directly

The optional PythonRe 0.4.0 adapter uses static handlers to enter its
existing error boundary for compilation, direct matching, cursor creation
and cursor advance. This removes captured delegates and their closures.
Direct work receives the current caller token. Cursor work continues to
forward the current token through its registration to the backend cursor.
Pattern text is still read lazily on error.

The Windows .NET 11 RC3 comparison on November 22, 2026 UTC uses two
32-file images with the same core, producer and worker. The adapter DLL
is the sole difference, from `da388dc30` to `9e063baa9`. Each sample
checks expected result and file output, cleans up the result and code
owner, and verifies shared allowance reuse. Both images pass all five
CPython 3.13.2 goldens. No elapsed comparison was collected.

| Complete script | Median allocated bytes before | After | Reduction | Admitted execution peak |
| --- | ---: | ---: | ---: | ---: |
| 3,072 Unicode rows with named captures | 3,436,776 | 3,264,600 | 5.01% | 2,089,005 |
| 3,072 ASCII rows with named captures | 3,128,080 | 2,955,904 | 5.50% | 2,075,085 |
| Rewrite 96 file lines using named groups | 966,200 | 936,248 | 3.10% | 50,434 |
| Compile 64 named rules | 388,176 | 382,032 | 1.58% | 139,956 |
| Positional call control | 1,060,272 | 1,060,272 | 0.00% | 1,284 |

Windows .NET 11 and Ubuntu .NET 10 each pass 714 focused Release
checks, including adapter cancellation during backend work, cursor
unwind and bounded original-subject windows. The initial local image
check rejected differing core build hashes. The recorded allocation
comparison uses the same verified core binary for both images after
checking that the reference and candidate core sources agree.

[Images and revisions](source.json) · [Allocation and admission](local-allocation.json) ·
[Checks and procedures](procedures.tar.xz).
