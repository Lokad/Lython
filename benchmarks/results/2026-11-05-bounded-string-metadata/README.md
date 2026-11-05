# Bounded string metadata, bulk algorithms withdrawn

2026-11-05, final measured core3fa1389d over retained9c1d/6cb05f8e. Keep the
correctness change: one existing scalar-width walk now computes Python character
or CLR UTF16 width with bounded cancellation ranges and checked chunk totals.
Native len forwards its current token/span, projection forwards its own token,
and prior admission refusal wins before later cancellation. String storage,
scalar-length cache, conservative capacity and ownership remain unchanged.

Removed the separate local CountRunes and four-way UTF16 loops. No lookup table,
custom SIMD, ASCII strategy, new cache, saved token, owner or execution counter
remains. Existing GetRuneLength still serves real index/slice/traversal consumers.

## Preserve the adverse experiments

| Complete file script | Initial SearchValues algorithm | BCL UTF8 refinement |
| --- | ---: | ---: |
| ASCII character count,1MiB/128lines |−51.02% /−50.93%|−51.59% /−52.46%|
| Mixed Unicode,458880B/128lines |+306.25% /+305.75%|+36.57% /+37.09%|
| Short Unicode,32768B/4096lines |+7.40% /+1.94%|+13.70% /+6.87%|

Each cell compares two identical-current processes to its own reference. Both
five-job collections complete146.77s with quiet/identity/golden/lifecycle gates.
Text/ordinaryCSV controls and all adverse signs are preserved in initial/ and
refined/. The BCL refinement removes both lookup tables but retains a substantial
Unicode cost. **Both performance algorithms withdrawn**, including the apparent
ASCII gains. This is evidence-led withdrawal, not a fixed threshold or permanent
ban; no continued rescue/timing campaign is proposed.

## Final correctness-only diagnostic

One three-job check of the shared scalar traversal,91.80s, all gates. This misses
the90s target by1.80s but stays inside the original120s owned service bound; no
deadline relaxed, work abandoned or sample discarded. All workers Closed, final
reservations zero, processes joined and fixtures removed.

| Complete file script | Current ms | Identical-current ms | Reference ms | Changes |
| --- | ---: | ---: | ---: | ---: |
| ASCII character count |0.5153|0.5124|0.5115|+0.73% /+0.16%|
| Mixed Unicode |0.5307|0.5319|0.5293|+0.28% /+0.50%|
| Short Unicode |0.6224|0.6232|0.6270|−0.72% /−0.60%|

This catches gross regression; it does not establish a CPU improvement, portable
equivalence, allocation reduction or closure of older performance debt. ASCII
current IQR2.37% further limits precision. The substantive performance objective
remains open.

All scripts read a file, sum len(line), write the complete total and print it.
Goldens independently verified with isolated local CPython3.13.2 and VM3.13.16.
Same frozen workerb94/auxiliaries, ordinary GC/tiering, quiet Azure F4as_v7 VM.
Five-second engine warmup/job and seven rotated~150ms samples. Same mediated
/dev/shm I/O, invocation, full validation and prior-result cleanup included;
compilation/startup, staging/reset, transport and final owner/process/fixture
teardown excluded. Exact source snapshots, catalogs and observations are archived
for each variant. No new benchmark framework or backend qualification.

## Correctness, delivery and limitations

Final build17.67s, zero warnings/errors. Focus529internal/2037public per actual
.NET11/10; full4124internal/16001public per target pass. Eleven new small cases
cover scalar-width extremes, supplementary text crossing chunk boundaries,
cancelled metadata reuse, projection tokens, exact astral admission, first refusal
and cleanup to baseline. Existing retention/cancellation/codec/CSV checks remain.
Final platform/stress/installed-consumer delivery pending; publication postponed.

Cancellation checks bound traversal when a current token is supplied. Native len
and projection supply it; context-free callers have no host token. Unit entry/
forwarding tests and bounded source checks are **not an in-flight latency
measurement**. Final CLR decoding remains synchronous. There is no hard latency
promise, hidden allowance or new cancellation policy. Cached strings retain no
caller's token, and cancelled computation does not publish a partial count.
