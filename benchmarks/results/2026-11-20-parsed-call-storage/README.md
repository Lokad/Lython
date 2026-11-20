# Parsed call operand storage screening

Both private prototypes increased compile allocation on all four complete
scripts. The retained frontend keeps its existing operand representation.

| Complete workload | Reference bytes per compile | Shared expression lists | Value operands only |
| --- | ---: | ---: | ---: |
| Generated validator | 2,466,815 | +2.73% | +1.74% |
| Inherited dataclasses | 608,184 | +2.84% | +2.19% |
| Configuration manifest | 243,752 | +2.04% | +1.10% |
| Eager annotated rules | 52,584 | +1.66% | +1.06% |

The first design stores expression references and optional argument forms
separately. It removes the static positional copy and individual argument
objects, but adds a list view and mixed-form storage. The smaller second design
keeps the existing list and changes only its element from a record object to
a value. Wider list elements and enumeration overhead are possible contributors.
The allocation data alone does not attribute the increases. Both designs keep
the emitted executable call-site layout.

Reference is production `46ccc2efa`, using exact snapshot `eaf4d65c1`.
Candidate sources are `895eb73ca` and `e1d6c5b25`. The local Windows .NET 11
RC3 diagnostic measures repeated compilation and disposal with tiering
disabled. All actual 32-file images passed four CPython goldens. The first
prototype also passed 671 white-box and 1,970 public focused Release checks.
The second passed the Release build and the golden cases. Neither layout was
timed on the quiet VM. A future design would need to remove the staging
growth and preserve the parser's existing bulk operations. The current changes
do not justify extending the design or creating a new runtime storage mode.

[Allocation samples, source diffs and checks](screening.tar.xz).
