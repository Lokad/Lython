# B1859 Direct one-shot ranges on signed PythonRe 0.5.0

The optional adapter sends original UTF-8 bytes and explicit byte bounds
to the producer's one-shot matcher for patterns with zero or one capture
group. The match uses caller-owned local range slots. With one group,
participation determines `lastindex`. The direct path removes cursor
creation and the retained range snapshot for these matches. Multi-group
one-shot matching and iteration keep the cursor's last-close metadata.
Program and working owners retain their existing lifetime and first-refusal
behavior.

Windows and Ubuntu WSL each pass 744 focused Release checks per target.
The added checks cover a repeated capture, a missing optional capture and
a named lookbehind capture before `pos`. A subprocess verifies that the
producer threw with the active token during both direct match shapes.
Release backend DLLs match the signed package in the test host and its
subprocess. Working refusal and refund checks cover zero, one and two
groups and iteration. The core restores and builds independently.

## Allocation and admission

Both frozen images use the B1856 core and the same signed PythonRe 0.5.0
DLLs. Only the optional adapter differs. All five complete scripts match
independent CPython output and files on Windows and on the VM. Both APIs
return their budgets to baseline with matching compilation and execution
peak admission in the measured execution checks.

| Workload | Warm sync allocation, previous → direct | Reduction |
| --- | ---: | ---: |
| Compiled log search, one group | 3,511,782 → 2,037,222 B | 41.99% |
| Module log search, one group | 3,708,318 → 2,233,758 B | 39.76% |
| Compiled log search, no groups | 3,126,702 → 1,570,104 B | 49.78% |

Async reductions agree to two decimal places. The two-group case allocates
the same bytes in both images. The invoice differs by at most 110 B.

## Quiet timing

November 27, 2026 UTC on Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3.
Two workers per image warm for at least five seconds each and supply seven
rotated batches. Ranges span pairings of worker medians. Warm elapsed
includes mediated RAM-backed I/O, verification and previous-result disposal.
Process CPU also includes fixture reset and request framing. Both clocks
exclude compilation and startup.

| Workload | Warm elapsed change | Process CPU change |
| --- | ---: | ---: |
| Compiled log search, one group | −11.85% to −11.07% | −11.49% to −8.71% |
| Module log search, one group | −13.68% to −10.16% | −11.67% to −9.92% |
| Compiled log search, no groups | −17.28% to −15.30% | −15.77% to −14.11% |
| Compiled log search, two-group control | −3.14% to −0.67% | −3.45% to −1.03% |
| Quoted CSV invoice control | −2.63% to +1.75% | −3.48% to +3.69% |

Retain the small one-shot range path. Both one-group workloads and the
no-group case improve across worker pairings. The two-group cursor path
also moves favorably in this run. Its cause remains open. The invoice
control spans zero. The earlier development measurements retain their
original images and adverse observations. [Development evidence](../2026-11-25-regex-050-artifact-replay/single-group/README.md).

All 140 timed batches and both quiet gates pass. Staging reuses 32 reference
files and 31 candidate files. All workers join, RAM fixtures are removed
and the persistent archive verifies under the lease.

[Package and test identities](qualification.json) · [Timing summary](summary.json) ·
[Allocation and admission](allocation-summary.json) · [Image identity](identity.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Patch and checks](procedures.tar.xz).
