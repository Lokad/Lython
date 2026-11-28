# B1929 Resolve callable reexports from module declarations

The frontend removes its eager 23-row callable-alias table. Eleven rows
duplicated the ordinary qualified name. Three existing OpenPyxl module
surfaces supply the two prefixes needed by the other twelve rows.
The member lookup borrows the surface already found. Standard metadata
uses the accessed module, and the existing submodule classifications
keep their original behavior.

The reference is `a8fb5861c`. The two 32-file images differ only in
the core DLL. The lookup witness matches all 1,342 member queries
across 66 registered modules and an absent module, including standard
metadata. Thirteen compiler cases match in validity and diagnostic
code, text and span. They cover reexports, shadowing, merged callable
bindings, and builtin submodules. The retained white-box checks now
test the behavior of all three reexported surfaces. Windows .NET 10
and 11 and Ubuntu WSL .NET 11 each pass 691 public and 67 white-box
focused Release checks.

Both worker images match the three stock CPython 3.13.16 goldens used
for warm timing. The workbook cold workload separately matches the
golden from the recorded Windows CPython and OpenPyxl 3.1.5. It
uses the workbook constructor, a worksheet write, two cell utilities
and the reader reexport.

| First compilation | Caller-thread CLR bytes, reference → candidate | Admitted bytes |
| --- | ---: | ---: |
| Configuration edit | 722,592 → 717,464 | 67,328 |
| Workbook reexports | 378,808 → 374,608 | 70,848 |

Windows .NET 11 runs each first compile in a separate process, seven
times per image and case. All 28 successful compilations return the
borrowed allowance to zero on disposal.

## Timing diagnostics

November 28, 2026 on Ubuntu WSL, .NET 11 RC3, pinned to one logical CPU.
Cold images are on persistent native WSL storage. Twelve rounds rotate
workloads and source order. All 48 launches verify. The parent times
launch through child exit and drain with a prepared empty fixture
directory. The child performs file setup, verification, response
writing and owner cleanup inside the boundary. CPU includes child
user and system time.

| Fresh script | Median elapsed, reference → candidate | Median process CPU, reference → candidate | Paired CPU change |
| --- | ---: | ---: | ---: |
| Configuration edit | 255.89 → 262.74 ms | 219.33 → 216.99 ms | −2.99% to +3.17% |
| Workbook reexports | 215.48 → 224.03 ms | 171.46 → 169.46 ms | −2.50% to +1.63% |

Both cold elapsed medians are higher. Paired elapsed and CPU span
zero for each workload. The warm run uses two workers per image
with seven rotated batches. Compile includes prior compiled-owner
disposal. Compile-to-result adds execution, applicable mediated I/O,
output and file checks and result cleanup. CPU also includes fixture
reset and request framing. All 84 batches verify. Workers join and
the persistent archive verifies after fixture removal.

| Warm script | Boundary | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Configuration edit | Compile | +0.73% to +3.41% | −1.72% to +1.08% |
| Typed ASCII CSV meter | Compile to result | −3.33% to +2.39% | −4.04% to +0.83% |
| Guarded report control | Compile | −1.77% to +2.33% | −0.36% to +1.27% |

Retain the removal of the duplicate catalog and the confirmed cold
allocation savings. Warm configuration elapsed is adverse in this
diagnostic. Keep it visible when measuring the next lookup change.

[Cold timing](cold-summary.json) · [Warm timing](warm-summary.json) ·
[First-compile allocation](allocation.json) · [Image identity](identity.json) ·
[Warm observations](warm-observations.tar.xz) ·
[Cold observations, source and checks](procedures.tar.xz).
