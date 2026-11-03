# Native producer/stream-call transport — B157

2026-11-03. Retained in93bc77bb. Native JSON/CSV file output shares the existing
guarded executable write operation. This removes per-chunk method objects,
CallArgumentValue arrays and general binding for exact owned text/binary handles.
CSV captures the immutable handle directly; custom CSV captures its callable once.
Custom JSON resolves write on every chunk, preserving rebinding, descriptor order,
arbitrary chunks and ignored returns. Ordinary Python attribute/method values keep
their real route. No new stream interface/table/options/cache/owner or public API.

The existing operation retains managed stack admission, cancellation and exception
translation; typed native writers preserve error/type/closed precedence. Writes
stage funded data synchronously; actual host publication/cleanup still awaits
flush/close. Source aliases, capacity/replacement preflights and terminal refusal
are unchanged. Obsolete CsvWriterObject.WriteMethod is replaced by captured
WriteTarget, with no compatibility field. No discarded prototype remains.

## Whole scripts

Same quiet F4as_v7/Ubuntu24.04.4/.NET11RC3, ordinary GC/tiering, actual mediated
files in shared tmpfs. Exact APIs/checks/ownership/boundary as B156. This isolates
storage sensitivity identified by B155; it does not establish an ordinary-disk gain.
Initial eight jobs use4s warmup, nine rotated~100ms batches, baseline/new/baseline
control. Existing config/copy/text/report/JSON sources are unchanged. Added complete
CSV rewrite reuses the57,361B canonical multilingual/quoted/multiline input, reads
and writes4097 records, checks all61,458 output bytes and stdout. Isolated local
CPython3.13.2 and VM3.13.16 agree; no external CSV timing ratio is claimed.

| Complete job | Initial elapsed Δ / unchanged control |
| --- | ---: |
| Config warm |−22.87 /+0.44%|
| Config fresh |−18.13 /+77.37%|
| CSV rewrite warm |−5.22 /+0.72%|
| CSV rewrite fresh |−4.02 /+4.59%|
| Copy warm |−1.54 /−2.59%|
| Unchanged text warm |−1.97 /−0.13%|
| Unchanged report warm |−1.69 /+0.33%|
| Unchanged JSON warm |+0.86 /−1.29%|

Initial config fresh control73.21%IQR makes that row inconclusive. Its CPU control
+191.63%, warm CPU control+38.42%, copy/JSON CPU controls also drift: retain them.
One bounded unchanged-image confirmation uses6s warmup,250ms batches,1s settling,
reversed setup order and CPython. Initial conditions/source/data remain intact.

| Config phase | Earlier ms | New ms | CPython ms | New / CPython | Elapsed Δ / control | Process CPU Δ / control |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Warm |0.331149|0.251206|0.185366|1.355×|−24.14 /−0.76%|−22.90 /+0.40%|
| Compile-run |0.420375|0.349113|0.253158|1.379×|−16.95 /+0.31%|−15.36 /+0.39%|

New config IQR0.54/0.50%, control0.88/0.93%. These demonstrate improvement under
the RAM-backed full-script boundary. Initial CSV warm is a useful ~5% improvement;
fresh retains4.59% control drift, so exact fresh gain is diagnostic. Adverse JSON
and copy relative-control points stay visible. CPU includes reset/runtime/JIT/
response, not guest-only CPU. No P99, RSS, startup or generic portable multiplier.

Allocation: unchanged existing512-warmup/32-job helper, nine rotated batches,
shared CoreLib/hash witness and three isolated images. It retains its legacy B154
purpose label; actual catalogue/IDs/hashes define B157. Counts include complete
Run/reset/I/O/checks/result disposal; compilation/startup excluded. Its elapsed is
not the performance verdict.

| Complete job | Allocation Δ | Unchanged control |
| --- | ---: | ---: |
| Large config |−43.614%|−0.0008%|
| Small config |−15.440%|−0.202%|
| CSV rewrite |−5.322%|−0.021%|
| Unchanged report |−0.0012%|−0.0001%|

[Accounted execution/root peaks](accounted-peaks.json) are identical across all
images/cases. This is not physical RSS; coarse headroom for minor packets remains.
All goldens/hash witnesses/image isolation/zero balances/fixture cleanup pass.

## Identity and qualification

Baseline8b34d97e863cf3ed138b1f893198a6563a108477 (qualified0f05 runtime plus docs),
coref13a567561ae2ad1d7242196a89dc0762cf3439c4ebd9f08247b68ecf29370cb.
Measured new cored65c6f5443dbf2a287e7bb7acd3fdfeadd25d212638847475aa7fe7b2fe7cc12.
Exact five production source hashes/patch and normalized unchanged helper/adapter
bits are retained. Frozen after all local builds/tests joined; baseline had a
nonincremental build. Committed rebuild metadata can differ. All measured source
hashes matched the retained code before commit. Core/ordinary adapter dependency
policies are unchanged; dev.14 is still the newest supplied producer, no replay.

Focused Release319 internal/1196 public plus191 argparse cases per actual.NET11/
.NET10 PASS,0 build warnings. Existing custom rebind/capture, text/binary errors,
actual suspension/cancellation, first refusal, aliases and shared-budget reuse
coverage is preserved. No architecture-specific tests or checkpoint tallies added.
Full delivery41620245727 follows independently: Windows/Ubuntu.NET11/10, original
Stress and installed Release consumers. Its actual result is recorded separately.

Initial timing126.400s, allocation42.831s, confirmation75.270s, all exit0 with
before/after quiet gates, full outputs, worker joins/owned cleanup and free lease.
No timeout/restart/abandoned operation. Primary ordinary-filesystem CPython rows
remain tied to their recorded earlier source; these data do not relabel them.
Publication remains postponed; broader text/report/unique-key debt remains open.

Delivery run [41620245727](https://github.com/Lokad/Lython/actions/runs/41620245727)
is complete and GREEN for exact 93bc77bb: Windows/Ubuntu ordinary Release .NET11/
.NET10, original Stress scales and installed Release consumers. Required steps
are verified in [the cached receipt](delivery-ci-status.json). Later class-method
fact correction ca4822f4 is separately focused-qualified and is not included in
this receipt. Publication remains postponed.
