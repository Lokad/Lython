# B349 setup storage guard

Initial setup stopped at its16MiB post-setup headroom assertion. The pre-extraction
guard passed, but directory/link/metadata creation consumed the small remaining
margin. Observed free16,699,392B; required16,777,216B. No service was submitted:
authoritative unit LoadState=not-found/ActiveState=inactive/MainPID=0. No worker,
profiler, guest fixture or measurements existed. Original payload/contract unchanged.

Retain that failed condition. Verify and remove only the old B166 duplicate transfer
(local payload ec6876f6, surviving raw archive04189bb4), preserving runtime images,
dependencies and evidence. Revalidate the prepared B349 image and the unchanged
16MiB guard, then submit its first service. This is preflight recovery, not a
replacement for an observed live run or a weakened measurement gate.
