# R260 source findings and refinement

First constructor/Value setter form cached own slot and wrote it even when inactive;
it also removed Resume captured refresh.1073internal/1524public pass, existing
nonlocal-directive comprehension fails. That form is reconstructed by edit.py plus
saved originals; no timing claim. Original TRX/log retained.

Independent frozen7114/0e715 probe confirms latent comprehension corruption:
outer slot readsouter while a separate closure reads2;CPythonbothouter. Retained
nonlocal sync must respect existing inlined binding precedence. Existing test kept,
new independent reader added. Deletion follows the same lexical barrier.

Source review: inactive frame can be retiring pooled workspace, not just suspended.
Preserve existingactiveowner guard; no new lifetimeflags/cleanuporder change. Value
setter owns active localcache and conditionalnamespace; StoreThroughOwner removed,
slot metadata immutablefromcreation, namespace-backed cells get no ownslot. Normal
localstores avoid duplicate cache/mirror publication.

Generator initialprobe matches9 beforechanges (oldcapturedrefreshalreadyworks).
Second baselineprobe discovers mirrored generatorclassnonlocal stale slot:oldvs
Pythonnew. Both original and firstcandidate fail; this is independentexistingbug,
not proof removal broke previous behavior. Mirrored resume reuses namespaceSyncAll,
plain capturedresume retainsoriginalrefresh; cooperativechecksinsharedlongsync.
Set/delete publictests bothAPIs+budgetreuse and isolatedPython3.13.2goldenspass.

Final9184focusedactualtargets,3correctedprobes and3complete scriptsbothAPIs all
outputs/balances/cleanup pass. Allocationflat. All desktoptimings/adversity preserved;
B606four-job targetedquiet sourcepair decidesretention. No newscalarrepresentation,
GC/tracing/owner/cacheframework/quota/cap/policy. Hypothesis does not assert CPUgain.
