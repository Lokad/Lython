# Share evaluated literal regex matches across frontend passes

The same exact direct-literal match can be resolved during annotation-flow
binding and then during ordinary expression checking. The frontend now
shares the validated Boolean fact when the literal, owned program handle,
operation and bounds agree. Temporary facts keep no prepared subject,
captures or token. The first actual provider work and the active cancellation
and admission checks remain. A changed fact is evaluated through the existing
provider operation.

The source pair starts at `eee178f71` and adds only the archived frontend
patch. A Windows .NET 11 RC3 diagnostic warms each case six times and takes
eleven compile-and-dispose observations, with three processes per side and
alternating source order. Allocation uses the current thread. Each case has
an independent host budget, and allowance returns to baseline after disposal.
Elapsed samples are local diagnostics with moving same-source controls.

| Compile case | Median CLR bytes, before → after | Peak admission, before → after |
| --- | ---: | ---: |
| 48 literal record selectors | 2,014,144 → 1,552,360 | 19,240,287 → 19,249,695 |
| Long Unicode match | 78,440 → 78,240 | 529,794 → 529,794 |
| Long Unicode miss | 72,608 → 72,560 | 526,914 → 526,914 |
| Configuration manifest | 72,328 → 72,336 | 82,304 → 82,304 |

The generated script alternates known matches and misses with an ignore-case
pattern and explicit bounds. The provider trace verifies **96 to 48** compile
preparations and match calls. All 48 runtime matches still call the provider
with the same windows in both images, and both produce the CPython output.
The four smaller match and miss traces each remove one frontend preparation.
The generated script allocates 22.93% less and uses 9,408 B more temporary peak
admission. The stable allocation result supports removing the duplicate work.
Local 48-selector medians range from 3.586–3.861 ms before to 2.800–3.245 ms
after. CPU and quiet-machine elapsed were unmeasured.

Windows .NET 11 and Ubuntu .NET 10 each pass 951 public and 129 white-box
focused checks, including provider cancellation, refusal, effective flags,
original windows and annotation behavior. The experiment uses the existing
optional adapter. Final qualification of a newer adapter remains separate.

[Results](summary.json) · [Procedures and raw runs](procedures.tar.xz).
