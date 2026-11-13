# Complete ZIP-config source comparison

2026-11-13 UTC. Source976f9439/frozen97bbeff3 against qualifiedcb8736ff/frozen5a184a1d
and stock CPython3.13.16. Final platform/stress/package delivery was pending at collection;
runtime bytes are unchanged by the overlap-test expectation correction.
Existing quiet F4as_v7/Ubuntu24.04.4/.NET11RC3, ordinary GC/tiering and Python GIL/GC.

| Complete warm script | Prior Lython | New Lython | CPython | New / prior elapsed | New / CPython |
| --- | ---: | ---: | ---: | ---: | ---: |
| ZIP list/read/config JSON;1,048,827 B archive |263.8–281.9 µs|58.8–59.9 µs|56.6–57.1 µs|−79.1..−77.3%|1.03–1.06×|
| Same script;16,635 B archive |46.22–46.44 µs|48.65–48.67 µs|48.42–48.76 µs|+4.75..+5.31%|1.00–1.01×|
| Invoice control;4,096 records/127,031 B |7.555–7.577 ms|7.488–7.550 ms|6.001–6.016 ms|−1.17..−0.07%|1.24–1.26×|

Ranges span two process medians per image; relative ranges use all four pairings,
not confidence intervals. Five rotating rounds. Large-archive prior/new IQR reaches
12.11/9.94%, prior process spread6.42%, new1.85%; its exact speedup magnitude remains
diagnostic, but the direction is large. Small-case IQR stays below1.82%, and its
roughly2.2–2.5µs regression is measured. Small wider CPU is inconclusive
(−1.05..+3.28%); invoice wider CPU is also inconclusive(−3.70..+4.00%). Large-archive
wider process CPU falls43.6–46.2%, including reset/protocol and background runtime work.
This is not an isolated interpreter CPU or embedding tail-latency claim.

Retain the coherent bounded-input architecture and correctness fixes. Accept and
record the small-case elapsed debt at this milestone; do not add a speculative size
switch or claim universal improvement. Revisit with a concrete removal argument if
future workload evidence justifies it. Other text/search gaps remain priorities.

Warm guest clocks include mediated RAM-file I/O, full stdout/file validation and
prior-result cleanup. Compilation, startup, fixture reset/staging, protocol and final
cache teardown are excluded. Wider CPU includes reset/protocol. Both engines run
identical source, including explicit UTF8 decoding before json.loads. The reference
and candidate workers differ because the latter supplies the optional positional
file capability; all other auxiliary bytes, including adapter0.4, are identical.
Worker/image hashes and full source provenance remain explicit. Sequential hosts
keep buffered behavior and do not inherit this positional-I/O speedup.

B684 preserves two partial ZIP jobs but lacks the invoice control and final quiet
gate: guest-time batch sizing underestimated untimed reset/validation costs and the
180s guard stopped it before the third job. No abandoned worker remains. Its original
partial observations, journal and missing gates are retained. B685 sizes batches by
full request wall time while keeping guest measurement boundaries and the240s owner
unchanged. All3jobs finish127.465s with complete quiet, identity, golden, joined-user
and archive/SHA gates. This corrects collection sizing, not runtime timing.

An earlier preflight detects locally mutated reference hard links before any
staging/measurement. The corrected reference comes from independently hash-verified
immutable VM fec79worker/9f728adapter bytes; original local-image mismatches and
commands survive. Only canonical byte-proved obsolete working copies are retired.
The252MiB floor and extra2MiB staging headroom remain unchanged. RAM traces are
archived/verified before unmount and no VM workers/fixtures remain.

[Complete raw observations](observations.tgz), [summary/wider CPU](summary.json),
[complete archive receipt](archive-receipt.json), [terminal state](terminal-status.json),
[original partial observations](failed-observations.tgz), [failure detail](failure-details.json),
[partial receipt](failure-receipt.json), [collectors, images and failed preflight](collection-and-images.tar.xz).
Publication remains postponed.
