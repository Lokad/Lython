# B1812 Protected file search by match position

Windows .NET 11 runs `a1493cfea` with the same protected search script,
41.0 KiB of UTF-8 file input and identical written output. The early
case moves the matched line from line 1,025 to line 1. The measured
image matches all 32 recorded files. Both cases match independent
CPython output and file goldens. Both APIs return the allowance to
baseline after disposal.

| Match position | Lython API | CLR bytes allocated | Peak admitted bytes |
| --- | --- | ---: | ---: |
| Line 1 | Sync | 39,288 | 19,556 |
| Line 1,025 | Sync | 113,280 | 167,072 |
| Line 1 | Async | 39,536 | 19,556 |
| Line 1,025 | Async | 113,528 | 167,072 |

The later match adds 73,992 CLR bytes and 147,516 admitted bytes. The
protected loop traverses 1,024 extra 20-byte lines. The native text
reader materializes a guest string for each nonempty line. When a line
fits in an existing decoded window, it creates a funded string view.
Lines across windows use the accumulated-read path. The extra loops,
reader refills and transfer of window backing are included in these
totals. This diagnostic does not isolate the allocation or CPU cost of
one operation.

Investigate whether a protected search can use the existing decoded
window through a proven prefix check and publish ordinary strings
where the loop exposes them. The eligibility must preserve a rebound
helper, the loop's visible bindings, a retained match, newline handling,
cross-window text, cancellation and awaited close. Time a candidate only
if it removes per-line materialization across the ordinary loop without
duplicating the reader or weakening ownership.

[Source and image census](source-census.json) · [Allocation, verification and catalogs](observations.tar.xz).
