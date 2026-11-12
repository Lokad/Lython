# Shared-allowance admission frequency — R283 source gate

2026-11-12, committed9bca1337/frozene4cc65b2, actual.NET11RC3. One untimed
diagnostic pass over three unchanged complete scripts, both public APIs. Reuses
the existing fixed comparison host/fixtures and a public allowance wrapper;
production code, ownership semantics and producer pins remain unchanged.

| Complete command | Reservation resizes | Growth / refund | Growth≤256B /≤4096B |
| --- | ---: | ---: | ---: |
| CSV validation,5120records | 13398 | 13375/23 | 8239/5135 |
| Captured text,3072lines | 6229 | 6212/17 | 4149/2059 |
| UTF8 report,3072records | 14425 | 14393/32 | 11288/3087 |

The≤256B column includes the≤64B bucket; the≤4096B column excludes≤256B.
Both APIs produce identical counts for each script. Four scopes/reservations are
created per execution; this is not thousands of reservation handles. Consumer
Bytes reads are zero in these ordinary runs. The wrapper itself reads Bytes to
classify resize deltas, adding locks/objects: no elapsed, CPU or allocation claim
follows. Existing sampled monitor costs include necessary shared synchronization
and other work; counts cannot be multiplied into a removable-cost forecast.

All independent stdout/files and post-execution compilation baselines pass; final
compiled-owner balances reach zero and owned fixtures are removed. No forcedGC,
quota/checkpoint counter, runtime option or public API change. Initial launcher
omits framework selection, then a private-wrapper primary-constructor warning
stops its build. Both failures survive; no complete command ran before correction.

**Stop this diagnostic without a production prototype.** Current CSV row, split
word and frame preflights already combine their storage. Source/call counts do
not prove duplicate funding that can simply be deleted. Amortizing all governor
resizes would require keeping extra funded capacity, a second balance and new
prepublication/reentry/refund invariants. It would also change refusal/retention
thresholds. That may be a valid later tradeoff, but counts alone do not justify
adding this mechanism under a simplification priority. No funding cache, page
allocator or extra ownership layer is selected automatically; no permanent design
ban follows. Keep effective preflight, terminal first refusal, pending/retained
ownership and shared-budget reuse. The broader performance objective remains open.

[Raw counters](observations.json) and diagnostic sources/build/launch logs are
archived with hashes. No VM collection or repeated platform/package qualification.
