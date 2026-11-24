# B1654 Reuse the spelling of each declaration and pattern binding

Parser bindings derive their declared and mangled names from the same normalized
token. A plain capture goes directly to its pattern node. An alternatives list
is allocated when the parser sees a pipe. This removes temporary strings and
syntax objects while preserving the mangling and normalization rules.

The frozen reference is `132d066c1`. The candidate adds the parser patch alone,
so this comparison isolates the parser. Tape, spans and storage shape match
across 170 code objects with 7,084 instructions from 31 sources. The CPython
3.13.2 outputs match for three complete workloads
and a Unicode pattern with mangled captures, a reordered OR alternative,
sequence and mapping rest, and an alias.

| Workload | Compilation allocation before | Compilation allocation after | Retained compilation admission |
| --- | ---: | ---: | ---: |
| Generated classifier | 954,640 B | 888,504 B | 703,904 B in both images |
| Sparse generated validator | 2,170,208 B | 2,170,128 B | 2,294,048 B in both images |
| Configuration manifest | 215,968 B | 215,376 B | 212,960 B in both images |

The Windows .NET 11 RC3 diagnostic measures managed allocation during warmed
compilation and compiled-owner disposal. The classifier allocates 6.93% less.
The sparse validator and manifest shifts are under 0.3%. The measurement covers
allocation. Elapsed time and CPU remain unmeasured for this patch.

Windows and Ubuntu each pass 281 focused .NET 11 and 55 .NET 10 Release checks
on the combined layout and parser source. Each compilation owner in the
diagnostic returns its shared allowance after disposal.

[Images](identity.json) · [Compile allocation](compile-allocation-summary.json) ·
[Source patch, tape comparison, tests and procedures](procedures.tar.xz).
