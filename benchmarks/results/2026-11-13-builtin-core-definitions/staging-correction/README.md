# File-staging allocation observation correction

The corrected runtime cb8736ff/frozen5a184a1d passed Ubuntu .NET11 ordinary
tests, then full delivery43446552390 failed in .NET10:
FileStagingOwnershipTests.PartlyFilledTextBufferRefusesBeforeWorkingBatchAllocationOrPublication(false)
observed94592process-global allocation bytes against the65536copy-sized bound.
The original completed Ubuntu job log is preserved in checks.tar.xz.

The interval from the clock gate to the first refusal has no I/O or suspension.
Observe current-thread allocation and assert equal thread identities, preserving
the65536bound, first shared-memory refusal, empty destination and zero final
reservation balance. This excludes unrelated process traffic rather than raising
the limit. No runtime sources changed. Focused Release checks pass32cases on each
actual.NET11/10 target; build/test logs and exact fixture sources are archived.
Cross-platform, original stress and installed-package qualification remains pending.
No performance claim follows from this test-only correction.
