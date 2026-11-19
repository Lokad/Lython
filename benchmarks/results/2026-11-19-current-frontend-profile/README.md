# B1067 frontend attribution on the retained trim checkpoint

The exact `12fabffb1` image ran on Ubuntu 24.04.4 and .NET 11 RC3 on the
quiet Azure F4as_v7 on 2026-11-19 UTC. All 32 image files and three CPython
goldens passed local verification. The quiet worker verified each completed
case before and after profiling. Compilation includes the compiled-owner
lifecycle and excludes guest execution.

| Compiler stack ancestry | Manifest | Generated validator |
| --- | ---: | ---: |
| Stacks with identifiable compiler ancestry | 610 | 599 |
| Parsing | 147 | 172 |
| Abstract interpretation | 153 | 103 |
| Executable construction | 140 | 144 |
| Block reachability and normalization | 8 | 33 |
| Operand layout and linearization | 15 | 32 |
| Name-binding and directive diagnostics | 39 | 25 |

These inclusive counts can overlap and omit inlined or unrecognized code.
They are diagnostic evidence, with no elapsed comparison. In the validator,
reachability appears in 21 nearest frontend frames and operand layout in
23. The source also passes over the retained instructions to normalize jump
targets, prepare call results and then forward operands. A compiler
prototype will combine the local instruction passes and reuse the builder's
block list. Keep precise protected-range unwinding and operand depth checks.
The sample counts do not establish the speedup or justify skipping validation.

The annotated-rule lane reached 3.457 of eight required warmup seconds in
128 batches of 512 compilations, so its sampler never started. The two
completed lanes remain diagnostic. Both quiet gates passed, users joined,
and all RAM data was verified in persistent storage under the lease before
the guest fixtures were removed. The overall service recorded the warmup
failure. The original manifest warmup remains in B1066.

[B1066](../2026-11-19-frontend-profile-warmup/README.md) ·
[Attribution](attribution.json) · [Raw observations](observations.tgz) ·
[Procedures](procedures.tar.xz).
