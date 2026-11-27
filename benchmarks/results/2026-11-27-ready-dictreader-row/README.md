# B1846 Complete parser-published CSV rows directly

The candidate returns a ready dictionary or exhaustion before entering the
general `DictReader` completion state machine in either API. A cold header
or guest-assigned line number uses the existing Python protocols. One general
completion path handles raw rows, blanks and genuinely pending reads. It
preserves line counts and releases raw row capacity only after adoption or
cleanup. [B1845](../2026-11-27-post-cpython-shipment-cpu/README.md) identified
the activation in the current file-fed map path. The older
[B1484](../2026-11-23-dictreader-activation/README.md) tried a broader split.

The source patch changes only the CSV object implementation. Both Windows
Release targets pass 425 public and 167 white-box focused checks. The new
test covers a ready inferred-header row, a blank followed by a real suspended
pull, retained rows and cancellation with full refunds. Both frozen images
match five independent CPython goldens. Peak shared admission matches in both
APIs on the four CSV cases and the allowance returns to baseline. Median CLR
allocation differs by at most 230 B per complete invocation.

## Local comparison

Both .NET 11 images use the B1844 comparison host. Only the core differs
from `a1b3a3325`. Two workers per image run on a pinned logical CPU in
Ubuntu WSL. Each warms for five seconds and supplies seven rotated batches.
Ranges span all pairings of worker medians. Warm elapsed includes mediated
I/O, verification and previous-result disposal. Process CPU also includes
fixture reset and framing. Both exclude compilation and startup.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| File-fed map | Async | −8.16% to −0.58% | −9.02% to +0.80% |
| List-backed summary | Sync | −31.95% to +17.38% | −29.82% to +0.58% |
| Quoted invoice | Async | −2.03% to +8.97% | −2.60% to +3.85% |
| Wide shipment | Async | −14.34% to +3.66% | −17.51% to +2.22% |
| Nested JSON document, unchanged path | Sync | −1.79% to +13.55% | −8.79% to +18.45% |

All 140 batches verify. The candidate list summary's worker medians differ
by 50.88% in elapsed. The unchanged JSON control moves in both measures.
The file-fed elapsed direction warrants one quiet confirmation with the same
images. Local results leave CPU and the other elapsed effects unresolved.

## Quiet confirmation

The same images run on Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3 on
November 27, 2026 UTC. Positions rotate across seven batches after at least
five seconds of warmup per worker. Warm elapsed and process CPU have the same
boundaries as the local run. Stock CPython 3.13.16 independently verifies the
five scripts in each image before collection. These timings compare the two
Lython images.

| Workload | API | Elapsed change | Process CPU change | Same-image CPU spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| File-fed map | Async | −11.70% to −6.25% | −10.52% to −4.01% | 3.05% / 4.10% |
| List-backed summary | Sync | −4.57% to −2.17% | −4.26% to −2.27% | 0.64% / 1.43% |
| Nested JSON document, unchanged path | Sync | +0.22% to +1.44% | +2.46% to +3.68% | 1.14% / 0.05% |

All 84 batches and both quiet gates pass. The persistent staging reuses
all 32 reference files and 31 candidate files from B1844 and streams the
changed core and inputs. The collector verifies both images after the run,
joins the workers, removes the RAM-backed fixtures and verifies the archive.

Retain the ready prefix. Both directly affected workloads improve in both
quiet measures at matching admission. The unchanged JSON control uses more
CPU, with closely grouped workers in each image. The measurements leave its
cause open. Carry that result into the next delivery comparison. Wide
shipment and quoted invoice timing remain inconclusive in the local run.

[Timing summary](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Image identity](identity.json) · [Observations](observations.tar.xz) ·
[Archive verification](archive-receipt.json) · [Patch and checks](procedures.tar.xz) ·
[Quiet timing](quiet/summary.json) · [Quiet observations](quiet/observations.tar.xz) ·
[Quiet terminal status](quiet/terminal-status.json) · [Quiet checks](quiet/procedures.tar.xz).
