# B1182 keyword-default dictionaries on observation

Private `ba21d43ac` binds prepared keyword defaults from their admitted
record values. On first read of `__kwdefaults__`, the function wraps the
same values in an independently mutable dictionary. Its allocation
was admitted before evaluating defaults. Replacing an unobserved array
returns its allowance. An exposed dictionary keeps its charge.

| Workload | Boundary | Windows warm allocation | Quiet elapsed change | Quiet process CPU change |
| --- | --- | ---: | ---: | ---: |
| Ordinary rule closures | Compile-to-result | −19.24% | −3.19% to −0.56% | −2.20% to +0.35% |
| Eager annotated rules | Warm | −15.82% | +0.24% to +1.71% | +0.11% to +2.14% |
| Nested factories | Warm | −11.55% | −4.02% to +1.53% | −5.61% to +2.42% |
| Every keyword dictionary read | Warm | 0.00% | −1.80% to +1.59% | +0.55% to +2.40% |
| File-backed CSV invoice | Warm | +0.00% | −4.79% to −0.33% | −3.85% to +0.62% |
| Generated dataclasses | — | +0.12% | — | — |

The allocation column is a separate warm diagnostic, including for
ordinary rule closures. All six accounted peaks match and every workload
returns the borrowed allowance to baseline.

The quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3 compared the
candidate with frozen `289380214`, which matches `576434749` in runtime,
test and worker sources. The run took place on November 20, 2026 UTC.
Warm includes guest I/O, golden verification and prior-result disposal.
Compile-to-result adds compilation and disposal of its owner. Process CPU
covers the wider batch. Ranges cross both independent worker medians per
revision.

The candidate stays private. Ordinary compile-to-result takes less
elapsed time across all pairings. Eager factories and read-every-dictionary
CPU increase across all pairings, while nested factory timing and invoice
CPU have mixed results. The nested reference spread reaches 4.86%. Test
selecting original keyword-default storage once per bind, retaining live
lookups after protocol callbacks and the original adverse measurements.

Both complete 32-file images matched six CPython goldens. Windows .NET 11
and Ubuntu .NET 10 each passed 912 focused checks of replacement, aliases,
order, required parameters, admission and asynchronous cleanup. Both
quiet gates and all 100 timed batches passed. All workers and fixtures
were released and the archive was verified under the VM lease.

[Summary](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Checks and procedures](checks-and-procedures.tar.xz).
