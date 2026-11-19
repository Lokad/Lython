# B1093 retained rule execution profile

On November 19, 2026, this diagnostic profiled the three rule scripts from
[B1091](../2026-11-19-inline-forwarding-cpython/README.md) on its verified
`06b40a712` Release image, core SHA-256 `c3e382aa…`, using .NET 11 RC3 on the
quiet Azure F4as_v7 VM running Ubuntu 24.04.4. Each sample comes from a warm
worker running the original script. The sample boundary includes worker-side
fixture reset, output checks and result cleanup. These scripts issue no guest
file I/O. The collection records sampled names, without a candidate image or a
paired timing comparison.

| Rule script | Samples | Resolved guest ancestry | Unknown leaf | Instruction prefix | Reserve | Builtin arithmetic | Create executable function |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Ordinary | 1,049 | 1,040 | 66 | 176 | 121 | 109 | 41 |
| Annotated | 1,071 | 1,063 | 80 | 168 | 103 | 58 | 41 |
| Captured | 1,263 | 1,251 | 138 | 189 | 138 | 104 | 42 |

The named columns count the nearest runtime frame. Native entry, prepared
operations and result consumers also appear. Captured rules record 92 nearest
frames at `BeginBoundCall` and 25 at `CaptureExecutableClosures`. Ordinary and
annotated reserve callers frequently include pending defaults and record
capacity. The captured reserve profile instead includes function values,
closure capture and executable value storage. See [attribution](attribution.json)
for frame names, ancestry, raw sample counts and unresolved paths.

Arithmetic is visible across all three scripts. Inspecting the shared native
and general integer dispatch found a duplicated first-operation probe. If a
later operation in a chain needs the general path, the existing implementation
also discards successful integer work. A candidate can publish the completed
prefix and resume at the first operation that needs general dispatch. The
profile does not establish how often partial chains execute or how much total
CPU the change can save. A paired script comparison with an unaffected control
is needed for an elapsed or CPU claim.

The [lease receipt](archive-receipt.json) records quiet gates, worker joins,
fixture cleanup and trace verification before unmount. [Raw observations](observations.tgz),
[collection procedures](procedures.tar.xz) and [terminal status](terminal-status.json)
preserve the measurement and environment evidence.
