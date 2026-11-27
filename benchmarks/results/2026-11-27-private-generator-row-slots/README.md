# B1910 Preserve native values across an admitted generator row

A generator can already fill a compatible `starmap` callback's admitted
workspace. The candidate transfers its retired yield slots directly and
returns the slot from a ready callback to the iterator. Guest-visible
aliases retain their existing object identity. An incomplete invocation
uses the ordinary framed path and keeps its workspace charged. Short and
excess rows retain the ordinary binder and ordered evaluation.

Against `203eddeb3`, the 2,048-row iterable shipment allocates 131,128
fewer CLR bytes per execution in both APIs, about 5.86%. Compiled
admission and peak execution admission match. The bound and file-fed
controls match their CLR allocation and admitted peaks. Every allocation
witness returns the borrowed allowance to baseline.

## First matched comparison

Only the core DLL differs between the two verified 32-file images.
Both images match three independent CPython 3.13.16 output and file
goldens. Local Ubuntu WSL timing on November 27, 2026 UTC uses two
workers per image on CPU 27 with .NET 11 RC3. Each warms for at least
five seconds and contributes seven rotated batches. Ranges span
pairings of worker medians. Warm elapsed includes mediated I/O, golden
checks and previous-result cleanup. Process CPU includes fixture
reset and framing.

| Workload | Mode | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Iterable shipment | Run | −5.54% to +4.12% | −8.96% to −0.74% |
| Bound shipment control | RunAsync | +1.08% to +1.91% | +0.43% to +3.69% |
| File-fed map control | RunAsync | −1.47% to +1.02% | −2.74% to −0.04% |
| Iterable shipment | RunAsync | −3.68% to +24.25% | −3.39% to +12.50% |

All 112 timed batches pass. The sync iterable reference CPU spread is
8.33%. The async iterable reference CPU spread is 11.12%. The adverse
bound result and wide async spread prompted one repeat with the same
images and reversed worker placement.

## Same-image repeat

Both images again match the same three independent goldens. The repeat
runs the async lanes in reverse placement and verifies all 84 batches.
The iterable CPU spread is below 1.5% in both images. File-fed candidate
CPU spread is 5.87%.

| Workload | Mode | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Iterable shipment | RunAsync | −6.29% to −2.98% | −5.76% to −3.33% |
| File-fed map control | RunAsync | −2.68% to +5.33% | −1.56% to +5.53% |
| Bound shipment control | RunAsync | −0.37% to +3.03% | −1.16% to +5.07% |

Retain the change for the removed object publication and the repeat's
async iterable result. The first adverse bound result did not reproduce
across all pairings. Keep the bound and file-fed variation visible.

The public scalar alias regression gives the same output on CPython and
both Lython APIs. Focused Release coverage also exercises defaults,
arity, real suspended reads, cancellation, refusal and allowance reuse.
Windows .NET 11 passes 579 focused checks. Windows .NET 10 and Ubuntu
WSL .NET 11 each pass 549.

[Image identity](identity.json) · [Allocation and admission](allocation-summary.json) ·
[Timing summary](summary.json) · [Raw observations](observations.tar.xz) ·
[Source patches and focused checks](procedures.tar.xz) ·
[Repeat timing](repeat/summary.json) · [Repeat observations](repeat/observations.tar.xz) ·
[Repeat procedures](repeat/procedures.tar.xz).
