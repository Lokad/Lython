# B1205 Synchronous method-format traversal

Compares private candidate `eb14aa3ee` with the production source at
`fbb10c14b` on the quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3
on 2026-11-20 UTC. The reference image was built from source-equivalent
`32c5f2ef7`. Both complete images and the same five exact workload goldens
were verified. Each role had two independent processes with at least
eight seconds of warmup per process. Five rotated measurement rounds
targeted 0.4-second requests. The ranges cover all four pairings of process
medians. Negative changes favor the candidate.

| Warm workload | Elapsed change | Process CPU change | Duplicate elapsed spreads, reference / candidate |
| --- | ---: | ---: | ---: |
| Named shipment, 3,072 records | +0.12% to +1.52% | 0.00% to +3.05% | 0.31% / 1.08% |
| Positional shipment, 3,072 records | +1.47% to +1.64% | +0.12% to +0.98% | 0.08% / 0.10% |
| Report with two nested widths, 2,048 rows | −6.22% to −2.91% | −6.52% to −2.75% | 3.47% / 0.06% |
| Expanded forwarding, 1,024 records | −1.21% to +0.69% | −2.50% to −0.41% | 0.58% / 1.34% |
| CSV invoice control, 4,096 records | −1.80% to +0.14% | −2.44% to +1.69% | 1.04% / 0.93% |

Warm guest elapsed includes mediated RAM-backed I/O, golden checks and
prior-result cleanup. Compilation, fixture reset, process startup and
final teardown are outside guest elapsed. Process CPU covers each request
loop. Local allocation diagnostics differ by less than 0.01% for all five
scripts. Accounted peaks match and both images restore the borrowed
allowance after cleanup. CPython was used for semantic checks.

The prototype completes ordinary field traversal synchronously. When a
lookup or render operation has pending work, it retains the selected
field, next access and existing governed builders until completion.
It adds 110 production lines to retire the unconditional template
continuation on the ordinary path. Nested formatting improves on both
clocks despite the reference spread. Neither shipment improves. The
named effect is small relative to sample variation and CPU does not
support a gain. The positional result is consistently adverse with
closely matched controls. The result does not isolate the cause in either
shipment.

The renderer prototype stays out of production. The nested result
supports studying nested field traversal when an approach can also
remove code or benefit a representative script. Keep the original
[B1198 comparison](../2026-11-20-inline-format-continuation/README.md)
and [B1204 CPU attribution](../2026-11-20-current-factory-report-attribution/README.md)
separate from this source pair.

Windows .NET 11 and Ubuntu .NET 10 each passed 742 public and 1,008
white-box focused Release checks on the prototype. Five independent
CPython probes passed on both frozen images in both execution modes.
The new public test verifies an earlier escaping nested spec, resumed
selector evaluation, file cleanup and budget reuse on success and
in-flight cancellation. The production renderer, with the new test,
passed all 264 format-method checks on Windows .NET 11 and Ubuntu
.NET 10. All workers joined after 222 seconds of
collection, the persistent archive verified under the lease and the
guest fixtures were removed. The restored archive matched the remote
hash.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Source, identity and checks](source-and-checks.tar.xz).
