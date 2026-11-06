# Consolidating the hidden local-read methods

Removing ExecutableFrameState exposes two same-signature methods: slot-only read
and ExecutionContext's lexical/inlined-scope wrapper (formerly internal new).
Initial duplicate-definition build failure is retained. Keep distinct meanings:
TryReadExecutableLocal reads actual slots/cells; TryResolveOwnLocal retains lexical/
inlined handling. Enumeration and formerly typed frame assertions use the slot
reader. No recursive dispatch or silent scope change after removing inheritance.
