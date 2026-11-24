# Attribute remaining native call workspaces

A Windows .NET 11 diagnostic on `489d3a3f6` instruments six complete
scripts to identify completed calls, native workspace entry, promoted
frames and general binding. Every script validates its expected output
and returns its shared allowance. The counters are confined to the
diagnostic build. They provide route information without a timing or
allocation claim.

Defaulted and captured `make_rules`, captured `alternate`, eager
`compile_rule`, the short report's `words_from_line` and CSV
`price_line` enter the separate native workspace. No measured script
enters deferred-child binding or native frame promotion. The short
report's outer `main` uses general binding once per invocation.
The postponed outer `for_policy` completes directly. The defaulted
`apply` and lambda take the completed-default path. Capture reads and
the invoice's arithmetic helper take the supplied-completion path.

The uninstrumented build gives the following caller extent audit.
The available slots start at the current call's first argument and
end at the caller's existing capacity. A capacity shortfall rules
out that particular slot borrow. The calculation gives no guarantee
that an otherwise large enough window can safely overlap result storage.

| Repeated call | Existing operand window | Callee slots | Additional slots needed for this borrow |
| --- | ---: | ---: | ---: |
| Captured `make_rules` | 3 | 10 | 7 |
| Captured `alternate` | 2 | 5 | 3 |
| Defaulted `make_rules` | 3 | 7 | 4 |
| Eager `compile_rule` | 2 | 6 | 4 |
| Short-report `words_from_line` | 7 | 5 | 0 |
| Invoice `price_line` | 6 | 12 | 6 |

Native workspaces are the common remaining entry for repeated calls in
these scripts. General binding and continuation transfer are absent from
most runs. Repeating the earlier argument-window loan would cover
the text example and leave the related factories and invoice out. The
next design needs a smaller complete execution footprint or a different
way to eliminate repeated dispatch. Increasing the caller window would
need its own storage and control evidence.
[Earlier operand loan](../2026-11-22-native-operand-call-loan/README.md).

[Route counts](route-counts.json) · [Existing capacity audit](workspace-fit.json) ·
[Sources, goldens and procedures](procedures.tar.xz).
