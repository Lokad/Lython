# B1394 Group decoded regex replacement escapes

The quiet Azure F4as_v7 compared frozen `c097d73db` and `9edfe90f0`
on November 22, 2026 UTC with Ubuntu 24.04.4 and .NET 11 RC3.
The retained source and test patch is `5ac8ca3dd`. The reference,
catalog and PythonRe 0.4.0 adapter match B1393. Initial worker order
is reversed. [Original scalar-replay comparison](../2026-11-22-regex-template-source-ranges/README.md).

Literal spans come from the funded immutable UTF-8 template. One
private buffer holds only decoded escapes, and adjacent escapes
replay as a run. The existing plan reservation funds its metadata
and its largest temporary name lookup. Lookup funding returns before
replay. The result owns its backing independently.

| Complete script, warm | Elapsed change | Wider CPU change | Worker spread reference / candidate |
| --- | ---: | ---: | ---: |
| Rewrite 96 file lines using named groups | −0.69 to +1.14% | +0.30 to +0.58% | 1.72 / 0.13% |
| Expand a 12 KiB literal template 48 times | −22.23 to −21.47% | −23.21 to −21.02% | 0.28 / 0.70% |
| Expand an escape-dense template 48 times | −0.88 to +1.50% | −0.65 to +0.95% | 1.56 / 0.83% |
| Expand plain literal 48 times | −0.01 to +0.66% | −1.06 to +0.79% | 0.59 / 0.08% |
| Positional call control | +0.56 to +6.15% | −0.03 to +4.46% | 0.77 / 4.75% |

Ranges span all pairings of two process medians per source after
eight seconds of warmup and seven rotated batches per process.
Warm clocks include applicable mediated RAM-backed I/O, output
verification and prior-result disposal. Wider CPU includes
reset and protocol. Startup and compilation are outside these clocks.

Retain the refinement. Long expansion uses 45.08% less local
allocation, and its admitted peak falls from 878,561 to 829,387.
The dense candidate now allocates 4.76% less with admitted peak
49,814 to 47,926 bytes. Dense elapsed and CPU span zero after the
initial 13.43–26.83% elapsed regression. File rewrite allocation
falls 1.08%, with a small adverse wider CPU reading. The unchanged
positional control is slower in this run, with 4.75% candidate worker
spread. Keep that adverse result open and repeat the control at the
next relevant call checkpoint. B1393's positional diagnostic spans zero.

Windows .NET 11 and Ubuntu .NET 10 each pass 796 focused Release
checks, including in-flight backend cancellation and terminal
admission refusal. Both frozen images match the five CPython goldens
and return their allowances. All 140 timed batches verify, both quiet
gates pass, workers join and the archive verifies under the lease.

[Timings](summary.json) · [Local allocation](allocation-summary.json) ·
[Source and images](source.json) · [Raw observations](observations.tgz) ·
[Tests and procedures](procedures.tar.xz) · [Archive receipt](archive-receipt.json).
