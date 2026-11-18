# B991 direct keyword overflow

User functions bind overflow directly into the admitted `**kwargs` dictionary.
Expansion retains the original keys of `**mapping` and the final dictionary
keeps their identities and order. The binder no longer creates a CLR spill map
or re-encodes those keys. Missing arguments, duplicates and interrupted
construction refund only the unpublished backing and synthesized names.
Native string hashes use the same cancellable path as ordinary key insertion.

B991 measures `25d3005f8` (`087eebd6`) against the archived, corrected source
patch (`97851ccf`). The verified run took 96 seconds on 2026-11-18 on a
four-vCPU Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11.0.0-rc.3.26522.148.
Two independent workers for each image rotated over warm-up and five measured
rounds per lane. Before/after quiet gates passed and all workers joined before
fixture teardown and archive verification.

| Warm workload | Reference elapsed | Corrected elapsed | Elapsed change | Process CPU change |
| --- | ---: | ---: | ---: | ---: |
| 1,024-row keyword forwarding report | 2.649–2.662 ms | 2.430–2.436 ms | −8.72% to −8.06% | −10.25% to −7.50% |
| Positional call control | 2.042–2.067 ms | 2.078–2.093 ms | +0.56% to +2.51% | 0% to +2.52% |

The delta ranges pair the two reference and two corrected worker medians.
Warm elapsed includes mediated RAM-backed I/O, golden checks and disposal of
the previous result. It excludes startup, fixture reset and final cache teardown.
Process CPU has the wider worker boundary. The report allocates 9.01% less and
its accounted peak falls 10.20% in the separate local allocation run. The
positional control's median allocation and peak are unchanged. Keep the
positional timing debt visible. B990 moved that timing in the other direction.

The [initial quiet comparison](../2026-11-18-direct-keyword-overflow-screen/confirmation/README.md)
isolates compilation and compile-to-result for the preceding prototype binary.
The original [adverse Windows screen](../2026-11-18-direct-keyword-overflow-screen/README.md)
remains archived with no established cause. B991 confirms the corrected warm
path. CPython serves as the output oracle, with no B991 timing.

Focused Release passed 34 white-box and 200 public cases per target on
Windows and 34 white-box per target on WSL (.NET 10 and 11). Coverage includes
original aliases through a suspending default, early duplicate detection,
preflight refusal and cleanup. The new canceled-long-key test fails on the
B990 image and passes on the corrected source.

[Summary](summary.json) · [Raw observations](observations.tgz) · [Checks and source patch](checks-and-procedures.tar.xz)
