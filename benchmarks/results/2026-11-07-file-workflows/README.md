# Maintained file-workflow comparison — R47/B401

2026-11-07, exact qualified8c9fd0d7 production/frozen d3a6c778; precommit
SourceLink3480aa55, current documentatione8389919. All560source hashes match;
worker7cf37ca5, adapter27211dbe/normal0.4 unchanged. Full delivery42371564043
already qualifies both Windows/Ubuntu, actual.NET11/10, original Stress and
installed core consumers. No new prototype, producer or repeated qualification.

Two missing maintained file workflows, not a repeat of B400's seven cases.
Search includes2049lines/42010B and exits after1025, leaving the trailing half
unread; its protected break and exact output are checked. Manifest copies only
two files/4107B, computes byte checksums through an annotated helper, sorts JSON
and writes the complete manifest. This measures small-file overhead, not large
copy throughput or arbitrary directory scaling. No source/data variants.

Same quiet AzureF4as_v7/Ubuntu24.04.4/four EPYC9V45 vCPUs/16GiB/no swap,
.NET11RC3 normal GC/tiering, CPython3.13.16 GCC13.3 PGO/LTO ordinary GIL/GC,
no JIT/debug, -I -S -X utf8. One CPython and two identical Lython roles per job,
5s warmup/role/job, seven rotated150ms rounds. Six jobs complete 129.63s.
180s owner+30s stop+30s margin, observed after240s. All source/image/Ready/
aux/golden/funding/quiet/Closed/exit/join/fixture gates pass, raw archive verified
under lease and independently after retrieval. No active worker/fixtures remain.

Warm and compile-to-result include mediated RAM I/O, guest work, complete stdout/
file validation and prior-result cleanup. Compile-to-result adds compile/program
disposal. Compile-only excludes execution/I/O. Startup/staging/reset/transport/
final cached teardown excluded. CPython retains modules, Lython isolates imports.
No median subtraction to isolate I/O, isolated-CPU, RSS, pending-I/O or tail claim.
Ratios above1 mean slower; ranges span two role medians, not confidence intervals.
Control=absolute role difference/candidate; IQR=larger Lython-role IQR/median.
The reused collector's generic R43/R44/R45 purpose label is historical; its exact
two-case manifest, six job entries, identity and predeclared R47 contract define
this collection. Variable/adverse observations remain visible.

## Compilation plus one execution

| Workload | Lython | CPython | Lython / CPython | Status; control / Lython IQR |
| --- | ---: | ---: | ---: | --- |
| Protected file search | 307.328–312.170 µs | 210.604 µs | 1.46–1.48× | Diagnostic; 1.55% / 1.11% |
| Two-file copy/checksum/manifest | 377.855–380.426 µs | 255.370 µs | 1.48–1.49× | Diagnostic; 0.68% / 2.39% |

## Warm execution

| Workload | Lython | CPython | Lython / CPython | Status; control / Lython IQR |
| --- | ---: | ---: | ---: | --- |
| Protected file search | 237.296–245.317 µs | 136.559 µs | 1.74–1.80× | Variable; 3.27% / 3.69% |
| Two-file copy/checksum/manifest | 222.824–224.537 µs | 150.289 µs | 1.48–1.49× | Diagnostic; 0.77% / 1.77% |

## Compilation/program disposal only

| Workload | Lython | CPython | Lython / CPython | Status; control / Lython IQR |
| --- | ---: | ---: | ---: | --- |
| Protected file search | 33.273–33.524 µs | 30.798 µs | 1.08–1.09× | Diagnostic; 0.75% / 0.83% |
| Two-file copy/checksum/manifest | 80.820–81.603 µs | 56.597 µs | 1.43–1.44× | Diagnostic; 0.96% / 2.97% |

[Raw observations](observations.tgz), [summary](summary.json), [identities](identity.json),
[receipt](archive-receipt.json), [terminal state](terminal-status.json),
[setup/source/contracts/commands](development.tar.xz) and
[full runtime delivery](../2026-11-07-integer-parsing/delivery-ci.json).
The complete-script performance objective remains active; publication postponed.
