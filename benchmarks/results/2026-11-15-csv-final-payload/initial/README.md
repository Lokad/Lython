# One final CSV payload: active prototype

R379, 2026-11-15. Frozen candidate `716176a8` versus qualified ec6c0416/
`6164a933`; one of585production inputs changes. Runtime is uncommitted/unretained,
with no CPU/elapsed or final platform qualification claim at this checkpoint.

Final record construction gives long fields separate immutable Python string
identities over one exact admitted payload, using existing UTF8 windows. Short
fields retain inline storage; numeric conversion/error order remains. Remove
per-field long byte arrays and consolidate repeated parser reset code. This
reduces allocations, not the necessary final copy from parser working storage.
No raw syntax/range inventory, new owner/refcount/cache, policy or memory layer.
Conservative per-record admission remains unchanged and funds working/output
overlap. Unpublished sibling withdrawal may retain the whole payload longer;
arbitrary-reference-withdrawal precision is not promised. Retained storage stays
funded until actual execution/result ownership ends.

## Focused validation and allocation

118internal/390public Release cases pass per actual .NET11/10, covering CSV
Unicode/dialects, numeric/error timing, duplicate/custom headers, aliases, real
I/O, cancellation, terminal refusal, retained results and cleanup. One new test
checks funding of the actual retained backing after private siblings/scratch end;
it also passes on the reference implementation before the runtime edit. Existing
large-payload checks prove cancellation/refusal before allocation/publication.
24 independent reference/candidate/isolatedCPython3.13.2 engine/phase goldens pass.

The maintained csv-validated-records.py source is unchanged. Its diagnostic input
has5,120rows/400,411B, three long metadata columns, quoted Unicode/newline notes,
numeric conversion and filtering. Manual totals are separately checked under
isolated CPython using the existing worker's StringIO output boundary. Initial
Windows-console CRLF validation refusal and capture correction are preserved;
no guest bytes/source/data or timing boundary was normalized or changed.

| Complete warm workload | Reference allocation | Candidate allocation | Change |
| --- | ---: | ---: | ---: |
| Metadata-heavy validated CSV |3,982,195.875B|3,760,902.125B|−5.557%|
| Ordinary invoice control |4,590,464.875B|4,590,441.625B|Effectively flat|
| Ordinary fulfillment control |4,673,332.375B|4,673,188.375B|Effectively flat|

Existing allocation tool, one fresh process/image, five64-invocation batches
after128warmups/at least4seconds. Medians use process-wide cumulative CLR
allocation, no forced GC. Includes fixture reset, mediated filesystem I/O, full
stdout/file validation and result cleanup; excludes compile/startup/final cache
teardown. This is allocation evidence, not live memory, CPU, RSS or tail latency.
All image/auxiliary/source identities, goldens and budget baselines verify;
all local workers join and fixtures close. Exact core/source/tool/checks/controls
remain in the archive. No VM collection or final retention decision yet.

Next: one bounded quiet-machine whole-script warm/fresh source pair with ordinary
CSV/text controls; retain with explicit debt, justified refinement or withdrawal.
Keep adverse observations; no fixed percentage veto. Publication remains held and
the broader performance roadmap remains active.
