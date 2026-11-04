# B196 one miss-path refinement, before code

Initiala6b2bfb4 completes14 jobs/273.583s capture (274.196s service), all original
quiet/golden/ownership/close/join checks. Exact patch/raw archive retained,
SHAf83ecbd64bf58463bf8e593668d896b207471f8d93cef596a853c176ba3eef71.
CSV/JSON allocation-2.6..4.1%,text-1.64%,report-0.69%,small control-50%.
Synchronous text-3.0..4.3% with1.3%old spread; async text+1.1..5.4% with4.1%
old spread. **Above-range counters reliably adverse3.4..4.5%sync/4.1..6.0%async**,
old spread1.0/1.8%,candidateIQR0.20/0.87%. Not an unstable-control excuse.

Untimed local owned-worker JIT inspection shows the BigInteger creation helper
Tier1 body remains351bytes, with two generalized integer comparisons, explicit
conversion, call frame/spills and boxing path. This is Windows/synthesizedPGO,
not VM CPU attribution. Initial diagnostic omitted verify, then used a fixture
outside the required temp parent; both errors retained, corrected protocol/layout
only. Ordinary VM measurements never use JIT overrides.

One justified source refinement: compute a candidate small index with managed
int.CreateTruncating(BigInteger), one unsigned range test, and exact equality ONLY
for apparent hits. Huge/negative values with matching truncated low bits must
miss; preserve exact BigInteger output/magnitude ownership. This avoids generalized
ordering checks on ordinary misses. Permit inlining for this small creation
boundary, no special opcode/counter whitelist, no new representation or Unsafe.
Existing-object/float/alias paths stay unchanged. Add independent large low-bit
collision semantic values to the public regression before freezing.

One four-image comparison: originala6b2, refined candidate, two unchanged B194
roles. CSV/JSON sync, text bothAPIs, above-range bothAPIs;5s warm/13~200ms rounds,
same protocol/goldens/gates plus5s cleanup settle. ~230s owned under300s. Separate
eight-case allocation diagnostic (B194/refined/B194) reuses original tooling;
original allocations already preserved. No new CPython campaign. Retain/refine
decision assesses miss cost, whole jobs, source simplification and allocation
separately; no fixed percentage veto. Further unchanged retiming is not planned;
new substantive evidence remains eligible for bounded future work.
