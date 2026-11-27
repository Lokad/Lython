# B1894 Apply the outer iterator protocol on first resume

Generator expressions validate and acquire the outer iterator at construction.
On first resume the ordinary `GetIter` instruction applies the protocol to that
iterator again. It can give the loop a different iterator while `.0` still
exposes the original object. Native iterators enter the loop directly. Closing
or throwing into an unstarted generator leaves the deferred protocol uncalled.

Both CPython 3.13.2 and 3.13.16 agree on the replacement iterator and the
`RuntimeError` caused by `StopIteration` from resumed acquisition. Focused tests
also exercise a delayed host read in the second `__iter__`, successful resumption,
and in-flight cancellation with the file closed and the budget back at baseline.
Windows .NET 11 passes 527 focused Release checks and 72 Debug checks including
the native prologue assertions. Ubuntu WSL .NET 10 passes 215 public and 60
white-box Release checks.

## Native workload control

Reference `af9558316` and the candidate with the archived patch differ only in
the core DLL in their verified 32-file images. Both verify the four existing
CPython 3.13.16 report goldens. The 2,048-row CSV reduction retains B1893's
allocation and admission. It allocates 4,135,086 B in `Run` and 4,135,302 B in
`RunAsync`, at a 5,940,679 B admitted peak for each image and API. All other
admission peaks match and every measured invocation returns to baseline.

Ubuntu WSL ran on one logical CPU with .NET 11 RC3 on November 27. Two workers
per image warmed for at least five seconds and provided seven rotated batches.
Ranges compare every pairing of worker medians. Warm elapsed includes mediated
RAM-backed I/O, output and file verification, and prior-result cleanup. Process
CPU also includes fixture reset and request framing. Compilation and startup
are outside both clocks.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Per-row generator reduction | `Run` | −2.20% to +1.99% | 0.00% to +1.59% |
| Per-row generator reduction | `RunAsync` | −0.20% to +0.11% | +0.21% to +1.77% |
| File-fed map control | `RunAsync` | −2.31% to +0.95% | +1.59% to +5.22% |

All 84 timed batches verify. The file-fed CPU control moves more than either
affected lane. These data are diagnostic. B1893's sync CPU result remains open
for a quiet checkpoint.

[Timing](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Image identity](identity.json) · [Observations](observations.tar.xz) ·
[Exact source and verification](procedures.tar.xz).
