# Regex cache slot reuse and cancelled admission

`5afc8500d` stops unpublished regex compilation when cancellation arrives
during cache-slot admission. It refunds the pending cache charge, closes
the unpublished provider handle and relinquishes the copied name storage.
The targeted regression on `ae8377368` completes without raising
cancellation. On the new core it reports cancellation with an empty cache
and restores the budget while the execution owner is still live.

The LRU now stores the entry directly in its list node and reuses the
retired node on eviction. Requested flag keys, recency, canonical
publication after reentry, terminal admission refusal, purge refunds and
retained aliases keep their existing behavior.

Windows .NET 11 and Ubuntu .NET 10 each pass 99 white-box and 664 public
focused Release tests. Both frozen worker images match three CPython
goldens with the optional PythonRe 0.4.0 adapter. A Windows allocation
diagnostic runs two batches of 640 named patterns, purges each batch and
uses an earlier retained pattern after purge. It allocates 49,560 fewer
managed bytes per script, 0.043% of its total. Compiling 64 named rules
uses 1,536 fewer bytes, 0.40%. The ASCII group-text control moves by 24
bytes. All three admitted peaks match and shared allowances return to
baseline. No elapsed comparison was made.

[Measured sources and images](source.json) · [Local allocation and ownership](allocations.json) ·
[Source patch, goldens and regressions](procedures.tar.xz).
