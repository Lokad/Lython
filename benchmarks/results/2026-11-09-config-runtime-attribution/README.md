# Current configuration execution attribution

R160/B510,2026-11-09. One unchanged config-edit.py,181B JSON file,7c10322c-equivalent
source/corecfeb99b0, same verified nonincremental freeze and common worker as R159.
Not a fresh postcommit binary or the subsequent argparse correction. Existing
source-labelled comparison establishes1.27–1.28× warm/1.11–1.12× complete versus
CPython; this capture attributes CPU, not a before/after or speedup.

Six-second whole-batch CPU sampling after6s measured warmup, normal GC/tiering,
only existing perf-map override. Actual execution clocks total4.223s; named guest
worker uses2.90CPU seconds/545samples. Caller/reset/protocol567samples, tiering38,
collection/finalizer1 remain separately visible. Guest unknown leaves42/545(7.71%).
CPU cannot explain off-CPU handoff latency. Sampling includes wider batch/reset/
protocol work than execution clocks; do not compare aggregate CPU with R159 timings.

| Innermost observed guest category | Samples | Share |
| --- | ---: | ---: |
| Mediated host/filesystem |282|51.74%|
| VM prefix, including useful operations |67|12.29%|
| Unassigned |67|12.29%|
| JSON |54|9.91%|
| Context initialization |23|4.22%|
| Text reader |17|3.12%|
| Builtin initialization |12|2.20%|
| Admission |12|2.20%|

Categories are observations, not removable costs; full categories/leaves/raw stacks
remain in the archive. Host paths preserve actual component authorization. The
comparison host supplies efficient ranges/appends; no default-host bottleneck is
established. Output validation/reset primarily run on the caller, outside guest
thread attribution; their timed and excluded portions remain as R159 documents.

Bounded source gate selects **no blanket builtin/type sharing redesign**. Ordinary
functions/signatures already shared. Object/type descriptors retain per-run owners;
exception/callable facades retain lazy name/new slots. Executable namespaces already
lazy, ordinary invocation contexts deferred. Twelve builtin samples do not establish
a dominant phase or justify new singleton/cache machinery. No architectural/CPU
gain claim and no permanent ban. Continue the distinct validated-CSV family to
assess actual per-record and call construction before choosing a redesign.

Original launch failed before mounting/work: reused two-role identity map where
the profiler expects one core hash. Preserve original payload/script/log and identity;
correct only metadata under a fresh idle lease, verify every core/aux hash, then
launch the same finite collection. No timed restart, storage cleanup or lowered gate.

All source/identity/golden/quiet/ownership/Closed/join/RAM/archive-under-lease/localSHA
gates pass. Finite120s owner+30stop+30margin. No users/fixtures remain. Prior delivery
CI42627090614 is cached green; this read-only attribution needs no repeated delivery.
Publication remains postponed and broader performance work remains active.

[Attribution](attribution.json), [source gate](source-gate.json), [frozen source](core-source.json),
[identity](identity.json), [raw capture](observations.tgz), [original setup/correction](preparation.tar.xz),
[terminal status](terminal.json).
