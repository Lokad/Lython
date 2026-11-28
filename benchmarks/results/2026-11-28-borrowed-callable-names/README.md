# B1930 Borrow the canonical callable name during module lookup

The candidate over `7260c1a28` selects the existing root call index and
looks up a bounded UTF-16 span. When a contract is found, abstract
analysis borrows the contract's qualified name. This removes the
transient qualified-name string for known module members. A longer name
can still use the ordinary string lookup. The module declaration and
reexport changes from B1929 remain in the reference.

The two frozen 32-file worker images differ only in the core DLL.
All 1,342 module member queries and 13 compiler cases have identical
results and diagnostics. Windows .NET 10 and 11 and Ubuntu WSL .NET 11
each pass 691 public and 67 white-box focused Release checks. Both
images agree with three independent stock CPython 3.13.16 goldens.
The workbook workload uses the independent B1929 OpenPyxl 3.1.5
golden. All four workload goldens also verify in the timed workers.

| First compilation | Caller-thread CLR bytes, reference → candidate | Admitted bytes |
| --- | ---: | ---: |
| Configuration edit | 717,752 → 717,864 | 67,328 |
| Workbook reexports | 374,608 → 374,160 | 70,848 |
| Generated configuration validator, 48 rules | 1,236,728 → 1,229,096 | 678,752 |

Windows .NET 11 uses seven separate first compilations per image and
case. All 42 successful compilations return the borrowed allowance to
zero. The generated validator normalizes keys and numeric values using
`urllib.parse`, `math`, and `json`; its exact source is in the archive.

## Warm timing

Ubuntu WSL on November 28, 2026, .NET 11 RC3, pinned to one logical CPU.
Two workers per image each warm for at least five seconds and contribute
seven rotated batches. Ranges span all pairings of worker medians.
Compilation includes prior-owner disposal. Compile-to-result adds
execution, golden checks and result cleanup. Where applicable, the
elapsed clock includes mediated RAM-backed I/O. Process CPU also
includes fixture reset and framing. All 112 batches pass, all workers
join and the persistent archive verifies after fixture removal.

| Script | Boundary | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Configuration edit | Compile | −1.85% to −0.99% | −0.49% to −0.20% |
| Generated configuration validator | Compile to result | −3.38% to +2.69% | −2.02% to +5.35% |
| Workbook reexports | Compile to result | −1.16% to +0.61% | +2.17% to +4.29% |
| Guarded report control | Compile | −4.32% to +2.31% | −3.41% to +1.58% |

Withdraw the span candidate. The workbook uses more CPU with about
one percent within-image CPU spread on both sides. Generated validator
allocation falls 7,632 B, and its timing spans zero with 5.28% candidate
CPU spread. The ordinary configuration compile has a small favorable
diagnostic. Preserve it alongside the workbook regression. The B1929
reexport-table removal remains the retained implementation.

[Timing](warm-summary.json) · [First-compile allocation](allocation.json) ·
[Image identity](identity.json) · [Observations](warm-observations.tar.xz) ·
[Exact trial patch and checks](procedures.tar.xz).
