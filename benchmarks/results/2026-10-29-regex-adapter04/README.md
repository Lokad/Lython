# Optional regex adapter refresh

Candidate `d005e85c4728a6cada56e00316ddeec211cffbbc` uses the published PythonRe
0.4.0 package and its exact Core 0.4.0 dependency. Control
`4b408097ebca427dc12ab186259c7bb88c0224a0` uses 0.2.0. Core runtime sources are
identical; the optional adapter, dependency versions and related checks differ.
The core NuGet package remains independent of Utf8Regex and the adapter remains
IsPackable=false. No adapter package is produced.

## Removed mechanisms and current ownership

The adapter no longer rejects cancellable operations, buffers every future match,
rejects supplementary-Unicode enumeration, guesses lastindex, repairs fallback
coordinates or interprets `.*?` and its replacement syntax itself. Backend cursors
handle empty/nonempty progress and supplied byte ranges/capture metadata.

Each cursor borrows the subject and funds its own current working scope. A scoped
registration forwards the current advance token into its owned cancellation
source; registrations and backend users finish before refunds. A non-ASCII
subject funds one byte-to-scalar map and captures borrow original byte slices.
Replacement-plan cache capacity stays on the longer-lived pattern owner rather
than being refunded with temporary substitution storage. Coarse high-water
estimates may reject early and require incidental backend/framework headroom;
they are not a certified backend heap/RSS envelope. No tracing, new allowance,
work/time policy, retry or background matching is introduced.

## Focused qualification

Release builds for both TFMs have zero warnings/errors. The focused regex,
frontend/provider, fnmatch and glob selection passes 77 internal and 327 public
tests on **Windows and Ubuntu, both net11.0 and net10.0**. Ubuntu
[build](build.log), [net11](net11.0.log) and [net10](net10.0.log) logs are retained.
Sixteen frozen CPython 3.13.2 goldens cover mixed/nested captures, last-closed
groups, scalar ranges, nullable Unicode progression, replacements, splitting,
invalid templates and effective flags across verbose/comment/scoped prefixes.

Five isolated real-backend cases cancel ongoing compilation, matching, cursor
advancement, substitution and splitting, check the original caller token and
return accounting to zero. A test-only controller cancels after entry; an owned
subprocess bounds a broken backend without abandoning work in the test host.
Separate controls cover cancelled old tokens on cached patterns/cursors,
streaming without cumulative charging, retained replacement plans, refused
admission and allowance reuse. Cooperating-provider contract checks remain
independent of these actual-adapter controls.

Windows full ordinary suites also pass on both TFMs: **3,882 internal + 15,016
public tests per target**. Original Windows stress also passes on both TFMs:
**3 internal + 406 public per target**. The established isolated Windows
package gate verifies both core assets, symbols, SourceLink, notices and consumers,
with only Lokad.Parsing in each dependency group and no Utf8Regex assemblies in
either consumer. Canonical 0.11 packages describe d005e85c; published 0.10 packages
remain untouched. Full Ubuntu ordinary/stress/package qualification remains a
later release gate, not a claim from these focused checks.

## One short comparison

Collected once on 2026-10-29, dedicated Azure Standard_F4as_v7, Ubuntu 24.04.4 x64,
four EPYC 9V45 vCPUs, 16 GiB RAM, no swap. Both Lython snapshots use SDK
11.0.100-rc.3.26522.148 and runtime 11.0.0-rc.3.26522.148. Reference is CPython
3.13.16, GCC 13.3 PGO/LTO, ordinary GC/GIL, `-I -S`. Exact identities and hashes
are in the receipts. The shared VM lease excludes other Lython jobs; quiet gates
pass before and after. The two jobs take 15.65 seconds in total, with no retries
or favorable reruns. **Every result is diagnostic and performanceQualified=false.**

| Workload / boundary | Input | Control ms | Candidate ms | CPython ms | Control / candidate IQR |
| --- | --- | ---: | ---: | ---: | --- |
| [Finditer / warm Run](00-warm-regex.iterator.1000.json) | 1,000 matches / 3,000 ASCII bytes | 13.068 | 1.355 | 0.124 | 4.7% / 23.8% |
| [Pattern reuse / warm RunAsync](01-warm-async-regex.reuse.100.json) | 100 searches of `ab` | 0.657 | 0.814 | 0.016 | 44.6% / 90.8% |

The iterator median improves substantially in this one observation, alongside
removal of whole-enumeration buffering and repeated prefix scans. Candidate
spread still prevents a qualified multiplier. Reuse is inconclusive: its nominal
24% increase comes with very high variation. Neither row certifies current
overall performance or closes the substantial CPython gap.

Warm lanes reuse compiled scripts; each invocation includes setup, guest pattern
compilation/cache behavior, all work, full output checks and cleanup. Startup,
script compilation, protocol transport and fixture hashing are excluded. Async
host operations complete synchronously in this diagnostic. This does not measure
genuine I/O suspension or isolate backend-only matching cost. CPython's module
pattern cache persists across invocations; Lython's execution-owned cache does
not. These are complete-script observations of each engine's behavior.

[catalog.json](catalog.json) uses the existing RegexScanBenchmarks iteration and
compiled-reuse shapes, printing the complete independent Python result instead
of returning it for the comparison protocol. [collect.py](collect.py) invokes the
existing micro command under owned 30-second services and a 60-second collection
budget. [collection.json](collection.json), [initial gate](machine-before.json)
and [final gate](machine-after.json) retain all outcomes and measurement status.
