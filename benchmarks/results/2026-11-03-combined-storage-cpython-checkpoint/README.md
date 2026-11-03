# Combined storage checkpoint against CPython (B184)

2026-11-03, current runtime source7a3a8afe (B183), .NET11 RC3 /CPython3.13.16.
Frozen precommit core`0f143a6c68393a6149202e6238e8e6e4d62e61e775b545bd2868bc4d38aa47a9`
is3eee2a0e plus the B183 source patch, now committed as7a3a8afe. Source files/hashes
and unchanged auxiliaries retain their actual identities; later SourceLink metadata
can change binary hashes without changing runtime source.

Direct coupled baseline is pre-B171 B166 sorted-record-key source, qualified as
2323cceb, frozen`3bed411efcd6e008fc04bca8634d32be64bafc71742509ff0192e8eee86ab8bc`.
The two old roles load the SAME physical image. Combined source changes are B171
native loop operands, B174 terminal UTF-8 storage, B178 direct casing and B183
reader-owned windows. This is one direct pair, not percentages added from earlier
independent measurements. Historical individual debts/results remain intact.

## CPython as the external reference

| Boundary /complete script | Lython ms | CPython ms | Lython /CPython | Lython /CPython IQR | Old-control difference |
| --- | ---: | ---: | ---: | ---: | ---: |
| Warm CSV4,096 records |5.365323|3.335063|1.609×|1.08/0.67%|+0.42%|
| Fresh CSV |5.540721|3.480726|1.592×|3.94/0.55%|+1.00%|
| Warm text3,072 lines |3.250456|1.180861|2.753×|0.50/0.46%|+8.31%|
| Fresh text |3.511504|1.261621|2.783×|1.60/0.21%|−10.01%|
| Warm JSON4,096 records |5.669256|4.090850|1.386×|0.89/0.42%|+2.40%|
| Fresh JSON |6.198490|4.435978|1.397×|0.56/0.71%|+0.08%|
| Warm report3,072 records |5.326380|2.356802|2.260×|2.02/2.94%|−1.77%|
| Fresh report |5.675469|2.506381|2.264×|3.17/3.86%|−0.57%|
| Compile CSV only |0.085351|0.054600|1.563×|0.67/0.19%|+1.25%|
| Compile report only |0.059655|0.037737|1.581×|0.52/0.18%|+2.57%|

Above one means slower. These are bounded diagnostic observations, not certified
multipliers. Text's identical old controls differ8–10% despite small within-worker
IQR; fresh source-pair text straddles old roles and is INCONCLUSIVE. Report fresh
has3–4% IQR and stays variable. Keep these limits next to the ratios. Startup and
actual suspending-I/O throughput are unmeasured; do not subtract unrelated warm/
fresh/compilation medians. No asynchronous CPU or elapsed debt is closed here.

## Coupled source-pair decision

| Complete script | Current vs two old roles, warm | Current vs two old roles, fresh |
| --- | ---: | ---: |
| CSV |+1.64–2.06%|+0.83–1.84%|
| Text |−2.28 to−9.78%|−9.63 to+0.43%|
| JSON |+0.29–2.70%|+1.81–1.89%|
| Report |−2.74 to−4.46%|−3.71 to−4.26%|

Retain the combined storage/ownership changes. Synchronous B171 report debt is
CLOSED at this combined checkpoint: report is net below BOTH pre-B171 old roles;
this does not rewrite the original B171+1.35% observation. CSV remains an ACCEPTED
~1–2% synchronous tradeoff for the retained storage simplifications. Fresh JSON
~1.8–1.9% is confirmed current synchronous debt, ACCEPTED pending the next coherent
primitive-storage milestone; do not call it noise or hide it behind allocations.
Warm JSON is near one old role; text fresh remains uncertain. Prior asynchronous
CSV/report uncertainty and B183 async controls remain separate. Allocation savings
are established in their own source-pair evidence, not inferred from these clocks.
The large text/report CPython gaps and whole-script objective stay OPEN.

## Method and evidence

Four independent workers per job: current Lython, old Lython, SAME-BITS old control,
isolated CPython. Forty workers total. Three seconds adaptive warmup/worker, one
second settle, nine rotated200ms target batches. Runtime/GC/tiering defaults;
CPython normal GIL/GC, GCC13.3 PGO/LTO, -I -S -X utf8. Quiet Azure Standard_F4as_v7,
Ubuntu24.04.4 x86-64,4 AMD EPYC9V45 vCPUs,16GiB/no swap. SDK/runtime
11.0.100-rc.3.26522.148 /11.0.0-rc.3.26522.148.

Protocol2 clocks include invocation, actual mediated tmpfs file I/O, complete
stdout/file verification and previous-result cleanup. Fixture reset/staging is
excluded symmetrically. Fresh compiles every invocation and includes Lython's
per-invocation compilation disposal. CPython uses fresh globals with process module
caches. Lython execution caches are isolated. Warm reuses compiled code; compile
only performs compilation/validation and replaces prior compiled objects without
guest execution. Final cached result/program/fixture/worker teardown and transport/
response hashing are outside clocks but JOINED. The documented CPython/Lython
result-retirement differences remain; no equal-live-memory claim.

Every full golden, fixture cleanup, worker join, identity/hash and before/after
quiet gate passed. Owned216.533s, collection215.918s, exit0. Evidence and frozen
workers are persistent; guest fixtures use tmpfs. All raw samples/warmups/identities/
filesystem/machine snapshots, collector and catalog are retained. Retrieved archive
SHA256`bee558b848c4d0f1995bce1622089fb23473d7f80e831282b47558ece2ff7647`
verified locally. No forced GC, runtime override or changed admission policy.
Publication/tagging remain postponed; platform delivery receipt is separate.
