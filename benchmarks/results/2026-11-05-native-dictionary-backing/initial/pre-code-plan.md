# D40 complete native dictionary backing: bounded selection contract

2026-11-05, parent e5017ce4 (production6140b97c/corec0221b77). Design candidate,
not an implemented or performance-qualified replacement. Finish current delivery
independently. No additional trace/capture is needed for this source selection.

Evidence: D36 ordinaryCSV allocation samples include PyDict5.77%, managed
OrderedDictionary3.92%, Entry<object,object>[]5.56%, Int32[]2.23%. These sampled
types are not CPU shares or exact counts. Int32[]40B can be the managed table's
bucket array as well as a rune-position table: do not attribute it exclusively to
text. Current PyDict.Storage owns one BCL OrderedDictionary; the wrapper allocates
its own entries and buckets. Every ordinary CSV data record creates a dictionary.
Native JSON/report construction likewise produces many small dictionaries.

Hypothesis: one native ordered-table backing embedded in PyDict can remove the
separate OrderedDictionary carrier and bucket array. This addresses complete
per-record construction, not a CSV-only fused parser or another call selector.
Do not predict a CPU speedup from the allocation inventory.

Current SetNativeItem/TrySetNativeExisting followed by BCL Add also repeats the
lookup/hash probe for a missing key. A complete replacement can probe once,
admit any growth, then insert using that cached structural hash. Keep outer
Python protocol dispatch, mutation/version handling and first-refusal semantics;
growth must not repeat a guest hash/equality or lose the original equal key.

Candidate invariant: one array of entries containing key, value, cached structural
hash, collision link and bucket head. The same array carries hash buckets and
ordered entries; count belongs to PyDict. Derive capacity from the array, removing
_nativeCapacity. _nativeCount replaces that integer; no new storage-owner wrapper.
Empty arrays may be shared. Existing IPyDictStorage live namespace bridges and
protocol-key side tables keep their current semantics. One native representation
for all sizes, no small-map mode, alternate backend, pool, cache or policy switch.

Displaced implementation: OrderedDictionary construction/EnsureCapacity/IndexOf/
GetAt/SetAt and its separate object+bucket backing in native PyDict. Replace its
complete storage operations in PyDict.Storage.cs; adapt the narrow ownership
adoption/index callers in PyDict.cs. Keep conservative existing major-capacity
and scalar headroom first; admit whole replacement overlap before allocation,
populate privately, publish once and relinquish old private storage afterward.
No identity ledger, tracing, collector dependency or hidden allowance.

Tradeoffs needing a decision before code:

- Entry width grows when bucket metadata shares each slot. Wrapper/header removal
  favors small records; larger dictionaries may use more physical space. Exact
  known sizes versus rounded geometric capacities affect both admission and hash
  costs. No tiny-budget threshold compatibility promise or new input ceiling.
- A custom hash-table algorithm adds maintenance responsibility compared with
  the BCL. Source selection must show a compact complete implementation rather
  than replacement dispatch/rehash frameworks. If complexity outweighs plausible
  whole-script gains, do not implement merely to tick a roadmap item.
- Ordered removal must preserve all bucket links and insertion order, including
  duplicate/equal keys. CLR/BCL structural mutations currently have atomic
  cancellation boundaries. New collision/copy loops need effective cooperative
  checkpoints; cancellation/refusal must leave valid retained storage, with native
  invariant cleanup finished before propagation. Do not conceal a new limitation.
- Keep exact comparer behavior, structural/frozen protocol hashes, no protocol
  redispatch from raw probing, live views/iteration versioning, mutation/reentry,
  retained key identity, NaN/numeric equivalence and copy/clear ownership.

Semantic acceptance: existing dictionaries/sets/protocol keys, CSV mutable headers
and shared-reader order, JSON hooks/duplicates/config/report, namespaces/imports,
generators and true async callbacks, cancellation/refusal, retained aliases,
construction rollback and budget reuse on actual11/10. Add independent collision/
ordered-removal or suspended ownership regression only for an uncovered gap;
no tests asserting a chosen field layout or exact allocation threshold.

Bounded comparison if selected: existing three-image/two unchanged-role driver,
ordinaryCSV warm/fresh, nativeCSV warm, JSON warm, report warm, text control; six
jobs<=180s owned. Exact sources/auxiliaries, normal runtime, quiet/fullgolden/
identity/balance/join gates, complete RAM-file I/O/result cleanup; no immediate
CPython replay. Preserve adverse and large-dictionary costs and choose retain,
refine, withdraw or explicit debt. A useful smaller construction result is not
completion of the substantial whole-script objective. Full platform/stress/
installed checks follow a coherent retained checkpoint, not this design gate.


Selected initial prototype: one32B-entry array, count in PyDict replacing its
capacity integer; no storage wrapper or second table. Power-of-two ordinary
capacities, with the actual CLR maximum as final representation fallback; mix
the existing32-bit structural hash privately before masking, preserving equality
and public hash values. Entry payloads occupy the insertion-order prefix; bucket
heads share those slots without moving their bucket positions during insertion.
Growth copies payloads/rebuilds links privately under existing overlap admission.
Removal shifts ordered payloads then rebuilds bucket links, remaining one atomic
structural mutation as with the existing BCL backend. No guest work/callbacks
inside that phase; token checks cover probe collisions and private growth. Do
not claim interruption within CLR allocation/atomic structural mutation. Keep
the existing conservative shell/per-slot estimates for this first source pair,
so this is storage/probe construction work rather than an accounting-rate trial.
Native existing-key update/missing-key admission/insertion share one hash/probe;
outer Python protocol and version/refusal handling remain. Reassess added custom
table maintenance responsibility against whole-script results before retention.
