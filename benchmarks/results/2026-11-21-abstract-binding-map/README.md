# B1269 unified abstract binding map

Private `0db9a55a9` versus measured `837fae29b`, whose production
source is retained at `d09acaf69`. A single map holds independent
value and sequence-length facts. Branch cloning and joining use one
map, and diagnostic provenance follows the left branch.

## Measurements

Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3 on November 21, 2026 UTC.
Two processes per source, each with at least 16 seconds of warmed
requests and seven rotated batches. Compilation includes disposal of
the code owner. Time to result adds one execution, mediated RAM I/O,
golden checks and result disposal. CPU includes reset and requests.
Process startup and final teardown sit outside guest clocks.

| Script and phase | Elapsed change | Process CPU change |
| --- | ---: | ---: |
| Generated validator, compilation | −1.15 to +0.31% | −0.54 to +0.54% |
| Generated validator, compile to result | −1.86 to −0.96% | −2.03 to +0.23% |
| Inherited dataclass configuration, compilation | −1.88 to −0.27% | −1.98 to +0.50% |

Ranges cover the four process-median pairings. Positive percentages
indicate slower runs. The whole-result validator and dataclass
elapsed improve slightly, with mixed CPU. Validator compilation
elapsed and CPU span zero. Local validator compile allocation
falls 0.62%, dataclass and postponed factory allocation rise 0.11%
and 0.12%. Warm allocation and accounted peaks match.

A local compile-only diagnostic stresses 256 retained scalar bindings
and 48 conditional joins. Allocation grows from 3,339,582 B to
4,725,828 B per compile, a 41.51% increase. The wider entry carries
both facts even for code that holds only scalars. The diagnostic
captures this layout cost and does not provide a timing estimate.

**Keep the prototype private.** The saved map and join traversal do
little for normal whole scripts here. The scalar-heavy allocation
increase outweighs the observed benefits for this representation.
A new layout must keep ordinary scalar entries compact and preserve
independent binding presence.

## Validation

Windows .NET 11 and Ubuntu .NET 10 each pass 21 white-box and 827
public Release checks. The additional fact-presence regression
exercises length-only joins and disjoint value and length facts.
Two 32-file frozen images agree on three CPython output and file
goldens, 253 diagnostic probes, and the final code and source spans
of 57 code objects. Compilation and owned execution return the
shared allowance to baseline.

All 84 timed batches and machine gates pass in 275 seconds. Worker
joins and fixture removal are verified. The persistent archive passes
its hash check under the lease. The four aliases of the prior
reference are retired after verifying the committed core image,
all alias paths and each archive.

[Quiet measurements](summary.json) · [Allocation and semantics](local-summary.json)
· [Raw observations](observations.tgz) · [Source and checks](checks-and-procedures.tar.xz).
