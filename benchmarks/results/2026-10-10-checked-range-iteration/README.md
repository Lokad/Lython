# Composite checked range enumeration

Integrated: targeted loop time falls -3.53%/-4.50% in both replicas, exceeding roughly 1% spreads. Positional calls -1.70%/-0.69%, keyword -2.27%/-1.05%, empty -1.10%/-2.85%, sort -3.91%/+3.03% and pipeline +0.06%/+0.70% remain visible. Call decreases are small or mixed against their spreads; no general call/sort/pipeline benefit or allocation saving is claimed. Loop managed allocation increases by 8.00/21.84 bytes per complete job. Accept the modest allocation/native cost for the repeated targeted loop benefit; retain every observation without recollection or full lanes. These are short paired diagnostics; independent-session and milestone qualification remain pending.

Twelve paired micros stop in **8.04–8.05 seconds**, including
cleanup. Separate ordinary allocation groups stop in **4.03 / 4.03 seconds**;
native-code groups in **4.03 / 4.03 seconds**.
Every collection has a **30-second external process-group cap**. **No full lanes
run**; qualification waits for an infrequent, declared milestone.

## Frozen boundary and correctness

Baseline `8f7833595dbacde77a61a0476d00ab585169ec12` / production `474906be73c88a05bd39be35f90d4b023fef99a6` is identical
in production to delivered green `257154a6`; its prepared Release inputs are
rehashed and reused. Candidate `cedda6b495335a5b831d61092eec8ac43b360128` / production
`b076a8aeb27e1848ad3b7afd4ae91a56f2a7b9c8` uses the same whole benchmark project
`c194ce88a7e4d52c576f3929ef82bce60bf37e2d`. Eighteen new independent regressions give candidate/delivered
tests `0ad697edb2ef437961e9c5bb2a7b790984152ba5`.

The synchronous context-aware PyRange path combines checked pulls and numeric
advancement in one active compiler-generated enumerator. It still checks before
pulls 1, 65, 129, etc., including the exhausted pull and before advancing the bound.
It uses the same arbitrary-precision bounds, one yielded box, OwnYield/OwnFreshInteger
funding, range allocation span, pool and lifetime. The original source iterator
remains available through CheckedSequence for materializer inspection; no known
count is introduced. Removing an active wrapper does not predict less allocation.
Generic/user/mutable-collection and cursor/async paths retain their original
operations. Opcode, per-instruction checkpoint/fuel/sweep order and host mediation
are unchanged. The iterator's finally region preserves terminal active disposal.

All eighteen exact cadence/exhaustion, active-disposal, first-check, denied-funding
span and fuel-before-cancellation regressions pass on unchanged production first;
the loaded baseline assembly is archived and hashed. They also pass the candidate.
An initial candidate build rejects duplicated primary-constructor span/context
captures (CS9107); the corrected wrapper uses its base storage. The source and
failure log are retained. No test executes or timing runs on that failed build.

Full frozen Debug passes **9,196 checks (1,439 white / 7,757 public)**; matching
VM Release passes **168 white / 296 public**, followed by six complete canonical
CPython cases, all before normal timing declarations. Existing suites cover range
ownership for retained/discarded heap yields, subscripts/reverse/slices, small-shell
calibration, sync/async cancellation/disposal, user protocols, generators, closure
lifetimes, argument errors and execution budgets.

## Complete-job timing

Microseconds per invocation; parentheses are IQR/median. Negative change means
faster than baseline. Both replicas and every control remain visible.

| Case / replica | Baseline µs (IQR) | Candidate µs (IQR) | CPython µs (IQR) | Change |
| --- | ---: | ---: | ---: | ---: |
| Empty / 1 | 23.327 (4.0%) | 23.071 (2.1%) | 1.496 (0.6%) | -1.10% |
| Integer loop / 1 | 1141.773 (0.9%) | 1101.449 (1.1%) | 619.782 (1.6%) | -3.53% |
| Positional calls / 1 | 565.875 (0.9%) | 556.273 (1.1%) | 119.994 (0.3%) | -1.70% |
| Keyword calls / 1 | 612.405 (1.2%) | 598.489 (1.2%) | 137.186 (0.3%) | -2.27% |
| Full stable sort + output / 1 | 2784.346 (6.9%) | 2675.522 (4.5%) | 304.234 (0.5%) | -3.91% |
| ASCII pipeline / 1 | 138.577 (0.6%) | 138.654 (3.4%) | 41.390 (0.4%) | +0.06% |
| Empty / 2 | 23.626 (3.0%) | 22.953 (4.2%) | 1.501 (0.5%) | -2.85% |
| Integer loop / 2 | 1152.961 (1.0%) | 1101.031 (0.9%) | 638.131 (0.1%) | -4.50% |
| Positional calls / 2 | 556.931 (0.6%) | 553.093 (1.2%) | 119.447 (0.4%) | -0.69% |
| Keyword calls / 2 | 618.099 (0.7%) | 611.627 (1.0%) | 137.264 (0.3%) | -1.05% |
| Full stable sort + output / 2 | 2629.485 (12.4%) | 2709.209 (6.3%) | 300.149 (1.0%) | +3.03% |
| ASCII pipeline / 2 | 137.391 (1.0%) | 138.346 (2.2%) | 41.510 (1.7%) | +0.70% |

