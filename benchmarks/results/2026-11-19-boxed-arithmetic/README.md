# B1128 shared boxed operand reads

Measured retained source `42055debe` and private prototype `980ffd04e`
on the quiet Ubuntu 24.04.4 Azure F4as_v7 with .NET 11 RC3 on November 19,
2026 UTC. Both complete 32-file images matched five CPython goldens.
Five rotated rounds used two processes per source. All 100 timed
responses passed and both quiet machine gates passed. The workers joined
and the archive was verified under the lease before fixture removal.

| Workload, warm execution | Candidate elapsed change | Candidate process CPU change |
| --- | ---: | ---: |
| Annotated rule closures | +2.37% to +5.34% | +4.04% to +5.39% |
| Ordinary rule closures | +1.87% to +4.44% | +1.72% to +3.59% |
| Captured rule closures | +1.91% to +4.48% | +1.04% to +4.56% |
| CSV invoice | +0.41% to +3.22% | 0.00% to +2.90% |
| Floating error report | +1.13% to +2.80% | +1.03% to +1.89% |

Ranges cover all four cross-source process-median pairings. Same-source
elapsed spread is at most 1.98%. The prototype removes duplicate boxed
operand reads when builtin arithmetic falls through to protocol dispatch.
It also routes completed native arithmetic through the larger input
dispatcher. The comparison establishes a whole-script regression and
does not isolate the cost of that routing. Keep the candidate private.
The existing native entry stays the reference for any narrower follow-up.

Both sources produced identical allocated bytes and accounted peaks on
all five local workloads and restored the shared allowance. The prototype
passed 1,197 focused Release checks each on Windows .NET 11 and Ubuntu
.NET 10, covering arithmetic, operand ownership, cancellation and cleanup.
The initial VM storage preflight refused staging. The completed run used
two verified archived image copies to recover working space. The original
preflight is in the procedures archive.

Warm elapsed includes mediated RAM-backed I/O, golden checks and disposal
of the previous result. Process CPU includes the wider worker request.
This comparison times Lython revisions against fixed CPython goldens.

[Measurements](summary.json) · [Raw observations](observations.tgz) ·
[Lease receipt](archive-receipt.json) · [Source, checks and procedures](checks-and-procedures.tar.xz).
