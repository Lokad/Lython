# Module binding in an existing instruction operand

R479/B892, 2026-11-17 UTC. **Private prototype; decision pending.** Qualified
d8611c85/frozen0315de0a versus be7497de, eight production files. A cancellable
compiler fixup puts known module indexes in LoadGlobal's existing spare operand.
Instructions and code objects gain no fields or retained tables. A name/bounds
guard reads the live frame, retaining ordinary namespace/builtin lookup on a miss.
No value caching, alternate execution, owner or public API is introduced.

| Complete script | Phase | Elapsed change | Wider CPU change | Reference / candidate controls |
| --- | --- | ---: | ---: | ---: |
| Floating JSON report | Warm | −0.86..+5.46% | −2.28..+7.59% | 1.43 / **4.88%** |
| Same | Compile and execute once | **+2.00..+3.12%** | **+1.81..+3.27%** | 0.19 / 0.91% |
| Integer CSV invoice | Warm | −2.46..−1.95% | −2.78..−0.83% | 0.23 / 0.30% |
| Helper text | Warm | −0.75..+0.39% | −1.85..−0.37% | 0.19 / 0.96% |
| Floating report | Compilation | +0.68..+3.57% | −0.98..+2.55% | 0.43 / 2.44% |

Natural-GC allocation and accounted peaks are unchanged in all three scripts;
all shared allowances return to baseline. Each actual Windows target passes
226 public and 65 internal focused cases: live rebinding/deletion, builtin fallback,
classes/closures, imports, pending I/O, cancellation, refusal, retained results and
cleanup. Actual copied 32-file images both pass three independent catalog goldens;
the candidate also passes all 13 previous complete-script goldens. No repeated
qualification of the unchanged delivered source.

193.34 seconds, five jobs, quiet Azure F4as_v7/Ubuntu24.04.4/.NET11 RC3; six-second
rotated full-request warmups, five sample rounds, natural GC and normal tiering.
Warm guest clocks include Run, mediated RAM I/O, output/file validation and previous
result cleanup; exclude compilation/startup/reset/protocol/final cache disposal.
Fresh adds compilation and compiled-owner disposal. External CPU has a wider
boundary. No CPython ratio or CPU gain is inferred from source simplification.

First staging failed before deletion or timing because an already removed working
raw was named. The failure is preserved; repaired staging freshly verifies exact
committed superseded core/evidence replicas, including all inode aliases. Current
images, toolchains, canonical evidence and 254/252 MiB floors remain intact. All
workers joined, machine gates passed, RAM removed, archive SHA verified; terminal
cached. Publication held.

The adverse fresh result is real evidence; the warm candidate control is unstable.
One unchanged-image warm/fresh confirmation with eight-second warmups is justified
before retain/refine/withdraw. This is uncertainty, not a permanent design ban.

[Summary](summary.json), [allocation](allocation-summary.json),
[validation](validation.json), [raw](first-pair/observations.tgz),
[identity](identity.json), [exact sources](candidate-sources.tar.xz),
[prototype](prototype.patch.gz), [core](candidate-core.dll.gz),
[procedures and original failure](procedures-and-checks.tar.xz).
