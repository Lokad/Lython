# E3b2 mapping extraction — before code

Baseline612f7755/frozenf2078b20; view delivery42124145716 pending, Counter terminal
42119563175 GREEN/all8 cached. Independent prior probes demonstrate wrong rest,
duplicate-key acceptance, interleaved key/subpattern evaluation and missing length
short-circuit. Preserve all original cases, CPython goldens and source snapshots.

Implement one synchronous extraction sequence shared by normal/async hosting:
1. Exact native PyDict subject and length guard before any key getter. Preserve
   current mapping-type support and sync-only key/property callback boundary.
2. Admit one object[2*N] using existing PyList.EstimateCapacityBytes(2*N). This
   pays metadata, references and conservative scalar-box headroom, rather than
   only reference slots. Large magnitude/object payloads retain independent owners;
   captured aliases transfer into existing admitted binding/frame/result storage.
3. Allocate/admit an existing private PySet and its known capacity before key
   callbacks. Evaluate ALL keys first, then for each key perform Python seen
   contains/add and dictionary get, storing values before any subpattern.
   Contains detects duplicates; Add's false result alone must not raise, since
   Python allows effects between these two operations. Keep current provenance
   explicit and check cancellation/first refusal after actual callbacks.
4. Match subpatterns against captured values. Copy the current native subject
   into rest only after success, reusing its stored hashes. Remove matched keys
   in original order; preserve KeyError if callbacks removed a key before rest.
5. Keep a rest result funded on publication; refund only a fresh unpublished
   copy on failure. Always ReleaseUnpublishedStorage on the private seen set and
   release the temporary buffer after all synchronous users. Existing cleanup
   must run even after first admission refusal. No new ownership subsystem.

Code removed: raw semantic HashSet/newPyValueComparer, interleaved foreach,
rehashing/filtering rest construction and missed duplicate behavior. Expose the
existing PySet.EnsureCapacity internally for this known-size construction; no
new backend, options, ledger, tracing, caps or dispatch framework. Empty patterns
reuse the rest-copy helper without allocating extraction/seen storage.

Contract tradeoff: conservative per-slot headroom and private working capacity
may deny earlier. Identical tiny thresholds are not a requirement. Preserve large
payload aliases; scalar/data owners are not refunded with scratch storage. Do not
claim exact heap reachability or support suspension during pattern extraction.
General generated-dataclass storage callback coupling remains a separate source
problem; this does not silently claim the backing comparer is fixed.

Acceptance: independent CPython goldens for rest/custom-equal keys, dynamic
numeric/custom duplicates, missing-key/short-subject ordering, all keys before
lookups/subpatterns, captured-value behavior after mutation, rest KeyError/order,
and empty/native cases; both APIs. Focused existing match/key/ownership families.
White-box preflight-before-getter, native swallowed refusal/cancellation, private
scratch recovery after mismatch/error, and large captured aliases remaining funded
through source clear and delayed body I/O. One short valid native pattern/control
comparison; incorrect custom-key baselines excluded. Full platform/stress/package
qualification batched after coherent delivery, not after each small correction.

## Focused validation and containment correction

Nine isolated CPython3.13.2 goldens pass in both APIs, including captured values
after mutation, rest deletion KeyError and exact custom hash order. Eight of ten
initial ownership cases passed; two native structural-field callbacks still read
field b after field a swallowed refusal/cancellation. Preserve that failure.
ReadField/ReadFieldAsync now check the active execution after actual attribute
completion; subsequent fields never run. General storage hash coupling is still
open, not claimed fixed. All ten ownership cases pass, including known-size
preflight before getters, scratch reuse over four executions of each pattern,
large captured aliases after source clear and genuinely suspended body cleanup.
Zero-warning/error build; focus770public/76internal per actual11/10 passes.
View delivery42124145716 failed one stale TypeError golden on both OS; independent
Python proof and corrected fixture in a39d6ba5, runtime behavior retained. Full
replacement delivery remains pending. No terminal CI status requery.
