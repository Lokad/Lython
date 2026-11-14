# Builtin iteration delivery

Exact `009d236a` / frozen `8ae1ae5f` passes
[43563853239](https://github.com/Lokad/Lython/actions/runs/43563853239): Windows
and Ubuntu, actual .NET11/10 ordinary suites, original stress and installed
Release package consumers. All581 frozen production inputs match the commit with
explicit newline normalization. Cached terminal state, full logs and16summaries
are retained; independently rebuilt CI assembly byte identity is not inferred.

This qualifies R345/R346, the retained R343 semantic tests and c9edb0ee's explicit
LF fixture correction. The original R345 Windows fixture failure remains a failed
run in its own evidence; it is not rewritten by this successful delivery.
Source-pair timing/allocation/control debts and R347 CPython uncertainty remain.
R348 is separate active work and inherits none of this qualification.
Publication/tagging and optional adapter promotion remain held.
