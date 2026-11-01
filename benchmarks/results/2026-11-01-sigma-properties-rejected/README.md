# Final-sigma property index prototype rejected (B30)

One fixed complete-job comparison on **2026-11-01**, baseline **830c6251**
(same runtime at e349f64b), candidate e349f64b plus [source.patch](source.patch).
Dedicated Ubuntu 24.04.4 Azure F4as_v7 / four EPYC 9V45 vCPUs, pinned .NET 11 RC3,
ordinary worker GC/tiering. Six owned processes, **46.589 seconds total**;
both quiet gates, complete goldens and ownership cleanup pass.

**Rejected and removed.** The predeclared target required at least 5% lower
complete text process-CPU median against **both** baseline observations. The
candidate improves 6.85% against the first, but only **4.15% against the identical
final control**. No retiming, alternate candidate or favorable control selection.
All three runtime files were restored byte-for-byte and the ordinary Release
solution rebuilt successfully. No new production API, table or tests remain.

## Fixed comparison

| Complete workload | Before CPU ms/job | Candidate CPU ms/job | Identical-before control CPU ms/job | Candidate change vs before / control |
| --- | ---: | ---: | ---: | ---: |
| Text helpers | 6.3332 | 5.8996 | 6.1548 | -6.85% / -4.15%, misses target |
| CSV helper control | 8.0022 | 7.5707 | 7.6777 | -5.39% / -1.39% |

Text has 3,072 lines / 35,840 input bytes; CSV 4,096 records / 57,361 bytes.
The existing complete-job driver uses four warmups, seven rounds of 128 public
synchronous jobs per process, before/candidate/identical-before. CPU includes
all process, GC and JIT work, not only the guest. Every CPU sample exceeds
100 ms, resolving the measurement granularity. Baseline CPU drifts 2.82% text /
4.06% CSV, within the declared 5% limit. The unchanged CSV control's favorable
point estimates emphasize why one selected baseline is insufficient.

Wall ms/job, before/candidate/control: text **6.7212/6.3905/6.6521**;
CSV **8.3785/8.4861/8.2956**. CSV wall time increases despite lower process CPU.
Allocation B/job: text **5,518,723.5/5,518,713.0/5,518,696.4**;
CSV **9,542,136.4/9,542,852.8/9,542,286.9**. No allocation increase exceeds 0.008%.
All observations remain visible; wall/allocation results do not override the
failed CPU target. No qualified elapsed, host-latency or CPython claim follows.

Jobs include fixture reset/staging, real mediated file work, full stdout/file
goldens, result disposal and baseline/zero ownership checks. Compilation/startup
are excluded; reflection/validation overhead is common. Each process is bounded
at 30 seconds, aggregate process budget 90 seconds; a 120-second systemd service
owns the lease and cleanup. The service is inactive and lease available afterward.

## Prototype and correctness

[B29](../2026-11-01-current-text-cpu/README.md) locates 10.7% of text guest self
samples in Unicode final-sigma property searches. The prototype replaces two
binary-searched interval arrays with one exact two-bit lookup using 98
deduplicated 256-scalar pages. It keeps the casing/builder algorithm, context
rules and cancellation/admission paths. Capitalize, swapcase and title also use
these properties; their semantics are retained without keeping the old search.

The tradeoff was **4,752 -> 10,624 bytes** of generated property data (+5,872 B);
compiled core PE size **5,538,816 -> 5,544,448 B** (+5,632 B). Managed span literals
need no runtime table construction or new cache. Startup/RSS impact was not
qualified. The failed gate means this extra representation is not retained.

The actual compiled lookup matches the frozen original flags for **all
1,114,112 Unicode positions**, including zero-valued surrogate positions and
out-of-domain controls. [Flags result](flags-equivalence.json), original ranges,
compressed expected bytes and the small managed reflection/CreateDelegate
diagnostic preserve that proof. Focused Release checks pass **95 native / 102
public per actual framework**, including CPython casing/context goldens,
cancellation, refusal, result ownership and budget reuse. No layout-only tests
are added; the existing useful regressions remain.

## Evidence and stopping rule

[acceptance.json](acceptance.json) records full-precision observations, gate,
source/archive hashes and restoration. [raw-diagnostics.tar.gz](raw-diagnostics.tar.gz)
contains every round, loaded assembly identity, catalog and process record,
machine gates and driver logs. Readable existing-driver adaptations and source
patch accompany it. No native/unsafe code or memory-policy changes enter core.

Stop B30. The hotspot is real, but this representation misses its fixed whole-job
acceptance. Do not reopen the prototype, retry timing, add an ASCII rescue path
or interpret sampled self time as a promised removable fraction.
