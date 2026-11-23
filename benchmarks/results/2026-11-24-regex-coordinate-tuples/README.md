# Direct admitted regex coordinate tuples

Lython `3e5826f7d` builds `Match.span()` and the cached `Match.regs`
directly in their final arrays. The earlier implementation allocated
nested item factories and temporary reservations, then scanned the
completed pairs for charges it could determine before construction.
The new `regs` reservation covers the outer tuple and every pair before
allocation. It publishes the cache only after construction completes.

| Script | Matches | Reference CLR allocation | Candidate CLR allocation | Reduction |
| --- | ---: | ---: | ---: | ---: |
| Named Unicode spans | 3,072 | 3,215,528 B | 2,797,736 B | 12.99% |
| Unicode whole-match spans | 3,072 | 3,117,588 B | 2,699,376 B | 13.41% |
| Wide Unicode registers | 128 | 2,446,656 B | 1,769,792 B | 27.66% |
| Unicode text groups | 3,072 | 3,264,584 B | 3,264,584 B | 0.00% |
| ASCII text groups | 3,072 | 2,955,768 B | 2,955,768 B | 0.00% |

Both 32-file images match all five independent CPython goldens. The
reference core is `43688c753`. Both sides use the same optional
PythonRe 0.4.0 adapter on Windows x64 and .NET 11 RC3. The November 23,
2026 UTC diagnostic records median managed allocations over three warm
invocations, with an existing compiled script and memory allowance.
The admitted peaks match and each run returns to baseline. This is
allocation evidence. CPU and elapsed time remain unmeasured.

Windows .NET 11 and Ubuntu .NET 10 each pass 292 focused Release
checks. These cover Unicode scalar coordinates, empty and unmatched
captures, cached register identity, provider cancellation and cleanup.
The added public regression exercises terminal admission refusal after
successful matching, cleanup back to baseline and a later retained
register result on the same allowance through both APIs.

[Allocation summary](allocation-summary.json) · [Image identity](identity.json) ·
[Goldens, observations and procedures](observations.tar.xz).
