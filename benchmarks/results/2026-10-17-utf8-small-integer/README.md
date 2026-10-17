# Bounded UTF-8 integer parsing and complete CSV diagnostic — 2026-10-17 UTC

Short decimal `int(text/bytes)` inputs now use the managed BCL signed-64 parser,
examining at most 20 UTF-8 bytes and requiring complete consumption. Successful
small values avoid decoded strings, per-digit BigInteger arithmetic and a scratch
reservation. Their Python integer representation and existing scalar headroom
remain. Other forms, bases, overflow and major values retain the admitted general
path; cached admission refusal is checked before work. Narrow working thresholds
can improve because storage disappears. No new owner, ledger or parser framework.

The same checkpoint deletes the empty cache re-snapshot hook and two calls,
updates coupon/retry comments and renames three useful ownership tests without
changing their assertions, caps or collection. Live cache funding and rollback stay.

| Complete CSV, fresh compile/run | Median | IQR / median |
| --- | ---: | ---: |
| Earlier Lython e01d45c6 | 8.181 ms | 7.54% |
| Candidate Lython 27a61681 | 7.503 ms | 6.44% |
| CPython 3.13.16 | 3.849 ms | 1.14% |

The candidate median is 8.3% lower in **one unqualified diagnostic**. Substantial
Lython spread and seven short rounds do not establish a speedup or qualified
CPython multiplier. No repeated timing, retuning, new producer or long lane.
Historical [BO](../2026-10-17-csv-fresh/README.md) and warm
[C](../2026-10-14-representative-csv/README.md) retain their own source identities.

The unchanged `scripts.csv-summary.large` processes 4,096 records / 57,361 UTF-8
input bytes: quoted multiline café notes, emoji, and negative amounts to ignore.
It aggregates accepted amounts and writes exact `report.csv` bytes. Independent
goldens at 0/16/1,024 blocks (0/64/4,096 records) pass for both Lython revisions
and CPython. Source: 533 UTF-8 bytes, SHA256
2e83f4a6b80d1eb3e326fe6eba8e4ef37a31ae4ec19a7d59c848a1736f098f2c.

Boundary: compile fresh source and run once in already-running workers, including
namespace/context setup, file reset/staging, complete output-byte checks and prior
result/compilation cleanup. Startup, transport and full response hashing are
outside the timer. Ordinary protection, GC and worker tiering remain; only the
supervisor's tiering is disabled. Lython uses the existing shared unbounded
allowance with normal public settings and fuel unset; CPython has no equivalent
in-process containment. File-host overrides and comparison tooling are unchanged.

Machine: dedicated Azure Standard_F4as_v7, Ubuntu 24.04.4 x64, Linux
6.17.0-1022-azure; four AMD EPYC 9V45 vCPUs, 16 GiB RAM, no swap. SDK 10.0.401 /
.NET 10.0.12; isolated UTF-8 CPython 3.13.16, GCC 13.3, PGO/LTO, ordinary GIL/GC.
Seven rotating rounds span 8.197 seconds, under the existing 28-second owned
service / one-second cleanup bound. All 604 source files per revision match.
Seven workers and fixtures are gone, relevant services inactive and lease free.
An initial setup script failed before extraction/build because of CRLF; its
LF-only correction preserves source, workload and limits. No timing had started.

| Separate native allocation diagnostic, 128 calls | Before | After |
| --- | ---: | ---: |
| Text or bytes, either hosting API | 11,576 bytes | 4,408 bytes |

Allocation compares `57275bf2` (after no-op cleanup) with production committed as
`27a61681`, using the identical [temporary probe](../2026-10-17-integer-text-calls/native-allocation-probe.txt)
from the earlier call-transport experiment. Four warmups/eight samples per case;
all samples agree. This separate Windows white-box measurement excludes compilation,
context/input creation and owner disposal, includes execution checks/parser/boxing/
frame cleanup, and keeps native async work on one thread. Shared i7-14700KF,
28 logical processors, 64 GiB RAM; SDK 10.0.300-preview.0.26177.108 / .NET 10.0.13,
ordinary GC/tiering. It is not the CSV allocation profile, physical heap, host
latency or a CPython comparison. Source hashes and actual DLL identities matter:
the candidate was measured before committing and metadata names the predecessor.
The temporary fixture is removed from live builds after capture.

Eighty saved fuel/span traces match. Eighty-six independent CPython boundary/error
cases pass through both APIs, including signed limits, overflow, suffixes/NULs,
whitespace/underscores, other bases and major results. Focused 192 white-box and
644 public checks preserve cancellation, terminal refusal, shared funding and
retained results. Full platform/package qualification belongs to the delivery
revision; these focused and timing receipts have their own exact source labels.

Evidence: [timing and loaded identities](compile-run.json),
[declaration and both source manifests](comparison-declaration.json),
[independent goldens](independent-goldens.json),
[baseline correctness](correctness-baseline.json),
[candidate correctness](correctness-candidate.json),
[before proof](before-proof.json), [cleanup/source proof](cleanup-source-proof.json),
[native allocation comparison](allocation.json), [CPython oracle sources](cpython-oracles.json),
and [setup correction](setup-correction.json).

The authoritative external comparison remains [BENCHMARKS.md](../../../BENCHMARKS.md).
