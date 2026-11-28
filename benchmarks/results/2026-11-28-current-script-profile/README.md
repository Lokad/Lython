# B1915 Attribute current shipment, JSON and compiled regex workloads

The November 28 local Ubuntu WSL captures use the exact `24218276c`
.NET 11 RC3 worker image from B1914, pinned to one logical CPU. All 32 files
match the image manifest. Stock CPython 3.13.16 output and file goldens
verify before collection. Each capture follows four seconds of verified
warmup and uses a six-second EventPipe sampled thread-time window.

| Workload | Verified invocations during collection | Leading regions |
| --- | ---: | --- |
| Iterable shipment, `RunAsync` | 2,048 | Generator advancement 67.8%, generator callback workspace 46.4%, text file 12.0% |
| Dense JSON document, `Run` | 2,944 | Converted dictionary update 34.8%, JSON construction 19.9%, text file 13.2% |
| Compiled regex log search, `Run` | 3,328 | Adapter 38.0%, producer 34.5%, text file 5.3% |

Shares use resolved Lython or Utf8Regex ancestry and exclude the recorded
wait and GC-poll frames. Regions overlap. Inlining can hide methods, and
generator advancement includes guest work. The adapter region includes
the producer. The original classification and its corrected wait filter
are preserved with the traces.

In the shipment trace, the nearest runtime frames for the generator-row
continuation and generator advancement state machines are 3.7% and 3.0%
of the eligible time. Those frames include the work done directly in each
method. In the dense JSON trace, the converted update already writes an
unboxed sum into the existing dictionary entry. Its inclusive share
covers operand checks, field lookup and conversion. These observations
support inspecting the update and row execution boundaries before
another layout or allocation change.

All 130 sampled batches verify, and each worker and trace process joins.

[Attribution](attribution.json) · [Image identity](identity.json) ·
[Run summary](run-summary.json) · [Traces and procedures](observations.tar.xz).
