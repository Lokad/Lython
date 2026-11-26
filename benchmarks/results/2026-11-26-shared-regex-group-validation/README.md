# Share validated provider group metadata

The exact compiled pattern already owns an immutable UTF-8 name list.
Frontend analysis now passes its validated list and count to the existing
compiled-program entry. Runtime publication reuses that list while the
program is borrowed. Analysis dictionaries still release their storage at
the end of compilation. Each execution creates and funds its own
guest-visible names. Dynamically compiled patterns keep their metadata
validation. The provider and adapter contracts are unchanged.

A cooperating provider observes three name-list reads on df71fc295 for
one compilation and two executions. The same test observes one read
with the change. Both runs preserve capture order, separate groupindex
views, program lifetime and shared allowance cleanup. The focused
Release selection passes 100 white-box and 702 public checks on each
of Windows .NET 11 and Ubuntu .NET 10. It includes malformed dynamic
metadata, local imports, current-token cancellation and terminal refusal.

The two 32-file images also verify independent CPython outputs through
the repository's optional adapter, with one and 24 literal compiled
patterns containing two named groups each. Both scripts verify the
group order and the scalar offsets of a Unicode subject. Runtime CLR
allocation and peak admission match in the local diagnostics. CPU
timing remains open.

Standard Release CI and package consumers passed for 4f7c085cd on
Windows and Ubuntu. [CI](https://github.com/Lokad/Lython/actions/runs/45847686103).

[Allocation](allocation-summary.json) · [Image identity](identity.json) ·
[Procedures and checks](procedures.tar.xz).
