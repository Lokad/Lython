# B1842 Publish CSV dictionaries directly from iterable input

List-backed and iterator-backed `DictReader` calls use the existing parser
publication path for matching private inferred headers. One preflight covers
the field values, final array and dictionary shell. The parser adopts the
array directly. Eligible rows skip the raw row handoff, the second preflight
and its two header checks. The existing live-header path handles replaced or
exposed headers and mismatched column counts. The parser revalidates the
inferred layout after admission and returns a raw row if the header changed.

The source rejects a nested pull during parser work and cleanup. The new
regression exercises this with an admission callback. Both baseline cases
fail with an exhausted outer iterator after the nested pull. Both pass with
the change and check that rows already returned retain their original keys
and values. Another case suspends on two iterator pulls in one quoted row,
then verifies the shared keys, decoded value, physical line count and owner
cleanup.

Windows .NET 11 and Ubuntu WSL .NET 10 each pass 423 public and 167 white-box
Release checks. Both worker images match independent CPython 3.13.2 output
and file goldens across four complete scripts. Only the core DLL differs
from `6bed7e6c0`. The list, generator and buffered summary cases read the
same 4,096 records and 57,361 UTF-8 bytes. The quoted invoice control reads
4,096 records and 174,147 bytes. These counts come from the actual inputs.

## Allocation and admission

Median Windows Release .NET 11 CLR allocation differs by at most 174 B per
complete invocation. Peak shared admission matches in both APIs for every
case. All invocations restore the allowance to its compilation baseline.

| Workload | Peak shared bytes in both APIs |
| --- | ---: |
| List-backed summary | 4,811,051 |
| Generator-backed summary | 4,402,371 |
| Buffered summary control | 3,671,803 |
| Quoted invoice control | 9,040,266 |

## Local warm timing

Two workers per frozen image run on one logical CPU in local Ubuntu WSL
.NET 11. Each warms for at least five seconds and supplies seven rotated
batches. The ranges span all pairings of worker medians. Warm elapsed covers
mediated I/O, output and file verification, and prior-result disposal. Process
CPU also covers fixture reset and worker framing. Both exclude startup and
compilation. All 112 timed batches verify.

| Workload | Lane | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Buffered summary control | Warm async | +0.86% to +3.91% | −1.98% to +1.82% |
| List-backed summary | Warm sync | −7.69% to −6.82% | −9.49% to −6.58% |
| Quoted invoice control | Warm sync | −3.70% to +3.92% | −1.40% to +5.45% |
| Generator-backed summary | Warm async | −6.86% to −3.19% | −6.13% to −5.56% |

Keep the direct publication and the reentry correction. Both affected scripts
use less elapsed time and CPU in every local comparison. The buffered control
takes more elapsed time with mixed CPU. The quoted control's reference CPU
medians differ by 6.94%. Preserve those control results for the next quiet CSV
checkpoint. CPython supplies the semantic goldens for this comparison.

[Allocation and admission](allocation-summary.json) ·
[Fixture counts](fixture-census.json) · [Image identities](identity.json) ·
[Timing summary](summary.json) · [Raw observations](observations.tar.xz) ·
[Archive verification](archive-receipt.json) · [Patch and checks](procedures.tar.xz).
