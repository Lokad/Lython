# Bulk-text and shared line-construction delivery

**86d9344e / measured ecd9e956** passes
[43805966777](https://github.com/Lokad/Lython/actions/runs/43805966777).
Windows/Ubuntu actual.NET11/10 each: **4,797 internal /17,914 public ordinary**,
**14 internal /406 public stress** (all original13internal plus added line case),
and installed Release consumers.16 summaries and full terminal/logs verified.
600 frozen inputs (586production,4build,10test) match committed source after
explicit UTF8 newline normalization. Rebuilt binary identity is not inferred.
Terminal cached; never query this run again.

This cumulative checkpoint covers retained R429 direct expandtabs/join output and
R430 shared final split construction/bounded current-context line scanning. R429's
earlier exact efd image was not separately fully qualified; its changes are covered
here together with R430. Unicode, generator/callback behavior, managed stack,
true async I/O, cancellation/refusal/rollback, retained aliases/caches and budget
reuse pass. No new cache/owner/dispatch framework or public API was introduced.

Source-pair [bulk-text limits](../../2026-11-16-bulk-text-construction/refined/README.md)
and [line confirmation](../confirmation/README.md) separately describe allocation,
CPU and adverse/moving controls. The original failed idle gate remains failed;
these correctness tests do not change it or prove performance/embedding latency.
Later async-entry prototypes are not qualified by this receipt. Publication held;
the broader call/frame/operand performance objective remains open.
