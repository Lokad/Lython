# Current CPU attribution and record-storage gate — R19

No speedup or new runtime implementation is claimed. This closes the missing
current attribution after the argument/context/dictionary simplifications and
selects the next coherent storage investigation, rather than another call arena.

2026-11-06, quiet F4as_v7 / Ubuntu 24.04.4 / .NET 11 RC3. Reuse the checked b94
worker and frozen b6650f09 core54b53c7f; the non-regex paths are unchanged by U04
b542b601. Existing text/helpers and JSON-to-Markdown report, mediated RAM I/O,
full result checking and prior-result cleanup. Warm execution only, no compilation
profile or CPython speed comparison. Normal GC/tiering; diagnostic
`DOTNET_PerfMapEnabled=3` emits maps without duplicate jit dumps. Profiler effects
and inlining limit attribution; do not subtract these shares from older captures.

| Leaf group | Text: 1,303 samples / 6.56 guest CPU s | Report: 1,460 samples / 7.35 guest CPU s |
| --- | ---: | ---: |
| Instruction prefix, including inlined work | 20.64% | 5.14% |
| Ordinary calls/binding | 1.84% | 1.92% |
| Native guards | 1.77% | 4.86% |
| Native evaluation | 3.15% | 7.81% |
| JSON string/container reading | — | 11.92% |
| Formatted-string append | — | 5.21% |
| Local-result publication | 3.99% | 1.30% |
| Monitor entry/exit | 4.60% | 5.27% |
| Exact integer addition | 2.69% | 0.82% |
| Unknown | 5.14% | 6.10% |

These are selected exclusive leaf groups, not complete subsystem costs. Prefix
samples include inlined operations; they do not prove dispatch alone costs 21%.
Library work includes essential semantics. Calls outside the selected symbols can
be inlined or attributed elsewhere. Keep all leaf names/counts in assessment.json.
The data does not justify a new argument arena, scalar convention or a predicted
percentage gain. Native guards must still precede effects; values read during
proof cannot be reused after an admission hook may have mutated their source.

## Selected source decision

JSON string/container work is a material report component. Source confirms a
remaining representation cost: the current key hint shares `PyString` names but
each record still constructs a full 40-byte native entry per capacity slot,
including repeated key/hash/chain/bucket/sequence metadata. CSV already transfers
its row array and DictReader avoids a guest list; do not replay the old input-copy
or bulk-admission proposals. Canonical dictionaries currently use one coupled
entry array, so sharing key metadata requires a real storage change.

Select the [immutable record-key storage gate](selected-record-layout.md). First
separate canonical native key metadata from row values, then share immutable
layouts through the existing execution owner and retire the duplicate JSON hint.
Do not bolt on a serializer-only mapping backend. Read/profile attribution is
supporting evidence, not proof that table construction dominates or a speed claim.
Completion requires semantic/ownership checks and complete affected scripts with
ordinary-dictionary controls, including any regression from a second backing array.

## Captures, cleanup and original failure

B358 collects 14.41 s of measured guest work in one bounded owner. Full before/
after goldens, exact core identity, active guest thread attribution, Closed/exit0,
immediate before/after quiet gates and fixture cleanup pass. No SSH/status query
occurs before the 150 s owner plus 30 s stop bound. No further capture scheduled.
Runtime temporary maps are copied/verified before their exact redundant copies
are removed after worker join. Data, maps and output remain in persistent storage.

The original owner exits1 only while archiving root-owned0600 perf.data. Preserve
the original archiver, journal and partial archive. After all users end, transfer
only the two owned sample files to the collection user and rearchive under an
exclusive recovery lease. No capture is repeated. The verified recovered archive
and its receipt explicitly retain originalOwnerExitCode1 and the recovery status.

Setup first encountered read protection on an older root-owned profile copy.
Use privileged hashing to verify that exact file against independent local/remote
archives before retiring redundant expanded copies. No sole evidence, current
image or archive was removed; profiler headroom exceeded 10 MiB before launch.
Source scripts, initial failure and checked setup remain in source-and-check-history.tgz.
The whole-script performance objective and independent regex work remain open.