Sources, fixtures and full goldens are identical, including full stable-sort
output. Compilation precedes timing; each invocation gets fresh guest state.
Pinned CPython 3.13.16 uses PGO/LTO, -I -S and ordinary GIL/GC. Lython uses
SDK 10.0.401 / CLR 10.0.12, workstation GC, Interactive latency and ordinary
worker tiering/PGO without overrides. API, materialization and containment costs
remain inside the complete job. Short diagnostics do not qualify independent
sessions or a general language speed ratio; no control time is subtracted.

## Separate allocation diagnostic

The frozen exact Compile/Invoke helper is rehashed and reused without rebuilding.
Process-wide GC.GetTotalAllocatedBytes(precise:true) brackets 100 invocations after
16 warmups per case/pass; two passes share each role's process. All 24 rows,
2,400 measured and 384 warmup invocations check complete output, without forced
GC. These managed observations are not exact per-type guest bytes or sessions.

| Case / pass | Baseline bytes/job | Candidate bytes/job | Bytes saved |
| --- | ---: | ---: | ---: |
| Empty / 1 | 29,400.00 | 29,400.00 | +0.00 |
| Integer loop / 1 | 1,079,424.64 | 1,079,432.64 | -8.00 |
| Positional calls / 1 | 1,146,354.48 | 1,146,362.48 | -8.00 |
| Keyword calls / 1 | 1,163,473.20 | 1,162,989.20 | +484.00 |
| Full stable sort + output / 1 | 3,538,216.32 | 3,541,299.68 | -3,083.36 |
| ASCII pipeline / 1 | 461,808.00 | 461,808.00 | +0.00 |
| Empty / 2 | 29,232.00 | 29,232.00 | +0.00 |
| Integer loop / 2 | 1,079,197.84 | 1,079,219.68 | -21.84 |
| Positional calls / 2 | 1,146,165.20 | 1,146,159.36 | +5.84 |
| Keyword calls / 2 | 1,162,799.36 | 1,162,807.36 | -8.00 |
| Full stable sort + output / 2 | 3,437,141.68 | 3,437,134.64 | +7.04 |
| ASCII pipeline / 2 | 461,808.00 | 461,808.00 | +0.00 |

## Separate native-code review

Two groups follow all ordinary timing and allocation. Only JIT disassembly flags
change; tiering/GC remain ordinary. All 24 helper rows verify complete outputs;
their allocation values are retained separately from ordinary allocation estimates.
The dump selects Execute and MoveNext in Lokad.Lython. All emitted bodies and
tiers remain visible, including iterator bodies outside the targeted range path;
missing tiers are not inferred. Inline summaries/call targets remain in evidence.

