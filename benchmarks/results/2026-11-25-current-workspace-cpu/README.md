# B1709 Current whole-script CPU attribution

These samples measure the immutable November 25, 2026 runtime
`7d24cfe9ad4deafe22db296e5fbfcf082a5405cf`, including buffered CSV
advancement and prepared native iterator workspace borrowing. The Azure
F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3. The same frozen worker files
and complete CPython-derived results were verified before and after each
capture. Each phase warmed for at least eight seconds. Linux perf captured
whole worker stacks at 199 Hz for approximately six seconds per phase.

| Workload and phase | Total samples | With a resolved Lython runtime frame | Selected observations |
| --- | ---: | ---: | --- |
| Ordinary generated definitions, warm | 1,171 | 1,158 | 469 nearest native instruction or call dispatch, 177 nearest definition/default construction, 161 nearest admission or retirement |
| Shipment callback report, warm async | 1,256 | 1,216 | 196 nearest instruction dispatch, 107 nearest CSV parsing and row construction, 46 nearest iterator routing |
| Defaulted rules, compile-to-result | 1,205 | 1,158 | 499 inside definition construction, including 185 nearest pair-member construction and 149 nearest reservation |

The shipment profile retains 36 samples nearest the zip pull state machine.
Map is nearest on 44 samples. The broader iterator region includes callable
execution and accounts for 83 samples. A direct native map input can remove
cursor acquisition and mediation, but these counts put instruction execution
and CSV work ahead of a larger iterator rewrite. The defaulted rule profile
still points to pair construction. A third evaluator-sharing variant would
need to remove a complete stage to justify displacing the existing path.

The regions count sampled stacks with observable frames. Inlining and
incomplete unwind can obscure the active method. Inclusive regions may
overlap. The capture spans application work with RAM-backed fixture I/O,
validation, reset, request framing and cleanup. It does not measure a CPU or
elapsed change against the previous source or CPython.

The first collection stopped while writing a perf symbol map to the full VM
disk. Joining, quiet checks and archival completed, but post-capture workload
verification did not. A second staging preflight rejected the remaining
space. Verified copies of two historical upload tarballs moved to local
working storage. The exact-image retry passed both quiet checks, reverified
the worker and each workload, and archived observations under the lease after
all users joined and the guest fixtures were removed.

[Attribution](attribution.json) · [Exact source and image](identity.json) ·
[Observations](observations.tar.xz) · [Terminal receipt](terminal-status.json) ·
[Procedures](procedures.tar.xz) · [Failed conditions](failed-conditions.tar.xz).
