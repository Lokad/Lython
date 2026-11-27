# B1886 Attribute current log search

The frozen B1885 image contains `4ab65dcc6`, .NET 11 RC3 and the signed
PythonRe 0.5.0 adapter. It ran on Ubuntu WSL pinned to one logical CPU.
The same two 3,072-line log fixtures and independent CPython goldens
verify. Three six-second EventPipe captures follow four seconds of
verified warmup per worker.

| Workload | API | Verified commands during collection | Core¹ | Adapter¹ | Producer¹ | Single matching² |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| Compiled-pattern search | `Run` | 2,304 | 63.7% | 9.6% | 26.7% | 38.3% |
| Module search | `Run` | 448 | 66.2% | 7.6% | 26.2% | 37.3% |
| Module search | `RunAsync` | 896 | 68.0% | 7.3% | 24.7% | 35.1% |

¹ Shares by nearest resolved Lython or Utf8Regex frame after filtering the
listed waits and GC-poll stacks. These are managed thread-time samples.
² Inclusive span containing a recognizable one-shot matching frame. Inlined
frames can hide some work. Regions overlap, and shares do not predict a
whole-command CPU or elapsed saving.

The core instruction driver is the largest named runtime frame. Module
search also has about 5% attributed to the general call path, covering
more work than argument binding. Source inspection confirms that the
eight module pattern operations reserve and construct a bound array for
ordinary executable calls. Omission and explicit Python None remain
distinct. Test removing this array with the existing fixed-slot binder
and snapshot only the arguments a pending pattern lookup needs.

The 57 batches verify and the workers and trace processes join. The
archive contains the full stacks and original operation receipts.

[Attribution](attribution.json) · [Image identity](identity.json) ·
[Run summary](run-summary.json) · [Observations](observations.tar.xz) ·
[B1885 comparison](../2026-11-27-wsl-log-cpython/README.md).