| Producer | Method | Emitted tier | Native bytes |
| --- | --- | --- | ---: |
| baseline | `System.Linq.Expressions.Compiler.ParameterList+<GetEnumerator>d__6:MoveNext` | Instrumented Tier0 | 426 |
| baseline | `System.Collections.Generic.Dictionary`2+Enumerator[System.Linq.Expressions.Compiler.BoundConstants+TypedConstant,int]:MoveNext` | Instrumented Tier0 | 378 |
| baseline | `System.Linq.Enumerable+<SelectIterator>d__251`2[System.__Canon,System.Collections.Generic.KeyValuePair`2[System.__Canon,int]]:MoveNext` | Instrumented Tier0 | 882 |
| baseline | `System.Linq.Enumerable+ArraySelectIterator`2[System.__Canon,char]:MoveNext` | Tier0 | 167 |
| baseline | `System.Collections.Generic.Dictionary`2+Enumerator[int,System.__Canon]:MoveNext` | Instrumented Tier0 | 458 |
| baseline | `System.Collections.Generic.Dictionary`2+Enumerator[int,int]:MoveNext` | Instrumented Tier0 | 350 |
| baseline | `Lokad.Lython.Frontend.StatementSyntaxTraversal+<EnumerateDirectExpressions>d__0:MoveNext` | Instrumented Tier0 | 11,926 |
| baseline | `Lokad.Lython.Frontend.StatementSyntaxTraversal+<EnumerateChildBodies>d__1:MoveNext` | Instrumented Tier0 | 2,740 |
| baseline | `Lokad.Lython.Frontend.PostponedAnnotationText+<Validate>d__1:MoveNext` | Instrumented Tier0 | 5,534 |
| baseline | `System.Collections.Generic.List`1+Enumerator[Lokad.Lython.Frontend.ExecutableInstruction]:MoveNext` | Tier0 | 193 |
| baseline | `ExecutionThreads+WorkItem:Execute` | Tier0 | 365 |
| baseline | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,099 |
| baseline | `Lokad.Lython.Frontend.ExpressionSyntaxTraversal+<EnumerateChildren>d__0:MoveNext` | Instrumented Tier0 | 11,767 |
| baseline | `Lokad.Lython.Frontend.AssignmentTargetFacts+<Reads>d__5:MoveNext` | Instrumented Tier0 | 1,434 |
| baseline | `Lokad.Lython.Frontend.StatementSyntaxTraversal+<<EnumerateDirectExpressions>g__EnumerateTargetExpressions|0_0>d:MoveNext` | Tier0 | 740 |
| baseline | `System.Collections.Generic.Dictionary`2+KeyCollection+Enumerator[System.__Canon,Lokad.Lython.Frontend.AbstractValue]:MoveNext` | Instrumented Tier0 | 336 |
| baseline | `System.Collections.Generic.Dictionary`2+Enumerator[System.__Canon,Lokad.Lython.Frontend.AbstractValue]:MoveNext` | Instrumented Tier0 | 537 |
| baseline | `System.Collections.Generic.Dictionary`2+Enumerator[System.__Canon,Lokad.Lython.Frontend.AbstractSequenceLengthBounds]:MoveNext` | Instrumented Tier0 | 507 |
| baseline | `System.Collections.Frozen.FrozenDictionary`2+Enumerator[Lokad.Lython.Frontend.StaticContracts+ModuleMemberName,System.__Canon]:MoveNext` | Tier0 | 80 |
| baseline | `PyIteration+<EnumerateChecked>d__5:MoveNext` | Instrumented Tier0 | 583 |
| baseline | `PyRange+<Iterate>d__22:MoveNext` | Instrumented Tier0 | 644 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 6,939 |
| baseline | `PyIteration+<EnumerateChecked>d__5:MoveNext` | Instrumented Tier0 | 583 |
| baseline | `PyRange+<Iterate>d__22:MoveNext` | Instrumented Tier0 | 644 |
| baseline | `PyIteration+<EnumerateChecked>d__5:MoveNext` | Tier1 | 512 |
| baseline | `System.SZGenericArrayEnumeratorBase:MoveNext` | Instrumented Tier1 | 83 |
| baseline | `System.Collections.Generic.List`1+Enumerator[System.__Canon]:MoveNext` | Instrumented Tier1 | 150 |
| baseline | `PyRange+<Iterate>d__22:MoveNext` | Tier1 | 759 |
| baseline | `System.Collections.Generic.Dictionary`2+Enumerator[System.__Canon,System.__Canon]:MoveNext` | Instrumented Tier1 | 230 |
| baseline | `System.Collections.Generic.Dictionary`2+ValueCollection+Enumerator[System.__Canon,Lokad.Lython.Frontend.AbstractValue]:MoveNext` | Instrumented Tier0 | 352 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 3,380 |
| baseline | `Lokad.Lython.Frontend.StatementSyntaxTraversal+<EnumerateChildBodies>d__1:MoveNext` | Instrumented Tier0 | 2,732 |
| baseline | `System.SZGenericArrayEnumeratorBase:MoveNext` | Tier1 | 27 |
| baseline | `System.Collections.Generic.List`1+Enumerator[System.__Canon]:MoveNext` | Tier1 | 106 |
| baseline | `System.Collections.Generic.Dictionary`2+Enumerator[System.__Canon,System.__Canon]:MoveNext` | Tier1 | 157 |
| baseline | `<DispatchLoweredExpressionAsync>d__1012:MoveNext` | Tier0 | 11,723 |
| baseline | `ExecutionThreads+WorkItem:Execute` | Instrumented Tier0 | 379 |
| baseline | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,098 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 2,664 |
| baseline | `Lokad.Lython.Frontend.ExpressionSyntaxTraversal+<EnumerateChildren>d__0:MoveNext` | Instrumented Tier0 | 11,744 |
| baseline | `Lokad.Lython.Frontend.StatementSyntaxTraversal+<EnumerateDirectExpressions>d__0:MoveNext` | Instrumented Tier0 | 11,905 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 2,683 |
| baseline | `System.Collections.Generic.List`1+Enumerator[Lokad.Lython.Frontend.ExecutableInstruction]:MoveNext` | Instrumented Tier0 | 238 |
| baseline | `SmallPyListStorage+<GetEnumerator>d__25:MoveNext` | Instrumented Tier0 | 328 |
| baseline | `PyIteration+<EnumerateIterator>d__18:MoveNext` | Instrumented Tier0 | 297 |
| baseline | `<AssignLoopTargetCoreAsync>d__1671:MoveNext` | Instrumented Tier0 | 6,100 |
| baseline | `ExecutableFrameState+<EnumerateLocals>d__8:MoveNext` | Instrumented Tier0 | 789 |
| baseline | `PyList+<GetEnumerator>d__63:MoveNext` | Instrumented Tier0 | 317 |
| baseline | `PyStableSort+Buffer+<GetEnumerator>d__8:MoveNext` | Instrumented Tier0 | 504 |
| baseline | `System.Collections.Generic.List`1+Enumerator[PyStableSort+Entry]:MoveNext` | Tier0 | 181 |
| baseline | `Lokad.Lython.Frontend.StatementSyntaxTraversal+<EnumerateChildBodies>d__1:MoveNext` | Tier1 | 1,256 |
| baseline | `System.GenericEmptyEnumeratorBase:MoveNext` | Instrumented Tier1 | 3 |
| baseline | `System.Linq.Enumerable+ListWhereIterator`1[System.__Canon]:MoveNext` | Instrumented Tier1 | 408 |
| baseline | `System.Linq.Enumerable+ConcatIterator`1[System.__Canon]:MoveNext` | Instrumented Tier1 | 618 |
| baseline | `System.Collections.Generic.Dictionary`2+KeyCollection+Enumerator[System.__Canon,Lokad.Lython.Frontend.AbstractValue]:MoveNext` | Instrumented Tier0 | 336 |
| baseline | `<DispatchLoweredExpressionAsync>d__1012:MoveNext` | Instrumented Tier0 | 14,340 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1 | 2,668 |
| baseline | `Lokad.Lython.Frontend.ExpressionSyntaxTraversal+<EnumerateChildren>d__0:MoveNext` | Tier1 | 5,779 |
| baseline | `System.Collections.Generic.HashSet`1+Enumerator[System.__Canon]:MoveNext` | Instrumented Tier1 | 206 |
| baseline | `Lokad.Lython.Frontend.StatementSyntaxTraversal+<EnumerateDirectExpressions>d__0:MoveNext` | Tier1 | 5,644 |
| baseline | `System.Runtime.CompilerServices.ConditionalWeakTable`2+Enumerator[System.__Canon,System.__Canon]:MoveNext` | Instrumented Tier1 | 503 |
| baseline | `System.Collections.Generic.Dictionary`2+Enumerator[System.__Canon,int]:MoveNext` | Instrumented Tier1 | 222 |
| baseline | `PyIteration+<EnumerateIterator>d__18:MoveNext` | Instrumented Tier0 | 297 |
| baseline | `<AssignLoopTargetCoreAsync>d__1671:MoveNext` | Instrumented Tier0 | 6,100 |
| baseline | `System.Linq.Enumerable+ArraySelectIterator`2[System.__Canon,System.__Canon]:MoveNext` | Instrumented Tier1 | 175 |
| baseline | `PyList+<GetEnumerator>d__63:MoveNext` | Instrumented Tier0 | 317 |
| baseline | `PyStableSort+Buffer+<GetEnumerator>d__8:MoveNext` | Instrumented Tier0 | 504 |
| baseline | `System.Collections.Generic.List`1+Enumerator[PyStableSort+Entry]:MoveNext` | Instrumented Tier0 | 226 |
| baseline | `ExecutionThreads+WorkItem:Execute` | Tier1 | 225 |
| baseline | `<DispatchLoweredExpressionAsync>d__1012:MoveNext` | Tier1 | 12,190 |
| baseline | `ExecutableFrameState+<EnumerateLocals>d__8:MoveNext` | Instrumented Tier0 | 789 |
| baseline | `SmallPyListStorage+<GetEnumerator>d__25:MoveNext` | Instrumented Tier0 | 328 |
| baseline | `System.Collections.Generic.Dictionary`2+Enumerator[System.__Canon,int]:MoveNext` | Tier1 | 149 |
| baseline | `PyIteration+<EnumerateIterator>d__18:MoveNext` | Tier1 | 789 |
| baseline | `<AssignLoopTargetCoreAsync>d__1671:MoveNext` | Tier1 | 6,327 |
| baseline | `PyList+<GetEnumerator>d__63:MoveNext` | Tier1 | 248 |
| baseline | `PyStableSort+Buffer+<GetEnumerator>d__8:MoveNext` | Tier1 | 349 |
| baseline | `System.Collections.Generic.List`1+Enumerator[PyStableSort+Entry]:MoveNext` | Tier1 | 105 |
| baseline | `System.Linq.Enumerable+ArraySelectIterator`2[System.__Canon,System.__Canon]:MoveNext` | Tier1 | 117 |
| baseline | `ExecutableFrameState+<EnumerateLocals>d__8:MoveNext` | Tier1 | 730 |
| baseline | `SmallPyListStorage+<GetEnumerator>d__25:MoveNext` | Tier1 | 126 |
| baseline | `StringMembers+StringLayoutSearchMemberProvider+<>c__DisplayClass11_1+<<TryGetMember>g__EnumerateParts|5>d:MoveNext` | Instrumented Tier0 | 1,309 |
| baseline | `PyList+DeferredSplitStorage+<BorrowForJoin>d__16:MoveNext` | Instrumented Tier0 | 515 |
| baseline | `System.Linq.Enumerable+ListWhereIterator`1[System.__Canon]:MoveNext` | Tier1 | 313 |
| baseline | `System.Collections.Generic.List`1+Enumerator[Lokad.Lython.Frontend.ExecutableInstruction]:MoveNext` | Tier1 | 117 |
| baseline | `System.Runtime.CompilerServices.ConditionalWeakTable`2+Enumerator[System.__Canon,System.__Canon]:MoveNext` | Tier1 | 359 |
| baseline | `System.Collections.Generic.HashSet`1+Enumerator[System.__Canon]:MoveNext` | Tier1 | 109 |
| baseline | `System.Linq.Enumerable+DistinctIterator`1[System.__Canon]:MoveNext` | Instrumented Tier1 | 745 |
| baseline | `Lokad.Lython.Frontend.AssignmentTargetFacts+<Reads>d__5:MoveNext` | Instrumented Tier0 | 1,432 |
| baseline | `System.GenericEmptyEnumeratorBase:MoveNext` | Tier1 | 3 |
| baseline | `System.Collections.Generic.Dictionary`2+KeyCollection+Enumerator[System.__Canon,Lokad.Lython.Frontend.AbstractValue]:MoveNext` | Tier1 | 121 |
| baseline | `System.Linq.Enumerable+ConcatIterator`1[System.__Canon]:MoveNext` | Tier1 | 762 |
| baseline | `System.Collections.Generic.Dictionary`2+ValueCollection+Enumerator[System.__Canon,Lokad.Lython.Frontend.AbstractValue]:MoveNext` | Instrumented Tier0 | 352 |
| baseline | `Lokad.Lython.Frontend.StatementSyntaxTraversal+<<EnumerateDirectExpressions>g__EnumerateTargetExpressions|0_0>d:MoveNext` | Instrumented Tier0 | 964 |
| baseline | `System.Linq.Enumerable+ArrayWhereSelectIterator`2[System.__Canon,System.__Canon]:MoveNext` | Instrumented Tier1 | 258 |
| candidate | `System.Linq.Expressions.Compiler.ParameterList+<GetEnumerator>d__6:MoveNext` | Instrumented Tier0 | 426 |
| candidate | `System.Collections.Generic.Dictionary`2+Enumerator[System.Linq.Expressions.Compiler.BoundConstants+TypedConstant,int]:MoveNext` | Instrumented Tier0 | 378 |
| candidate | `System.Linq.Enumerable+<SelectIterator>d__251`2[System.__Canon,System.Collections.Generic.KeyValuePair`2[System.__Canon,int]]:MoveNext` | Instrumented Tier0 | 882 |
| candidate | `System.Linq.Enumerable+ArraySelectIterator`2[System.__Canon,char]:MoveNext` | Tier0 | 167 |
| candidate | `System.Collections.Generic.Dictionary`2+Enumerator[int,System.__Canon]:MoveNext` | Instrumented Tier0 | 458 |
| candidate | `System.Collections.Generic.Dictionary`2+Enumerator[int,int]:MoveNext` | Instrumented Tier0 | 350 |
| candidate | `Lokad.Lython.Frontend.StatementSyntaxTraversal+<EnumerateDirectExpressions>d__0:MoveNext` | Instrumented Tier0 | 11,926 |
| candidate | `Lokad.Lython.Frontend.StatementSyntaxTraversal+<EnumerateChildBodies>d__1:MoveNext` | Instrumented Tier0 | 2,740 |
| candidate | `Lokad.Lython.Frontend.PostponedAnnotationText+<Validate>d__1:MoveNext` | Instrumented Tier0 | 5,534 |
| candidate | `System.Collections.Generic.List`1+Enumerator[Lokad.Lython.Frontend.ExecutableInstruction]:MoveNext` | Tier0 | 193 |
| candidate | `ExecutionThreads+WorkItem:Execute` | Tier0 | 365 |
| candidate | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,099 |
| candidate | `Lokad.Lython.Frontend.ExpressionSyntaxTraversal+<EnumerateChildren>d__0:MoveNext` | Instrumented Tier0 | 11,767 |
| candidate | `Lokad.Lython.Frontend.AssignmentTargetFacts+<Reads>d__5:MoveNext` | Instrumented Tier0 | 1,434 |
| candidate | `Lokad.Lython.Frontend.StatementSyntaxTraversal+<<EnumerateDirectExpressions>g__EnumerateTargetExpressions|0_0>d:MoveNext` | Tier0 | 740 |
| candidate | `System.Collections.Generic.Dictionary`2+KeyCollection+Enumerator[System.__Canon,Lokad.Lython.Frontend.AbstractValue]:MoveNext` | Instrumented Tier0 | 336 |
| candidate | `System.Collections.Generic.Dictionary`2+Enumerator[System.__Canon,Lokad.Lython.Frontend.AbstractValue]:MoveNext` | Instrumented Tier0 | 537 |
| candidate | `System.Collections.Generic.Dictionary`2+Enumerator[System.__Canon,Lokad.Lython.Frontend.AbstractSequenceLengthBounds]:MoveNext` | Instrumented Tier0 | 507 |
| candidate | `System.Collections.Frozen.FrozenDictionary`2+Enumerator[Lokad.Lython.Frontend.StaticContracts+ModuleMemberName,System.__Canon]:MoveNext` | Tier0 | 80 |
| candidate | `PyRange+<IterateChecked>d__23:MoveNext` | Instrumented Tier0 | 757 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 6,939 |
| candidate | `PyRange+<IterateChecked>d__23:MoveNext` | Instrumented Tier0 | 757 |
| candidate | `PyRange+<IterateChecked>d__23:MoveNext` | Tier1 | 1,071 |
| candidate | `System.SZGenericArrayEnumeratorBase:MoveNext` | Instrumented Tier1 | 83 |
| candidate | `System.Collections.Generic.List`1+Enumerator[System.__Canon]:MoveNext` | Instrumented Tier1 | 150 |
| candidate | `System.Collections.Generic.Dictionary`2+Enumerator[System.__Canon,System.__Canon]:MoveNext` | Instrumented Tier1 | 230 |
| candidate | `System.Collections.Generic.Dictionary`2+ValueCollection+Enumerator[System.__Canon,Lokad.Lython.Frontend.AbstractValue]:MoveNext` | Instrumented Tier0 | 352 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 3,395 |
| candidate | `Lokad.Lython.Frontend.StatementSyntaxTraversal+<EnumerateChildBodies>d__1:MoveNext` | Instrumented Tier0 | 2,732 |
| candidate | `System.SZGenericArrayEnumeratorBase:MoveNext` | Tier1 | 27 |
| candidate | `System.Collections.Generic.List`1+Enumerator[System.__Canon]:MoveNext` | Tier1 | 106 |
| candidate | `System.Collections.Generic.Dictionary`2+Enumerator[System.__Canon,System.__Canon]:MoveNext` | Tier1 | 157 |
| candidate | `<DispatchLoweredExpressionAsync>d__1012:MoveNext` | Tier0 | 11,723 |
| candidate | `ExecutionThreads+WorkItem:Execute` | Instrumented Tier0 | 379 |
| candidate | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,098 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 2,796 |
| candidate | `Lokad.Lython.Frontend.ExpressionSyntaxTraversal+<EnumerateChildren>d__0:MoveNext` | Instrumented Tier0 | 11,744 |
| candidate | `Lokad.Lython.Frontend.StatementSyntaxTraversal+<EnumerateDirectExpressions>d__0:MoveNext` | Instrumented Tier0 | 11,905 |
| candidate | `System.Collections.Generic.List`1+Enumerator[Lokad.Lython.Frontend.ExecutableInstruction]:MoveNext` | Instrumented Tier0 | 238 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 2,813 |
| candidate | `System.GenericEmptyEnumeratorBase:MoveNext` | Instrumented Tier1 | 3 |
| candidate | `Lokad.Lython.Frontend.StatementSyntaxTraversal+<EnumerateChildBodies>d__1:MoveNext` | Tier1 | 1,256 |
| candidate | `System.Linq.Enumerable+ListWhereIterator`1[System.__Canon]:MoveNext` | Instrumented Tier1 | 408 |
| candidate | `System.Linq.Enumerable+ConcatIterator`1[System.__Canon]:MoveNext` | Instrumented Tier1 | 618 |
| candidate | `System.Collections.Generic.Dictionary`2+KeyCollection+Enumerator[System.__Canon,Lokad.Lython.Frontend.AbstractValue]:MoveNext` | Instrumented Tier0 | 336 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1 | 2,677 |
| candidate | `<DispatchLoweredExpressionAsync>d__1012:MoveNext` | Instrumented Tier0 | 14,340 |
| candidate | `SmallPyListStorage+<GetEnumerator>d__25:MoveNext` | Instrumented Tier0 | 328 |
| candidate | `PyIteration+<EnumerateChecked>d__6:MoveNext` | Instrumented Tier0 | 583 |
| candidate | `PyIteration+<EnumerateIterator>d__19:MoveNext` | Instrumented Tier0 | 297 |
| candidate | `PyRange+<Iterate>d__22:MoveNext` | Instrumented Tier0 | 644 |
| candidate | `<AssignLoopTargetCoreAsync>d__1671:MoveNext` | Instrumented Tier0 | 6,100 |
| candidate | `ExecutableFrameState+<EnumerateLocals>d__8:MoveNext` | Instrumented Tier0 | 789 |
| candidate | `PyList+<GetEnumerator>d__63:MoveNext` | Instrumented Tier0 | 317 |
| candidate | `PyStableSort+Buffer+<GetEnumerator>d__8:MoveNext` | Instrumented Tier0 | 504 |
| candidate | `System.Collections.Generic.List`1+Enumerator[PyStableSort+Entry]:MoveNext` | Tier0 | 181 |
| candidate | `ExecutionThreads+WorkItem:Execute` | Tier1 | 225 |
| candidate | `System.Linq.Enumerable+ListWhereIterator`1[System.__Canon]:MoveNext` | Tier1 | 313 |
| candidate | `Lokad.Lython.Frontend.ExpressionSyntaxTraversal+<EnumerateChildren>d__0:MoveNext` | Tier1 | 5,803 |
| candidate | `System.Collections.Generic.HashSet`1+Enumerator[System.__Canon]:MoveNext` | Instrumented Tier1 | 206 |
| candidate | `Lokad.Lython.Frontend.StatementSyntaxTraversal+<EnumerateDirectExpressions>d__0:MoveNext` | Tier1 | 5,644 |
| candidate | `<DispatchLoweredExpressionAsync>d__1012:MoveNext` | Tier1 | 12,190 |
| candidate | `System.Runtime.CompilerServices.ConditionalWeakTable`2+Enumerator[System.__Canon,System.__Canon]:MoveNext` | Instrumented Tier1 | 503 |
| candidate | `System.Collections.Generic.Dictionary`2+Enumerator[System.__Canon,int]:MoveNext` | Instrumented Tier1 | 222 |
| candidate | `System.Linq.Enumerable+ArraySelectIterator`2[System.__Canon,System.__Canon]:MoveNext` | Instrumented Tier1 | 175 |
| candidate | `PyList+<GetEnumerator>d__63:MoveNext` | Instrumented Tier0 | 317 |
| candidate | `PyIteration+<EnumerateChecked>d__6:MoveNext` | Instrumented Tier0 | 583 |
| candidate | `PyIteration+<EnumerateIterator>d__19:MoveNext` | Instrumented Tier0 | 297 |
| candidate | `PyRange+<Iterate>d__22:MoveNext` | Instrumented Tier0 | 644 |
| candidate | `<AssignLoopTargetCoreAsync>d__1671:MoveNext` | Instrumented Tier0 | 6,100 |
| candidate | `PyStableSort+Buffer+<GetEnumerator>d__8:MoveNext` | Instrumented Tier0 | 504 |
| candidate | `System.Collections.Generic.List`1+Enumerator[PyStableSort+Entry]:MoveNext` | Instrumented Tier0 | 226 |
| candidate | `ExecutableFrameState+<EnumerateLocals>d__8:MoveNext` | Instrumented Tier0 | 789 |
| candidate | `SmallPyListStorage+<GetEnumerator>d__25:MoveNext` | Instrumented Tier0 | 328 |
| candidate | `PyList+<GetEnumerator>d__63:MoveNext` | Tier1 | 248 |
| candidate | `System.Collections.Generic.Dictionary`2+Enumerator[System.__Canon,int]:MoveNext` | Tier1 | 149 |
| candidate | `System.Linq.Enumerable+ArraySelectIterator`2[System.__Canon,System.__Canon]:MoveNext` | Tier1 | 117 |
| candidate | `PyIteration+<EnumerateChecked>d__6:MoveNext` | Tier1 | 1,318 |
| candidate | `PyIteration+<EnumerateIterator>d__19:MoveNext` | Tier1 | 789 |
| candidate | `PyRange+<Iterate>d__22:MoveNext` | Tier1 | 787 |
| candidate | `<AssignLoopTargetCoreAsync>d__1671:MoveNext` | Tier1 | 6,329 |
| candidate | `PyStableSort+Buffer+<GetEnumerator>d__8:MoveNext` | Tier1 | 349 |
| candidate | `System.Collections.Generic.List`1+Enumerator[PyStableSort+Entry]:MoveNext` | Tier1 | 105 |
| candidate | `ExecutableFrameState+<EnumerateLocals>d__8:MoveNext` | Tier1 | 730 |
| candidate | `SmallPyListStorage+<GetEnumerator>d__25:MoveNext` | Tier1 | 126 |
| candidate | `StringMembers+StringLayoutSearchMemberProvider+<>c__DisplayClass11_1+<<TryGetMember>g__EnumerateParts|5>d:MoveNext` | Instrumented Tier0 | 1,309 |
| candidate | `PyList+DeferredSplitStorage+<BorrowForJoin>d__16:MoveNext` | Instrumented Tier0 | 515 |
| candidate | `System.Collections.Generic.List`1+Enumerator[Lokad.Lython.Frontend.ExecutableInstruction]:MoveNext` | Tier1 | 117 |
| candidate | `System.Runtime.CompilerServices.ConditionalWeakTable`2+Enumerator[System.__Canon,System.__Canon]:MoveNext` | Tier1 | 359 |
| candidate | `System.Collections.Generic.HashSet`1+Enumerator[System.__Canon]:MoveNext` | Tier1 | 109 |
| candidate | `System.Linq.Enumerable+DistinctIterator`1[System.__Canon]:MoveNext` | Instrumented Tier1 | 745 |
| candidate | `Lokad.Lython.Frontend.AssignmentTargetFacts+<Reads>d__5:MoveNext` | Instrumented Tier0 | 1,432 |
| candidate | `System.GenericEmptyEnumeratorBase:MoveNext` | Tier1 | 3 |
| candidate | `System.Collections.Generic.Dictionary`2+KeyCollection+Enumerator[System.__Canon,Lokad.Lython.Frontend.AbstractValue]:MoveNext` | Tier1 | 121 |
| candidate | `System.Linq.Enumerable+ConcatIterator`1[System.__Canon]:MoveNext` | Tier1 | 762 |
| candidate | `System.Collections.Generic.Dictionary`2+ValueCollection+Enumerator[System.__Canon,Lokad.Lython.Frontend.AbstractValue]:MoveNext` | Instrumented Tier0 | 352 |
| candidate | `Lokad.Lython.Frontend.StatementSyntaxTraversal+<<EnumerateDirectExpressions>g__EnumerateTargetExpressions|0_0>d:MoveNext` | Instrumented Tier0 | 964 |
| candidate | `System.Linq.Enumerable+ArrayWhereSelectIterator`2[System.__Canon,System.__Canon]:MoveNext` | Instrumented Tier1 | 258 |
| candidate | `StringMembers+StringLayoutSearchMemberProvider+<>c__DisplayClass11_1+<<TryGetMember>g__EnumerateParts|5>d:MoveNext` | Instrumented Tier0 | 1,309 |
| candidate | `PyList+DeferredSplitStorage+<BorrowForJoin>d__16:MoveNext` | Instrumented Tier0 | 515 |

