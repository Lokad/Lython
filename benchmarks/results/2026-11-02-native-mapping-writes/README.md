# B117: one native dictionary write invariant

2026-11-02, baseline4411fdae versus recorded uncommitted source. Existing-key
writes formerly probed storage three times through SetNativeItem, then
ReplaceStoredValue, then OrderedStorage.SetItem. Consolidate into StoreItem:
check prior refusal, TrySetExisting once, otherwise preflight capacity and AddNew.
Keep finally's size/version update if an adapter publishes before throwing.
GlobalNamespace replacement also stops checking membership before repeating
the same lookup. Preserve named-slot synchronization and its funding.

Both old helpers and redundant probes are removed, about28 net runtime lines.
No new backend, cache, raw reference, ledger or policy. IPyDictStorage/SetItem
remain: namespace and environment adapters genuinely use them. Equal keys retain
the original key object and insertion order; updates do not invalidate iterators
merely for value changes. Growth refusal precedes insertion; a funded existing
slot is never permission to continue an already-refused command.

## Complete-script comparison

Same independent-process protocol as B116, four complete scripts, two blocks,
three current/candidate/control workers; CSV also includes pre-B113 and its own
unchanged control.4s warmup and9 interleaved~200ms batches per worker. Exact
hashes, stdout/files, quiet gates and joined cleanup pass. Owned service179.048s
exits0; unit inactive and lease free. Compiler/startup excluded.

| Script | Candidate elapsed, blocks0/1 | Candidate CPU | Unchanged control elapsed / CPU |
| --- | ---: | ---: | --- |
| CSV/helpers |+4.03% /–0.75%|+3.92% /–1.08%|+2.14%/+3.22%; –1.19%/–0.41%|
| Text/helpers |–6.21% /–7.34%|–8.30% /–10.42%|–3.24%/–1.99%; –4.29%/–7.35%|
| JSON/helpers |+0.05% /–0.20%|–2.09% /+2.00%|+2.46%/+1.32%; –2.46%/0.00%|
| Helper report |–0.44% /–2.28%|+1.31% /–2.49%|–1.15%/+1.33%; –2.74%/–3.80%|

Text beats its unchanged control by about3% elapsed and3–6% CPU in both blocks.
CSV is effectively flat versus current controls; JSON/report are inconclusive.
CPU is externally sampled Linux TGID user+system ticks through command/response,
weighted across9 batches. It includes runtime/JIT/response, not guest-only CPU.
No allocation measurement or universal speedup claim. Preserve all adverse rows.

Candidate CSV CPU versus pre-B113 is+2.04%/+0.86%; legacy controls0.00%/–1.84%.
The original6–7% diagnostic debt is not disproved. These conditions indicate a
smaller possible2–3% regression versus legacy controls, accepted explicitly at
this boundary; stop targeting an exact old percentage with more local probes.
The substantial whole-script performance objective remains open.

Warm timing includes actual mediated I/O, fixture reset/full output checks and
previous-result cleanup. Last result/final compiled/process/fixture disposal are
outside batch timing and all awaited/joined. No new framework or repeated
CPython/compilation campaign; reuse B115's named external baseline.

## Correctness and delivery

Release137 internal and576 public cases per actual.NET11/.NET10 pass unchanged:
dictionary identity/order/iteration, native/protocol collisions and mutations,
namespace/environment adapters, growth refusal, prior terminal refusal,
retained aliases, cancellation and cleanup/budget reuse. No probe-count tests
or timeout/assertion relaxation. No public API or ownership contract change.

Full Windows/Ubuntu ordinary .NET11/10, original stress and installed-package
qualification PASS in [cached delivery41490838628](delivery-ci-status.json),
exact ffa3f6f8, covering B114+B116+B117. This does not qualify later prototypes.
Do not requery the terminal run. Publication/adapter pin stay postponed.
