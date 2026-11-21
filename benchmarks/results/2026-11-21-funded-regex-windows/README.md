# B1293 funded UTF-8 windows for regex captures

Group access, findall and split use the same funded UTF-8 range
representation as ordinary text. Non-inline partial values can share
the immutable subject's payload when the current execution owns it.
This removes a payload array and copy per eligible result. Each view
funds its own shell. The original backing remains charged to the
command owner, which can conservatively retain a large subject for a
small surviving capture. Compiled and other-owner subjects still copy
partial captures. Whole capture identity and scalar offsets are
unchanged.

Reference `151b05134` and prototype `d05150e9d` ran on the local
Windows .NET 11 developer machine on November 21, 2026 UTC. Both use
the optional 0.4.0 adapter. All 32 files per image verify, and both
images match four CPython 3.13.2 goldens.

| Workload | Warm allocation change | Accounted peak, bytes before / after | Local warm elapsed change |
| --- | ---: | ---: | ---: |
| Single-scalar capture control, 4,096 scalars | +0.003% | 1,349,349 / 1,349,349 | −2.12% to +7.23% |
| Overlapping capture groups, 4,096 scalars | −39.46% | 5,459,155 / 1,176,275 | −7.15% to +10.94% |
| Extract messages from 256 log lines | −5.42% | 178,379 / 132,331 | −26.96% to +0.29% |
| Split 128 log sections | −19.57% | 152,919 / 103,671 | −4.68% to +6.56% |

All allowances return to baseline. Elapsed ranges span all pairings
of two independently warmed processes per image, with five rotated
batches per process. The clock includes execution, golden observation
and prior-result cleanup. These snippets construct their input in the
script and perform no guest filesystem I/O. The local elapsed results
are inconclusive. The log-message reference medians differ by about
34%, and no quiet-VM timing was collected. Retain for the removal of
copies and the measured reductions in allocation and accounted peak.

Windows .NET 11 and Ubuntu .NET 10 each pass 67 white-box and 422 public
Release checks using the existing optional adapter and cooperating
providers. The focused cases cover cancellation between admission and
publication, terminal denial of a compiled-subject copy, and access to
a command-owned capture after match work has closed and the pattern
cache has been purged. The original copy-specific cancellation case
failed on the new view path. Its successor exercises both owners and
checks admitted storage is refunded. The original failed test and the
rejected catalog with mismatched fixture sizes are archived.

[Diagnostic summary](summary.json) · [Raw observations and procedures](checks-and-procedures.tar.xz).
