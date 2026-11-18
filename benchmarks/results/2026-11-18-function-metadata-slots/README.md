# B1010 fixed function metadata

Measured the R583 source `7a0bc456b` (`91b7ea2d`) against prototype
`57ed0b1e8` on the quiet Ubuntu Azure F4as_v7 with .NET 11 RC3 on
2026-11-18 UTC. Both 32-file images and the unchanged B1009 catalog
verified. Five jobs took 229.64 seconds. Each used two processes per
source with eight seconds of warmup and five measured rounds. Both
quiet gates and 100 measured golden responses passed. All workers joined,
the archive was verified and fixture teardown completed.

| Workload and boundary | Candidate elapsed change | Candidate process CPU change |
| --- | ---: | ---: |
| Rule closures, warm | −2.39% to +1.48% | −3.16% to +1.20% |
| Annotated rules, warm | −3.62% to −0.76% | −5.94% to −1.36% |
| Positional calls, warm | −0.15% to +1.42% | −1.60% to +1.66% |
| Rule closures, compile-to-result | −3.17% to −1.18% | −3.77% to −1.82% |
| Rule closures, compilation only | −0.47% to +2.54% | 0.00% to +2.60% |

The change removes a CLR dictionary per function and funds six fixed
metadata slots with the function wrapper. Custom attributes keep their
Python dictionary. Standard metadata preserves assignment, deletion
and lazy annotation recreation. Both lowered creation paths now preflight
the wrapper and release an uncommitted reservation if construction fails.

A separate Windows allocation check reports 2.72% fewer allocated bytes
for unannotated rules and 9.21% fewer for annotated rules. The unannotated
accounted peak rises 2.59% (5,058,225 to 5,189,329 bytes) with fixed fields
funded from creation. The annotated peak drops 1.24% (5,295,285 to
5,229,781 bytes). All shared allowances returned to baseline. Retain the
simpler fixed storage, lower allocation and observed annotated warm and
unannotated one-shot gains. Unannotated warm, compilation alone and
positional timing remain inconclusive. The positional control uses a new
reference and does not resolve the earlier adverse B1009 observation.

Focused Release checks passed 227 public and 76 white-box cases each
on Windows .NET 11 and WSL .NET 10. Both APIs matched CPython 3.13 for
standard attribute changes, independent mutable metadata, annotation
aliases, custom dictionary shadowing and generic type-parameter identity.
Full Windows and Ubuntu qualification follows at delivery.

Warm includes execution, output verification and prior-result cleanup.
Compile-to-result adds compilation and disposal of its owner. Startup,
fixture reset, protocol and final teardown are outside guest elapsed.
Process CPU covers the wider request.

[Measurements and image hashes](summary.json) · [Raw observations](observations.tgz) ·
[Source, checks and procedures](checks-and-procedures.tar.xz)
