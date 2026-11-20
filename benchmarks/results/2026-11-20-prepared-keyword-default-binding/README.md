# B1183 prepared keyword-default binding

Retained `848cbf783` selects original keyword-default value storage once per
bind. It removes eager dictionary construction and lookup when Python never
reads `__kwdefaults__`. The first read adopts the already admitted values
into a mutable dictionary. Binding through a published or replacement
dictionary retains live lookup after callbacks.

| Workload | Boundary | Windows warm allocation | Quiet elapsed change | Quiet process CPU change |
| --- | --- | ---: | ---: | ---: |
| Ordinary rule closures | Compile-to-result | −19.24% | −2.65% to +2.13% | −2.39% to +1.22% |
| Eager annotated rules | Warm | −15.82% | −2.83% to +1.21% | −3.98% to +1.48% |
| Nested factories | Warm | −11.55% | — | — |
| Every keyword dictionary read | Warm | 0.00% | −3.03% to +3.72% | −2.29% to +1.44% |
| File-backed CSV invoice | Warm | +0.00% | −1.21% to +0.45% | −1.96% to +1.22% |
| Generated dataclasses | Warm | +0.09% | — | — |

All six admitted peaks match and all workloads return the shared allowance
to baseline. The allocation column comes from a separate warm diagnostic.
All four elapsed and CPU ranges span zero. The read-every-dictionary
reference workers differ by 5.75% in elapsed time. B1182's earlier binder
incurred consistently higher CPU on eager factories and dictionary reads.
That direction did not repeat in this comparison. Retention is based on
removing materialization and allocation while preserving semantics.
[Original comparison](../2026-11-20-deferred-keyword-defaults/README.md).

The run took place on November 20, 2026 UTC on the quiet Azure F4as_v7 with
Ubuntu 24.04.4 and .NET 11 RC3. Frozen reference `289380214` matches
`576434749` in runtime, test and worker sources. Warm covers mediated I/O
where used, golden checks and prior-result disposal. Compile-to-result adds
compilation and disposal of its owner. Process CPU covers the wider batch.
Ranges cross both independent worker medians per revision.

Windows .NET 11 and Ubuntu .NET 10 each passed 912 focused Release checks.
Both complete images matched six CPython workload goldens and six additional
probes in both Lython execution modes. The probes cover sparse required
keywords, defaults observed by decorators and generators, replacement and
retained aliases. Both quiet gates and all 80 timed batches passed. All
workers joined and the archive was verified under the VM lease.

[Summary](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Checks and procedures](checks-and-procedures.tar.xz).
