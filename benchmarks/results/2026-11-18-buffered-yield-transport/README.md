# Buffered yield transport

R510/B935, 2026-11-18 UTC. Private **1bce52ad** oncf052bd5 versus fully
qualified **cf052bd5 / abab8760**. Retain the complete same-command buffered yield
handoff inside the existing executable prefix. An immediately discarded send result
needs no suspended YieldValue/waiting state, prefix return/reentry, send(None)
publication or PopTop dispatch. Duplicate completed-yield driver probes disappear.
No new opcode, framework, owner, cache or resource policy.

Existing admitted List/PyList/sum destinations keep their capacity/pull rules.
Generic join still drains before item validation. Unbuffered send/throw/close,
mirrored namespaces and genuinely suspended I/O retain their boundaries; delegation
uses the same existing resume semantics and pending disposal finishes before work.
A declined sum reaches ordinary addition once, outside its running generator.

Both actual .NET11/.NET10 pass422internal/479public focused cases, including real
delayed I/O, cancellation/awaited close, terminal refusal, aliases and budget reuse.
Two new integer-promotion/reentrant-consumer programs independently pass CPython3.13.2.
Six actual-worker goldens/allocation/reuse pass; every32-file copied image hash and
the catalog were verified before staging. Allocation is effectively flat and all
six accounted peaks are unchanged. No memory improvement claim.

Quiet Azure F4as_v7/Ubuntu/.NET11RC3, default GC/tiering; four jobs154.99seconds,
six-second warmups/five rotated rounds/two processes per source:

| Complete script/phase | Elapsed change | Wider process CPU change | Reference/candidate spread |
| --- | ---: | ---: | ---: |
| Text normalization warm |−10.90..−7.68%|−12.05..−8.38%|1.59/2.00%|
| Text normalization compile-to-result |−10.57..−8.31%|−11.08..−6.41%|1.07/1.44%|
| Floating warm |−2.00..−0.61%|−1.09..−0.43%|1.21/0.20%|
| Positional calls warm |−1.31..+2.03%|−0.77..+3.59%|2.26/1.11%|

Keep the named whole-text benefit and coherent removed transport. Positional-call
uncertainty and possible CPU regression remain explicit debt; textwarm maximum
IQR4.31% also limits precision. No universal gain, new CPython ratio, causal split
between removed steps or subtraction of source conditions. Full platform/original
stress/installed Release qualification is separate, not inherited from the reference.

Warm includes Run, mediated RAM I/O, goldens and prior-result cleanup. Fresh adds
compilation and compiled-owner disposal. Startup/reset/protocol/final cache teardown
are excluded; process CPU includes wider worker costs. Quiet before/after, joined
workers, persistent/local SHA archive and RAM teardown all pass. Only exact committed
old65fc working aliases retire after currentabab full qualification and fresh idle
path/UID/hash/link/all-alias proof; currentabab/catalogs/toolchains/unmatched evidence
remain,254/252MiB floors unchanged. An earlier read-only bfef replica proof found it
absent and retired nothing; original condition retained.

Initial untimed two-file source/passing checks remain preserved. Source review
identified repeated declined-sum probing; no observable failure was demonstrated.
The measured three-production-file refinement consolidates completed yields before
timing. No repeated measurement of an unchanged condition.

[Decision](decision.json),[summary](summary.json),[allocation](allocation-summary.json),
[raw observations](observations.tgz),[identity](identity.json),
[source](candidate-sources.tar.xz),[patch](prototype.patch.gz),
[source gate](source-gate.json),[checks/procedures](procedures-and-checks.tar.xz).
Publication postponed; broader whole-script performance work continues.
