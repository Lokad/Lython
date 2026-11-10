# Reassessment of bulk CSV record construction

Read-only assessment at 32d1ea1a, 2026-11-10. No new timing, runtime prototype or
qualification campaign. Compare the exact [B74 patch](../2026-11-01-csv-records-rejected/README.md)
with the [subsequent retained constructor](../2026-11-02-native-csv-records/README.md)
and current source. B74's 6–8% gains missed rigid thresholds; its adverse text
+4.16% / +1.28% observations remain valid historical uncertainty, not a design veto.

The revised constructor was already retained and qualified. Current native-header
construction admits whole capacity once, uses one ambient scope and direct stores,
handles duplicate/short/extra-width records, and preserves the dynamic-key route.
It removes the cursors, per-field wrappers and incremental growth identified by B74.
Later changes adopt funded row arrays and share immutable record keys. Native file
input is already parsed/compacted in its owned buffer. Reapplying B74 would repeat
work present in production; no confirmation run is justified for that old patch.

Reentry and retained aliases remain real constraints: shared-key adoption checks
header identities before and after admission. Admission may mutate headers or
reenter the reader. Skipping the second check is not safe. PyList has no mutation
version suitable for replacing these walks; adding global list-version machinery
would impose new state and mutation work beyond this CSV use. No such cache or
counter is introduced. Changed headers retain their key identity/order and earlier
rows keep independent values and funding.

The existing broader pipeline includes parser construction, not just dictionary
packaging. R182's 14.41% parser/construction attribution is not removable overhead.
The narrow assessment stops here: the historical opportunity has shipped, and a
new proposal must identify additional intermediate storage or construction work
before editing. This does not permanently close record design work or require a
fixed percentage. Preserve duplicate/custom/wide headers, reentry, retained rows,
preflight rollback, cancellation and terminal refusal in any later redesign.

[Exact current sources](sources.tar.xz), [identity](source-identity.json).
