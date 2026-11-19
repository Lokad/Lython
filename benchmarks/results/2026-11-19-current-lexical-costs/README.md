# B1054 rule definition and capture attribution

Profiled retained `68a7fc465` with core `b853302a` on the quiet Azure F4as_v7,
Ubuntu 24.04.4 and .NET 11 RC3, on 2026-11-19 UTC. The ordinary and captured
rule cases are unchanged from B1053. The mutable rule case constructs 2,048
closures and calls each twice while updating a captured `nonlocal` counter.
The complete 32-file worker image and all three cases verified against CPython
outputs before staging. Each case warmed for at least eight guest seconds.
The recorded guest windows were 5.18–5.65 seconds per case.

| Nearest runtime frame | Ordinary rules | Captured rules | Mutable counters |
| --- | ---: | ---: | ---: |
| Resolved runtime stacks | 1,114 | 1,021 | 1,021 |
| CaptureExecutableClosures | 4 | 61 | 58 |
| MemoryGovernor.Reserve | 137 | 106 | 72 |
| Reserve with capture as nearest caller | 0 | 25 | 16 |
| CreateExecutableFunction | 48 | 50 | 35 |
| ValidateNonlocalTargets | 3 | 5 | 6 |

Capture preparation resolves the same names while sizing retained storage and
again while filling the cell array. Prepared source slots can remove repeated
name/namespace lookup for exact executable parent-child pairs. Keep the array
and newly retained cell capacity funded, and use the dynamic path for inlined
iteration namespaces, class cells and incomplete lexical preparation. The
observations point to capture construction before changing nonlocal validation
or frame creation across all calls.

Sampling includes useful and inlined work. Counts overlap and give no estimate
of removable CPU. This run measures attribution rather than an elapsed release
comparison. Workers joined, both quiet gates passed, and all RAM traces were
archived and verified under the lease before unmount. Unknown and ambiguous
frames remain in the raw evidence.

[Attribution](attribution.json) · [Raw observations](observations.tgz) · [Verified procedures](procedures.tar.xz).
