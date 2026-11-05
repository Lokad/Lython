# Native dictionary backing and complete record construction

2026-11-05. **Retain** the completed source over d644b5cf, measured final core
**1f5919c0**, versus **6140b97c/e5017ce4, measuredc0221b77**. Committed
**e958c9b1** passes full Windows/Ubuntu, actual.NET11/10, original
stress on both targets and installed Release package consumers. The terminal
[CI42005719439 receipt](delivery-ci-status.json) is cached; no unchanged replay.
Publication postponed; performance work stays open.

One managed entry array contains ordered payloads and hash-bucket heads. Remove
the separate OrderedDictionary object/bucket array and capacity field; count
occupies that integer. Private mixing preserves public hashes and spreads integer
keys. Existing-key update and admitted insertion share a hash/probe. Live namespace
bridges and Python protocol-key side tables retain their behavior. No second native
representation, pool, owner layer, policy, unsafe code or runtime dependency.

Growth admits replacement overlap and copies privately before publication. Existing
conservative shell/scalar-capacity estimates remain. Power-of-two ordinary capacity
and actual CLR representation bounds can alter admission amounts; known tiny
thresholds are not a compatibility promise. Removal/clear retain capacity funding;
aliases and pending users keep their owners. Refused admission remains terminal.

## Complete-script source comparisons

Quiet dedicated F4as_v7/Ubuntu24.04.4/.NET11RC3, ordinary GC/tiering. Existing
three-image driver, two unchanged baseline roles, identical auxiliaries/CoreLib/
hash witnesses. Six guestwarmup seconds/image, two settle seconds/job, nine rotated
approximately200ms batches. Full invocation, mediated RAM-file I/O, full stdout/
file goldens and result cleanup included. Fresh adds Compile/Dispose; reset,
startup, hashing and final cached teardown excluded. Process allocation includes
reset/background work. Actual users finish before owner/fixture cleanup.

Initial **4b61edd7**,161s/six jobs, all gates: nativeCSV warm3.91–5.86% lower,
ordinary CSV warm5.86–7.05% lower, fresh4.47–5.34% lower, JSON4.72–5.79% lower;
allocation−3.3–14.8%.
Report elapsed−0.55/+0.98% is inconclusive and allocation is **+5.13%**. Preserve
that adverse result. JSON's one-entry hint became a two-slot table; each three-
field report object then needed another backing and growth loan. Source and the
extra approximately64B/record explain the changed construction work.
[Initial plan/raw](initial/observations.tgz), [all metrics](initial/assessment.json).

One refinement starts unknown-width nonempty JSON objects with a four-slot block;
empty objects remain empty and other known dictionary capacities remain compact.
This removes intermediate two-slot storage for three-field records. Two-field
JSON records retain more capacity, reducing their initial allocation saving.
Refined **3384dd91**,160s/six jobs, all gates:

| Complete job | Elapsed vs baseline / control | Baseline spread | Allocation vs both |
| --- | ---: | ---: | ---: |
| Native-helper CSV warm |−4.16% /−7.35%|3.45%|−5.20–5.22%|
| Ordinary-helper CSV warm |−8.45% /−6.44%|2.15%|−3.44–3.46%|
| Ordinary-helper CSV compile + run |−5.62% /−7.11%|1.61%|−3.32–3.35%|
| JSON summary warm |−4.09% /−4.28%|0.19%|−6.90%|
| JSON-to-Markdown warm |−1.94% /−6.13%|4.46%|−4.49%|
| Text control warm |+0.09% /−2.78%|2.96%|essentially unchanged|

JSON has the clearest elapsed control. CSV is favorable against both roles, with
control variation limiting magnitude; report/text elapsed remain inconclusive.
No causal CPU attribution, additive gain, new CPython ratio, RSS/startup or
embedding-latency claim. These construction timings precede the final removal
completion below and do not measure its exact final binary.
[Refinement contract](construction/pre-code-plan.md), [metrics](construction/assessment.json),
[frozen source/catalog/raw/gates](construction/observations.tgz).

## Ordered removal completion and limits

Source review before retention found a new complexity cliff: the initial table
rehashes all capacity after every removal, including the last entry. Complete
the invariant by unlinking the key and shifting only the ordered suffix. Chains
descend through insertion indices; moved heads/links adjust once, physical bucket
heads remain in place, and clearing a payload preserves its bucket. This removes
the full capacity sweep; last removal needs only its bucket-chain unlink. No
allocation or guest callback occurs during structural mutation. Probing/private
growth check cancellation; individual CLR allocations and atomic structural
mutations are not internally interrupted, as with the previous BCL boundary.

Final **1f5919c0**,54s/two jobs, all gates, versus originalc022: nativeCSV allocation
−5.20–5.22%; a CSV job prefixed with constructing/draining an8,192-key cache
allocates **17.38% less**. Elapsed points are3.38–9.38% lower and4.64–13.38% lower, but
unchanged controls differ **6.62%/9.16%**. Keep elapsed **inconclusive**; favorable
points do not resolve that instability. The cache job is a diagnostic extension,
not a new CPython baseline or general large-dictionary claim. Retain for the
complete storage/probe removal, construction results and semantic checks; resolve
remaining timing uncertainty at a process-isolated milestone, not repeated local
tuning. Larger entry width/capacity rounding can cost space for some large maps.
[Completion contract](completion/pre-code-plan.md), [metrics](completion/assessment.json),
[exact final source/driver/raw](completion/observations.tgz).

## Validation and preserved conditions

Final nonincremental Release54.06s, zero warnings/errors. **4,111 internal and4,655
relevant public cases per actual11/10** pass, covering dictionaries/protocol keys,
CSV/JSON/hooks, scopes/classes/generators, real async users, cancellation, terminal
refusal, retained aliases and allowance reuse. Two independent regressions cover
colliding nonreflexive-key identity/order through growth/removal/copy/drain, and
cancellation requested during hashing before a collision scan. They assert
semantics/funding, not layout, fuel or exact checkpoint counts. All previous
source variants' logs remain; no assertion, timeout or containment bound relaxed.

All three captures independently check CPython3.13.16 full output/files, identities,
normal runtime, quiet endpoints, frozen/finalzero balances, joins, fixture removal
and free lease. Archives contain source snapshots/patches, full catalogs and driver.
CSV has4,096 logical records despite quoted newlines; JSON has4,096 records plus
1,024 blank lines; report has3,072 records. Input byte counts remain unchanged.

VM storage maintenance removed only hash-verified archived raw/upload duplicates
and hard-linked four byte-identical historical cores, preserving paths/bytes/modes.
One earlier link attempt refused differing modes before mutation. One private
driver-adaptation marker was corrected locally before any timing; no capture was
discarded. [Validation/identities/limits](validation.json),
[all original/final logs and maintenance receipts](validation-logs.tgz).
