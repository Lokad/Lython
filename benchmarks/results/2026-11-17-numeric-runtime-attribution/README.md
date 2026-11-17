# Current numeric-report CPU attribution

R472/B882,2026-11-17UTC. Committed dc480d38/frozen5ecb2599, same fixed
three-case catalog and current optional0.4adapter. Two complete4,096-record
numeric JSON/report scripts differ only in integer versus exact-fraction data.
Existing checked workers/perf protocol; two5s captures after8s guest warmup.
**Attribution, not an elapsed comparison or a removable-cost estimate.**

| Observation | Integer | Floating |
| --- | ---: | ---: |
| Active-thread samples | 971 | 983 |
| Unmapped leaves, retained | 63 | 59 |
| Nearest instruction-prefix frame | 150 | 127 |
| Nearest stack-transfer frame | 97 | 104 |
| Nearest direct-sequence-index frame | 38 | 108 |
| Nearest native-call selection | 29 | 31 |
| Nearest JSON construction | 62 | 51 |
| Nearest numeric JSON conversion | 33 | 91 |

JSON inclusive122/177samples overlaps useful decoding; call/frame216/157overlaps
useful native work. The generator/iteration category matches general MoveNext
carriers, including async JSON wrappers; its922/943samples do not establish time
spent in generators. Broad stacks are not exclusive/removable shares.
All raw stacks/threadCPU/unknowns remain. Managed stack checks stay; these samples
do not justify removing cancellation/admission or predict a speedup.

Current source rejects two stale proposals: JSON already has a single iterative
construction loop, admitted inline/grown frames and transfers only real pending
callbacks. Default numeric parsing is native UTF-8. PyList already funds metadata/
scalar headroom at capacity, not through per-number admissions. Do not rebuild
these delivered mechanisms. R469bulk raw-token scanning and R470argument staging
remain withdrawn; sampled dispatch alone does not vindicate their old designs.

Completed source gate: LoadGlobal still resolves the global context and
probes the active module's string→local/closure maps per read, despite immutable
compiler-owned module slot metadata. Assess preparing that binding fact once,
guarded by the actual module layout, without caching values. Rebinding/deletion,
builtin fallback, globals dictionaries, imports/class/lazy scopes, real suspension,
current tokens and funded aliases must remain. [Executable sites](global-read-shapes.json)
show three reads per numeric-generator iteration (two data,one helper),12,288per
complete script; helper text/report/call controls contain none. The private next
prototype prepares those facts; no CPU forecast or broad compiler rewrite follows
from this profile. Original c599dac6already prepared global opcodes from lexical
facts; this gate concerns repeated module-slot discovery, not redoing that work.

Quiet AzureF4as_v7/Ubuntu24.04.4/.NET11RC3, natural workstationGC; only diagnostic
PerfMapEnabled=3 override. Whole batch CPU is wider than guest clocks: reset/I/O/
validation/protocol remain visible, unknown/native frames retained. Active guest
thread CPU4.89/4.95s; tieredJIT0.08/0.09s and other threads recorded separately.
All identities/goldens/pre-post quiet/Closed/exits/joins pass. Owned RAM raw/maps/
stacks archived and byte-verified persistently under lease before unmount. Terminal
cached; no repeated capture. Retire only committed B880workingraw395,964Bwith fresh
UID/path/hash/link/all-inode-alias proof. Current images/toolchains/canonicals and
254/252MiBstaging/final floors preserved. CI44022750002active independently;
no runtime/contract change or publication from attribution.

[Attribution](attribution.json),[raw/maps/stacks](observations.tgz),
[identity](identity.json),[procedures](procedures.tar.xz).
