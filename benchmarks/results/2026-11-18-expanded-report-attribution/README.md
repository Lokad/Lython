# Expanded forwarding report CPU attribution

B995 profiled `98da93f7d` / frozen `b62bfd52` on the quiet Azure F4as_v7
VM on 2026-11-18 UTC. The 1,024-record report used normal GC and tiering in
the verified 32-file .NET 11 RC3 worker. The complete script passed golden
stdout and output-file checks. Guest fixtures lived on RAM-backed storage.

| Capture at 199 Hz | Report |
| --- | ---: |
| Guest elapsed | 4.945 s |
| All-thread CPU samples | 1,002 |
| Resolved runtime ancestry | 973 |
| Unmapped leaves | 117 |
| Fixture/host stacks outside resolved runtime ancestry | 24 |

The nearest resolved runtime method was `MemoryGovernor.Reserve` in 139
samples. Its first resolved non-governor callers included argument
accumulation (30), keyword decoding (29) and dictionary capacity growth
(19). The overlapping expansion and binding group includes 579 runtime
samples and encompasses callee work. Formatting (83), split construction
(54) and right-trim (31) also recur. Staged text output appears in nine
stacks and all host I/O in 27. The output handle already batches writes.
Inlining and incomplete stacks limit attribution. These samples do not
quantify an exclusive CPU share or establish a speedup.

The next source pair tests deferring the call's duplicate index when the
first keyword source is an exact dictionary. Its unique names can remain
in the existing argument storage unless a later keyword source needs a
collision check. Decoded names still serve downstream binding. The probe
does not change host I/O, frame construction or the budget contract.

Both quiet gates passed and all users joined. The RAM trace was archived
and checked under the lease before unmount. The preserved procedures include
the storage preflight and proofs for two retired archived observation
replicas.

[Attribution](attribution.json) · [Raw observations](observations.tgz) ·
[Image identity and procedures](procedures.tar.xz)
