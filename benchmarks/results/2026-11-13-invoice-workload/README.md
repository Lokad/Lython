# Invoice workload and composed-helper gate

R302 adds one maintained representative script to the existing catalog/worker
protocol: [CSV invoice](../../scripts/csv-invoice.py). It reads quoted Unicode CSV,
parses integer quantities/prices/rates, calls composed pricing/rounding helpers,
aggregates category tuples in a dictionary and writes a complete CSV report.
It includes a signed refund. Basis-point rounding is explicitly
`(amount * rate + 5000) // 10000`; this fixture has one chosen deterministic rule.

The CLR catalog supplies independent checked row totals and complete-file goldens.
Existing worker tests cover all three scales and compilation/warm/compile-to-result
boundaries. Numeric report tests cover both public APIs and return the shared
allowance to its compiled baseline after each execution. **100 focused public
cases pass per actual .NET11/.NET10 target.** Isolated local CPython and Lython
agree with every golden at empty/small/large, including all output files.

The large finite command initially refused the 4MiB JSON fixture allowance.
Its explicit invoice allowance is16MiB, funding retained field/tuple storage;
all cleanup and admission assertions remain. The original failure is preserved.
This does not claim row-count-independent uninterrupted streaming.

Inspection of qualified runtime source `fc2531b8` shows `price_line` uses12 final
slots and `rounded_basis_points` uses6; both are native-eligible. However,
`NativeExecution.Call` only handles native receiver/unary calls and returns to
ordinary execution for a user-function call. Thus pricing promotes its caller
before either two-argument rounding call, even when rounding completes natively.
This is an elimination hypothesis for complete composed-call construction,
not proof that frame cost dominates or a speed forecast.

Next, one bounded current CPU/complete-script gate must decide whether removing
that promotion is material. A selected implementation must reuse the same final
slots/PC and linear runner, transfer a genuinely suspending child exactly once,
preserve managed stack/cancellation/refusal and unwind prepared children if parent
admission fails. No new arena, expression graph, whole-body proof, value wrapper,
public option or third execution backend is selected.

No qualified elapsed/CPU comparison or runtime prototype exists yet. The CLI
correctness smoke's timer samples are diagnostic and must not become CPython
ratios. [Validation](validation.json), [source, original logs, complete worker
receipts and compiler inspection](source-checks.tar.xz). Earlier measurements
remain source-labelled in [BENCHMARKS](../../../docs/BENCHMARKS.md).
