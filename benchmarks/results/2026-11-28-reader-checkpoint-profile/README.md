# B1922 Profile the reader checkpoint

Three November 28, 2026 captures use the exact `bfde55b6b` 32-file image
from B1921 and .NET 11 RC3, pinned to one logical CPU in Ubuntu WSL.
Independent CPython 3.13.16 output and file goldens verify on both scripts.
Each capture follows four seconds of verified warmup and records six seconds
of EventPipe sampled thread time. All 158 sampled batches verify.

| Workload | Verified invocations | Inclusive managed regions |
| --- | ---: | --- |
| Iterable-row shipment, `RunAsync` | 2,368 | Generator advancement 73.9%, outer row handoff 51.9%, text file 17.3% |
| Iterable-row shipment, `Run` | 2,176 | Generator advancement 76.7%, generator callback path 34.3%, text file 14.3% |
| Dense JSON summary, `RunAsync` | 5,568 | JSON construction 47.5%, converted dictionary update 18.0%, text file 13.8% |

Shares are of sampled time with resolved Lython ancestry after removing
known wait frames and leaves labelled `UNMANAGED_CODE_TIME`. Regions overlap
and include their callees. Inlining can hide a region. The original
wait-filtered view assigns 7.1% and 10.4% of the two async captures to the
nearest managed frame in the result relay. Over 98% of each relay assignment
ends in unclassified native time. The managed view therefore guides selection
of interpreter work for the next change.

Within the async shipment, the generator advance and outer pull continuation
are the nearest Lython frames for 4.6% and 2.9% respectively. Both values
include work performed inside the methods. Within async dense JSON, the
construction loop and byte-builder growth are nearest for 17.0% and 8.2%.
Builder growth is shared across callers. Use a concrete call site and its
ownership costs before choosing a transfer or allocation design.

[Managed attribution](managed-attribution.json) · [Original attribution](attribution.json) ·
[Image identity](identity.json) · [Run summary](run-summary.json) ·
[Raw traces and procedures](observations.tar.xz).
