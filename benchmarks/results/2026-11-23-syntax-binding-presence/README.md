# Share outer binding facts with syntax validation

Two private frontend changes compare against the exact combined
`1b841b597` core. Both preserve the separation between names
bound by a lambda's defaults in its defining scope and names
bound in its body.

The first variant, `3ce487100`, collects ordered assignment
expression names during syntax validation. A statement no longer
walks its ordinary expressions again to collect bindings. Try and
match suites keep their original interleaving. The second variant,
`1816e63de`, records a single presence bit. Only statements
without an enclosing binding skip the later expression walk.
The existing collector still handles source order for real writes,
including exception clauses and match guards.

| Windows .NET 11 compile diagnostic | Ordered list | Presence bit |
| --- | ---: | ---: |
| Inherited dataclass configuration | −0.04% | −0.05% |
| Sparse generated validator | +0.96% | −0.03% |
| Two factory scripts | 0% | 0% |
| Nested lambda defaults | +0.16% | +0.08% |

The percentages compare medians of local allocation batches.
Every compiled owner returns its allowance. The presence bit
avoids the sparse validator's observed allocation increase.
The structural probe finds only two omitted scans across seven
representative scripts. These cover eight expression nodes in the
dataclass and 16 in the sparse validator. The four binding-focused
controls exercise actual assignments, so their scans remain.

Keep both variants private. The representative frontend already
avoids most of these walks. The ordered list takes storage for
real bindings, and the presence bit eliminates too little work
on the measured scripts to justify another quiet timing campaign.
Revisit with a representative input dominated by such walks or a
design that can remove a larger binding phase.

The first variant matches 15 exact tape fixtures and ten CPython
outputs. The second matches 16 tape fixtures and eleven outputs.
Windows .NET 11 and Ubuntu .NET 10 each pass 3,975 focused Release
checks per variant. The first Ubuntu run hit four CLI comparisons
without the expected Python command. The rerun builds the matching
probe and supplies CPython 3.13.2. The initial logs and rerun are
preserved. No elapsed or process CPU claim was collected.

[Allocation, scope counts and images](summary.json) · [Source and checks](procedures.tar.xz).
