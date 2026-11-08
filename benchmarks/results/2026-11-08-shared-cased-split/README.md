# Shared casing/split preparation — 2026-11-08

R92 reuses the existing UTF-8 casing/whitespace kernel through one call-site
recipe in ordinary executable code and closed native helpers. Both old native-only
fused operation variants and their independent span/evaluation selection disappear.
Branches and protected bodies can now avoid the intermediate cased string and
second dispatch. No new kernel, owner, execution engine or public option.

Baseline: committed ac5aea97, core `6a098198…`, fully qualified CI42463155006 and
the [current CPython checkpoint](../2026-11-08-canonical-method-checkpoint/README.md).
Candidate: bd18ea0f plus the archived R92 production sources, core `68f63d29…`.
Full hashes/source inventory are in identity.json; full delivery is recorded
separately when available. The primary CPython table still describes ac5aea97.

| Complete script | Allocation diagnostic | Quiet-VM elapsed, all four median pairings |
| --- | ---: | ---: |
| Guarded text helper, sync | −12.74% | −4.84 to −3.48% |
| Guarded text helper, async API | −12.74% | −8.01 to −6.78% |
| Existing fused helper | approximately neutral | −4.21 to +3.42%, inconclusive |
| Ordinary rsplit helper | approximately neutral | −2.00 to +1.85%, inconclusive |
| CSV helper | approximately neutral | −0.26 to +1.07%, possible small elapsed debt |
| Report, async API | approximately neutral | −0.65 to −0.17%, small favorable result |

B453: 168.220 seconds, normal GC/tiering, same unchanged worker7cf37ca5 and
optional adapter0.4; Ubuntu24.04.4/F4as_v7, 4EPYC9V45 cores, .NET11RC3.
Five-second warmup per process/job; seven rotated150ms rounds, two independent
copies of each source. Guarded coarse process CPU agrees: sync−4.76to−3.23%,
async−8.62to−7.02%. Fused/rsplit controls vary across identical copies; they do
not establish improvement or debt repayment. CSV remains uncertain. This is a
source-pair comparison, not a new CPython measurement or proof of the whole goal.

Timing includes complete execution, mediated RAM-file I/O, full output validation
and prior execution cleanup. It excludes compilation/startup/staging and final
cache teardown. The host completes I/O inline; genuine suspension is qualified
separately. Allocation uses the existing isolated-image desktop diagnostic,
including result disposal/budget checks. Compilation allocation rises about
0.1–0.3%; guarded compile-to-result allocation falls about11.8%. Frontend elapsed
is unstable and supports no CPU claim. Desktop guarded async elapsed+68.76to82.65%
is preserved; B453 did not reproduce it, without a complete causal explanation.

Focused checks:3859public Release11;128public semantics and24internal ownership
per actual11/10. Existing Unicode cases now also run in branchy and protected
bodies; native/dynamic receivers, custom suspending methods, forwarded-local
visibility, retained results, cancellation at admission, first terminal refusal,
input funding and cleanup/budget reuse remain covered. No public API change.

All source/core/auxiliary/Ready/golden/quiet/Closed/exit/join/fixture/archive gates
pass; no VM users or fixtures remain. observations.tar.xz preserves both cores,
sources, original raw local/VM results, failed local setup/analysis conditions and
receipts. Disk headroom came from verified atomic hardlinks of byte-identical
old cores: every path/content and archive remains intact, proof included.
No RSS, tail-latency, startup or universal CPU claim.
