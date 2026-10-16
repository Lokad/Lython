# Complete text job: lower-call wrapper experiment rejected

| Warm text-summary file job, 3,072 lines / 35,840 input bytes | Lython baseline | Rejected candidate | CPython 3.13.16 |
| --- | ---: | ---: | ---: |
| Read, lowercase/split/count, sort and write report | 6.8308 ms | 6.7464 ms | 1.3224 ms |

One [short diagnostic](warm.json) observed a 1.2% lower candidate median against
5.7% / 4.8% / 0.9% IQR/median, respectively. **The change is inconclusive.** The
extra dispatch was not justified and the entire production prototype was removed.
There is no qualified speedup, confidence interval or current overall CPython
multiplier. The owned command took 8.923 s including cleanup; no repeat or retuning.

The newer representative text row in BENCHMARKS.md uses the **baseline** time,
not the rejected candidate. Baseline revision:
`72856c8c817ea709b90d3966ec5aa98cfbafc772`. The final production sources match that
qualified revision exactly; tests/docs changed afterwards. This observation still
does not qualify a historical-to-current timing trend. The earlier [AA snapshot](../2026-10-15-utf8-lower/README.md)
remains preserved separately.

Rejected candidate `64b8e2651d75ad7d6a58e3bf00df492bc140e5a4` has six frozen
production sources in [the declaration](comparison-declaration.json). Immediate
zero-argument lower calls reused the existing receiver-separated transport, skipped
the bound callable (and sync receiver-cache entry), and shared the lowercase/
ownership body and strip/lower frame handler. Exposed, dynamic, malformed and
expanded calls retained their ordinary routes. Existing no-argument callables
already avoid general binding and arrays; the prototype did not remove those.
No new Unicode implementation, owner, binder/cache framework or capability switch
was introduced.

Rejection removes the lower marker/eligibility/dispatch arms, shared direct string
handler and extracted lower body. The original required strip helper and ordinary
lower implementation are restored. Useful public tests remain as StringLowerCallTests;
there are no tests requiring the discarded internal route. Source equality to
baseline is checked independently at delivery. The archived candidate remains
evidence, not supported runtime machinery.

Both roles use the unchanged complete `scripts.text-summary.large` job (fixture
size 1,024 generates 3,072 physical input lines). The output is `6144` plus a
42-byte, four-line report; [baseline](baseline-correctness.json) and
[candidate](candidate-correctness.json) match independent stdout/file goldens at
all three existing sizes. Base64 fixture strings in JSON represent binary file
bytes; their encoded length is not the input size.

Measured 2026-10-16 on the dedicated Azure Standard_F4as_v7: Ubuntu 24.04.4 LTS,
four AMD EPYC 9V45 vCPUs / 16 GiB RAM; SDK 10.0.401/runtime 10.0.12, CPython
3.13.16 with ordinary GC/GIL and `-I -S`. Normal Lython protections, cooperative
allowance, GC/tiering and managed stack admission remain enabled. CPython has no
equivalent containment.

Both engines use warm compiled code, fresh invocation state and matched efficient
file-host operations. The timer includes input/output reset/staging, execution,
projection, exact output/file checks and previous-result cleanup. Compilation,
process startup, transport and full hashing are excluded. This is synchronous
Run; actual RunAsync suspension/cancellation is tested separately.
[Cleanup/source proof](cleanup-source-proof.json) verifies identical comparison/
catalog/Python sources, all six frozen changes, seven exited workers and removed
file fixtures, inactive services and available VM lease.

[Local validation](local-validation.json): the candidate passed 2,354 white-box
and 251 focused public cases; after removal the restored runtime passes the same
251-case public group (overlap; all owned groups under 30 s). Twelve new public
cases cover Unicode, exposed method metadata/receiver lifetime, expanded/malformed
argument effects, retained results, denial before assignment after body entry,
actual custom property/method suspension/cancellation without replay, and cleanup
to baseline. Five [independent CPython 3.13.2 programs](cpython-goldens.json) match.
No timeout or containment assertion was relaxed. Original Stress and full local
public suites were not rerun. Exact package/platform qualification is recorded
at delivery; the next source investigation must not retune this producer.
