# Actual asynchronous sequence-consumption comparison

B867, 2026-11-17 UTC. Same immutable e92d9598 reference and aaef443f candidate
as the parent evidence; committed candidate 46343804. The existing **warm-async**
lane selects RunAsync. The parent B866 warm/compile-run jobs select Run regardless
of catalog runAsync metadata. This supplemental comparison closes that execution-
boundary gap; it does not repeat synchronous timings or measure async compilation.

| Complete script | Elapsed change | Wider CPU change | Reference/candidate control movement |
| --- | ---: | ---: | ---: |
| JSON/tuple/Markdown report | −10.87..−5.53% | −12.27..−2.58% | 0.10 / 5.89% |
| Generator/list/render | −24.05..−6.89% | −21.93..−6.25% | 1.79 / 20.44% |
| CSV invoice control | +0.14..+3.61% | +0.97..+3.05% | 1.28 / 2.16% |

Four pairings of two independent-process medians per source; ranges are not
confidence intervals. Candidate report/generator medians are favorable, but moving
candidate controls—especially generator/list—make their precise magnitude
uncertain. CSV is slightly adverse, including CPU. Preserve that debt rather than
claiming no regression. No allocation, genuine pending-I/O, startup, RSS or
embedding-tail improvement is established by this timing collection.

Three jobs in 95.127 s; four-second full-request warmups and five rotated roughly
0.5-second samples per process, existing worker/protocol/catalog. Warm includes
RunAsync, mediated RAM I/O, complete stdout/file checks and prior-result cleanup;
excludes compilation/startup, reset/protocol and final cache teardown. CPU clocks
are wider. Same quiet F4as_v7 / Ubuntu24.04.4 / four EPYC9V45 vCPUs / 16 GiB /
.NET11 RC3 / natural workstation GC and tiering as the parent comparison.
All image, golden, quiet, Closed, exit, join and persistent archive gates pass.
No users or fixtures remain. SHA-verified archive before owned RAM teardown;
254/252 MiB staging/final disk floors preserved, no retirement or core copy.
Terminal cached permanently; no unchanged retiming pending. Full delivery is
tracked separately; publication remains held.

[Summary](summary.json), [identities](identity.json), [raw observations](observations.tgz),
[archive receipt](archive-receipt.json), [procedures](procedures.tgz).
