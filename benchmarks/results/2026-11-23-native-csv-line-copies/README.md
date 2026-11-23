# Native CSV decoded-line inventory

The production CSV parser at `30d956735` copies decoded file windows into
parser scratch. A core built from `9b35ab6b8` adds source counters for
each physical-line path. All four CSV cases and the independent Unicode
text control use the archived B1446 catalog and the same comparison host.
Each completes once on Windows and .NET 11 RC3. The runner checks stdout,
output file bytes, result cleanup and shared-allowance reuse.

| Script | Bytes copied from decoded windows | Incomplete window segments | Native literal completions | Native handoff to general grammar |
| --- | ---: | ---: | ---: | ---: |
| Native typed ASCII CSV | 139,299 | 8 | 4,096 | 0 |
| Native typed Unicode CSV | 223,267 | 12 | 4,096 | 0 |
| Quote-heavy native invoice | 174,147 | 9 | 3,072 | 1,024 |
| Borrowed typed ASCII CSV | 0 | 0 | 0 | 0 |
| Nonlocal Unicode text control | 0 | 0 | 0 | 0 |

Literal scanning resumes the general grammar for 1,024 physical lines
of the quoted invoice. The general parser directly handles 1,025
additional lines, including continuations and first admission.
The native meters each have one first-admission line on the general
path. The borrowed typed meter completes 4,096 literal records and copies
zero fallback-prefix bytes. The counters identify a copy boundary shared
by ordinary native records. They do not quantify its CPU cost. The
earlier native ASCII slowdown cannot come from repeated parser handoff
on this workload. A prospective decoder loan must retain source funding
through admission and consumption and preserve copying for records
that cross windows or require transformations.

[Counts](counts.json) · [Image and catalog identity](source.json) ·
[Instrumentation and local checks](procedures.tar.xz).
