# Text storage gate — R75, no current capture

Frozen scripts preserve the original conditions; they are historical evidence,
not maintained operational tooling.

2026-11-08, committed runtime33b6c191/corebb6e72b5. Production is unchanged by
this investigation. **No valid current allocation capture or timing result.**
Stop the setup attempt here. This is missing evidence, not rejection or permanent
closure of scalar/token-storage designs. The source review selects whole token
storage for the next bounded design assessment; it does not promote a prototype.

The existing exact-integer path boxes each arithmetic result, but object arrays
already combine locals/temporaries and direct inputs avoid redundant transfers.
Merely replacing the operand array would leave boxes at dictionaries, closures,
mirrored locals and suspended calls while increasing frame capacity. Existing
native helper returns, readonly call descriptors and sequence adoption are retained.
Whitespace splitting already admits the complete final fragments/list together and
writes cased tokens directly. There is no per-word reservation or intermediate
cased source left to remove. [Source gate](source-gate.md).

Historical D36 allocation ticks attributed35.72% to64B PyString shells,18.44% to
byte arrays and18.09% to32B boxed BigInteger. This sampled traffic is neither exact
counts, live memory nor CPU; the source predates later call/sequence improvements.
The current token construction still creates separate wrappers/byte arrays, so
eliminating that storage phase is worth assessing. Do not call these current
percentages. Separately retained R63 CPU leaves have1,086guest samples/5.47CPU s;
inclusive frames are not additive saved-CPU estimates. Original revision-labelled
evidence is linked in [experimental history](../../EXPERIMENTS.md).

## Failed conditions and cleanup

The planned single text-helper capture reused the existing external managed
allocation-tick collector, unchanged core/worker/fixtures/goldens and normal GC/
tiering. No forced GC, heap tracing, production counters or new runtime dependency.

- B431 stopped before root creation on the persistent disk-floor gate. Its original
  source/log remain; archive-verified redundant old extracts/core links were removed,
  retaining immutable VM and independently verified local archives.
- B432 stopped before guest requests/collector startup: the harness incorrectly
  required unloaded optional assemblies in Ready. Ready advertises core, worker
  and CoreLib; all staged auxiliary files had independent SHA checks. Original
  failed source/observations are archived.
- B433 verified/warmed the guest, but collector attachment failed because the
  Linux diagnostic socket exceeded108characters under the long owned TMPDIR.
  No allocation events or captured job were obtained. Original stderr/source and
  Ready are archived. A short owned alias was prepared as a correction.
- B434 stopped at staging: free275,062,784B versus required277,024,768B. The failed
  setup archives had consumed the additional staging headroom. No new mount,
  worker or collector was created. No further collection was launched.

All users started by B432/B433 joined; fixture directories were empty and owned
mounts unmounted. Failure archives were verified under the VM lease and locally.
No live collector or sole RAM evidence remains. [Assessment](assessment.json)
records all failures, cleanup, historical attribution and limitations. No source
decision is inferred from these tooling failures. Future measurement can resume
after correcting staging/socket setup; it must preserve these original conditions.
