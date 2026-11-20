# B1231 and B1232 JSON property names from validated layouts

The decoder already shares immutable key names between repeated JSON objects.
When a strict decode establishes a layout from unescaped names, the new decoder
can consume each expected name with a bounded UTF-8 comparison and a closing
quote check. A hit replaces token classification, UTF-8 revalidation and a
second lookup. Misses run the ordinary decoder. The existing key layout retains
its owner, and proof stays attached to the layout through nested and suspended
parsing. No extra key cache or memory allowance is needed.

| Warm workload | Input | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Named shipment report | 3,072 three-field objects | −7.51% to −2.15% | −6.84% to −1.70% |
| Wide shipment report | 3,072 24-field objects | −6.00% to −5.81% | −6.14% to −5.08% |
| Alternating shipment report | 3,072 alternating wide and narrow objects | −2.11% to +0.38% | −3.63% to 0.00% |
| Invoice control, original | 5,120 CSV data rows | +0.32% to +5.26% | +0.85% to +6.25% |
| Invoice control, exact-image repeat | Same CSV | −1.48% to +0.59% | −0.84% to +2.47% |

The named report and the wide report improve on both clocks. The alternating
report is inconclusive. The invoice's initial same-direction adverse result
does not recur in the repeat. Candidate invoice elapsed spread falls from
3.88% to 1.02%. The repeat still has mixed CPU. Retain the JSON change and both
invoice observations. All four cases restore the borrowed allowance, accounted
peaks agree exactly and managed allocation changes by less than 0.02%.

Reference core `eaf4d65c1` matches the delivered production sources at
`46ccc2efa`. The candidate core comes from `11de545f6`. Both frozen 32-file
images use the same worker, adapter and fixture catalog. Ranges compare all four
pairings of two independently warmed workers per image. Seven rotated samples
target 0.8 seconds per worker, after at least 12 seconds of interleaved warmup.
The initial four cases took 304.31 seconds on the Azure F4as_v7, Ubuntu 24.04.4,
.NET 11 RC3, on 2026-11-20 UTC. The invoice-only repeat took 80.85 seconds.
Warm includes mediated RAM I/O, goldens and previous-result cleanup. Compilation,
process startup, fixture reset and final cached teardown sit outside its clock.
Process CPU covers the wider request. CPython 3.13.2 verified fixture goldens
locally. This is a Lython source comparison with no new CPython timing.

Windows .NET 11 and Ubuntu .NET 10 each passed 232 white-box and 899 public
Release checks. Six added cases independently match CPython, covering duplicate
keys, escaped quotes and backslashes, empty and non-BMP names, strictness after
permissive decoding and exact error coordinates. Existing checks cover live
callbacks, cancellation, refusal and funded retained keys. Both workers verify
all four complete stdout and file goldens.

The initial collection completed all 112 timed batches and passed both quiet
gates. Its archive assertion still expected three jobs. Workers had joined and
the raw samples were already persistent. The four-job archive was recovered and
hash-verified under the VM lease without retiming. The original failed service
status, assertion, samples and recovery evidence are included. All 28 repeat
batches, quiet gates, worker joins and archive checks completed normally.

[Initial comparison](summary.json) · [Invoice confirmation](invoice-confirmation/summary.json) ·
[Initial observations](observations.tgz) · [Checks and procedures](checks-and-procedures.tar.xz).