Final emitted Tier1 Frame.Execute changes 2,668 to 2,677 bytes (+9 / 0.34%); both retain 19 PGO / 55 single-block / 5 other inlinees. The fused range MoveNext emits 1,071 bytes (16 / 44 / 0 inlinees), versus baseline's separately emitted checked/range MoveNext bodies of 512 / 759 bytes. Candidate generic checked/range fallback bodies still emit: their Tier1 sizes are 1,318 / 787 with different PGO/inline profiles, versus baseline 512 / 759. Early larger frame OSR stays 6,939 bytes; later bodies differ, including candidate 2,796/2,813 versus baseline 2,664/2,683. Every emitted body/tier, inline summary and call target is retained. Do not subtract or sum these to claim total resident code-cache savings, dynamic call frequency or causal CPU gains; repeated short target timing supports the integration decision.

## Audit and cleanup

The independent audit recalculates all **521 normal responses**,
request IDs/counts, medians/IQRs, hashes/goldens, frozen producers and ordinary
worker defaults. It checks gate completion before declaration and journal ordering
through the separate diagnostics. All **18 owned services** are terminal,
**58 recorded PIDs** absent, both producers/helper rehashed and the shared VM
lease freshly free. No observation is discarded or recollected.

Raw declarations, tests, journals, native listings and cleanup proof stay private
under `.git/agent-notes/checked-range-iteration-20261010/`.
Maintained [evidence](evidence.json) carries audited observations and supporting
hashes. Further runtime work, original CSV/pipeline causes, external Utf8Regex
integration and milestone/secondary qualification remain pending.
