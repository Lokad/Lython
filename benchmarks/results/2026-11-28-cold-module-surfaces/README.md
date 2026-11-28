# B1946 Attribute module metadata on the first configuration command

Two fresh configuration commands use the exact 32-file `bf10eeaea` image
from B1943, Ubuntu WSL and the pinned .NET 11 RC3 toolchain on November 28,
2026 UTC. They reuse B1927's 357-byte script and 181-byte input. Both output
and file goldens verify. The worker and EventPipe collector exit after each
capture, and the image hashes match before and after.

Each trace contains 2,680 matched JIT start and method-load events. These
intervals are per-method wall intervals between those events and can include
stalls. They establish which initializers run. Exclusive CPU share and the
effect on process startup remain unmeasured.

| Initializer | IL bytes | Matched wall intervals |
| --- | ---: | ---: |
| Frontend `StaticModuleCatalog` | 8,741 | 5.090 and 3.361 ms |
| Runtime `BuiltinModuleCatalog` | 2,210 | 0.670 and 0.594 ms |

The frontend surface constructor builds a frozen lookup set, a sorted
member list and a filtered export list for every declared module. The
configuration script uses explicit module imports, including JSON.
This identifies an implementation stage to inspect for consolidating
compile-time member metadata. Preserve exact ordinal membership, sorted
members, exports, reexports and submodule resolution. Follow the change with
first compile and compile-to-result pairs and a warm import-member control.
The smaller runtime registration initializer remains a separate concern.

The first verifier expected startup identities to carry library hashes.
The once protocol reports paths for these libraries. The corrected
validator confirms those exact paths and the complete frozen image before
and after capture. It recovers the already completed first trace and
collects the second. The verifier failures are retained with both traces.

[Catalog and earlier measurements](../2026-11-28-first-command-phases/README.md) ·
[Image identity](identity.json) · [Module JIT intervals](module-jit-summary.json) ·
[Raw traces and procedures](observations.tar.xz).
