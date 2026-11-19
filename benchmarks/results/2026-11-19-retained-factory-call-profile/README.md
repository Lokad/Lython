# B1081 call attribution after factory-frame removal

The retained `1c9a8e6a1` runtime ran on the quiet Azure F4as_v7 with
Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-19 UTC. The complete 32-file image
matches B1079 and all three catalogs passed the local CPython goldens.
Each warm lane verified output and files before and after CPU sampling.

| Observed runtime work | Ordinary rules | Annotated rules | Expanded forwarding |
| --- | ---: | ---: | ---: |
| Stacks with runtime ancestry | 1,088 | 1,076 | 1,015 |
| Instruction loop as nearest runtime frame | 186 | 146 | 115 |
| Admission as nearest runtime frame | 124 | 118 | 61 |
| Prepared operation dispatch as nearest runtime frame | 76 | 75 | 6 |
| Native call start as nearest runtime frame | 70 | 44 | 0 |
| Executable function construction as nearest runtime frame | 52 | 75 | 0 |
| Admission with record construction as nearest caller | 28 | 51 | 0 |

Neither rule capture has sampled invocation-frame construction. Both still
show definition construction, default ownership and native entry. The
annotated lane has 38 nearest frames under metadata attachment. The
forwarding lane has 68 nearest frames under split construction, 45 under
keyword copying and 43 under positional formatting. Keyword copying
protects the live source dictionary and reentrant admission. The
attribution identifies remaining work without establishing an avoidable
CPU fraction or a reason to bypass those semantics.

Both quiet gates passed. The workers joined, the RAM traces were verified
in persistent storage under the lease, and guest fixtures were removed.
Sampling groups can overlap or miss inlined methods. B1081 is a source
selection aid with no paired elapsed or CPU claim.

[Attribution](attribution.json) · [Raw traces](observations.tgz) ·
[Procedures](procedures.tar.xz).
