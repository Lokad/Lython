# B1864 Collect iterable rows directly into a call packet

The candidate replaces the separate sync and async argument staging for
arbitrary `starmap` rows with the existing admitted call accumulator.
Ordinary callbacks bind from its live prefix. Callables that require an
owned array can still receive one. Generator batching fills spare admitted
capacity through the existing yield path. Exact lists, tuples and direct
zip rows continue to use their existing dispatch.

The reference is the B1862 runtime at `d887f04c5`. The candidate is
`b6268d329` plus the archived source patch. Only the core DLL differs
between the complete 32-file images. The generated-row workload parses
2,048 shipment records from the same 27,350-byte CSV file as the controls.
Each record yields four arguments to a policy callback and contributes to
the result. Both images match all three independent CPython goldens,
including the generated-row output `28477` and the unchanged file digest.

## Allocation and admission

Windows .NET 11 Release, CLR bytes per warm invocation. The generated-row
report saves 147,400 B sync and 147,512 B async, both about 4.96%.
The two controls differ by at most 56 B per API. The report's peak
shared admission rises from 4,304,176 B to 4,304,400 B. Compilation
funding matches and every measured invocation restores its allowance.

## Initial timing

Ubuntu WSL and .NET 11 RC3, with all workers on one logical CPU.
Two workers per image warm for at least five seconds each and supply
seven rotated batches. Ranges span all pairings of worker medians.
Warm execution includes applicable RAM-backed I/O, verification and
previous-result disposal. Process CPU also includes request framing and
fixture reset. Compilation and startup are separate.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Iterable-row shipment | Sync | +1.14% to +41.82% | +4.63% to +34.59% |
| Exact-row shipment control | Sync | −13.93% to +18.63% | −24.20% to −0.83% |
| Iterable-row shipment | Async | −14.66% to −11.68% | −14.63% to −8.51% |
| File-fed map control | Async | −4.34% to +7.60% | −6.27% to +7.20% |

All 112 batches verify. The affected sync workers vary by up to 26.23%
in elapsed time and 16.71% in CPU. The unchanged exact-row control also
moves. The local timings warrant a quiet confirmation using the original
images and reversing the lane order.

Both Windows Release targets pass 495 public and 467 white-box focused
checks, covering ordered iteration, genuine suspension, retained arguments,
refusal and cleanup. The same checks pass on Ubuntu WSL .NET 10.

## Quiet confirmation

The same images run in reverse lane order on Azure F4as_v7 with Ubuntu
24.04.4 and .NET 11 RC3. The time boundaries, worker counts and sample
schedule stay the same. The local catalog independently verifies on the
VM against stock CPython 3.13.16.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| File-fed map control | Async | −2.92% to +0.93% | −1.97% to +4.43% |
| Iterable-row shipment | Async | −6.94% to −3.81% | −6.25% to −2.73% |
| Exact-row shipment control | Sync | −4.26% to −0.52% | −3.49% to +0.92% |
| Iterable-row shipment | Sync | −7.70% to −4.62% | −8.66% to −4.27% |

Retain the direct argument buffer. The quiet trial finds lower CPU in
both affected lanes and reverses the initial adverse sync timing. The
exact-row control also uses less elapsed time with CPU spanning zero.
The file-fed control spans zero in both measures. The benchmarks compare
Lython images. CPython supplies the output and file goldens.

Both quiet gates and all 112 batches pass in 163.55 seconds. Workers
join, the temporary guest files are removed, and the archive verifies
under the lease. Staging frees 5.84 MB from two remote names for the
same retired worker replica after verifying its complete frozen local
image and both earlier archives. [Storage record](quiet/vm-storage.json).

[Local timing](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Image identity](identity.json) · [Observations](observations.tar.xz) ·
[Checks and source patch](procedures.tar.xz) · [Quiet timing](quiet/summary.json) ·
[Quiet observations](quiet/observations.tar.xz) ·
[Quiet verification](quiet/terminal-status.json).
