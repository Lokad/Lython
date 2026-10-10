# Inline operand-stack storage

Rejected: managed call allocation falls about 9.86–10.01%, but both replicas have slower loops (11.99%/15.69%), positional calls (10.19%/6.93%) and keyword calls (2.12%/8.33%), beyond their spreads. The candidate stays isolated; fifteen independently baseline-passing regressions are delivered. Every control, allocation observation and native body/tier is retained. Short diagnostics do not qualify independent sessions or a general Python ratio.

Twelve paired micros stop in **8.04–8.07 seconds**, including
cleanup. Separate ordinary allocation groups stop in **4.03 / 4.03 seconds**;
native-code groups in **4.04 / 4.04 seconds**.
Every collection has a **30-second external process-group cap**. **No full lanes
run**; qualification waits for an infrequent, declared milestone.

## Frozen boundary and correctness

Baseline `096e33bdb4b2be7ab6111000b7e3845a67517c99` / production `6bc4cc2110d9e02d3e110cb77e6c7eb6cfc737af` is identical
in production to delivered green `f9c2efa7`; its prepared Release inputs are
rehashed and reused. Candidate `f37042204d503f4998853496568fbd0ce2ad238f` / production
`a68b17197dce02d28df865cd2980c674a254fdfb` has tests `0450b52fc004263cbb08a6fe9b67a576d8d25f17` and unchanged
whole benchmark project `c194ce88a7e4d52c576f3929ef82bce60bf37e2d`.

Four shallow operand references live inside the interpreter. The fifth push
spills to an array with the original capacity hint; later growth stays doubled.
Spill clears the inline copies, pop clears its slot, and unwinding disposes
temporary entries in original order before clearing. A disposal exception leaves
the original entries/count available for propagation. Generator/suspended storage
remains frame-owned. No pooling, unsafe writes, numeric representation, funding,
opcode/checkpoint, local/cell mirroring or host-mediation change is introduced.

Independent regression `c71cf822` adds fifteen reference/growth/reuse and disposal
order/failure checks. All pass on unchanged baseline production first; the
versioned baseline assembly and full receipts are archived. The independent test tree
is delivered with baseline production; the runtime candidate remains isolated. The frozen
VM baseline producer predates these tests but has identical baseline production.
Full frozen candidate Debug passes **9,211 checks (1,454 white / 7,757 public)**;
matching VM Release passes **183 white / 296 public**, followed by six complete canonical
CPython cases, all before normal timing declaration. Existing exact iteration,
funding/cancellation, retained/dropped ownership, generic/user protocols,
generator/closure, reentrant calls and async host suites remain in the gates.

## Fresh accepted-runtime call CPU leads

Before the candidate, two separately declared ten-second positional/keyword
captures reuse the accepted baseline producer and frozen parser without rebuilds.
Their whole groups stop in **14.07 / 16.09 seconds**, under **30-second caps**;
all **1,152 responses / 36,680 invocations** match complete output. They retain
**966 / 1,201 samples**, zero reported losses/missing stacks and **6.21% / 7.08%
unresolved leaves**. Interpreter Execute is 16.05% / 14.07% exclusive; full
guest-frame execution is 69.98% / 72.27% inclusive. Allocation ticks lead with
interpreter/context objects and object arrays. These are diagnostic leads,
not removable shares or predicted gains. Inclusive stacks overlap; profiler,
kernel and background JIT costs remain visible. Allocation ticks are sampled,
not exact bytes by guest type. GC pairs are incomplete, so no complete GC counts
or pauses are claimed. Inputs/helpers rehash; two services terminal, six PIDs
absent and the VM lease freshly free. There is no before/after CPU claim.

## Complete-job timing

Microseconds per invocation; parentheses are IQR/median. Negative change means
faster than baseline. Both replicas and every control remain visible.

| Case / replica | Baseline µs (IQR) | Candidate µs (IQR) | CPython µs (IQR) | Change |
| --- | ---: | ---: | ---: | ---: |
| Empty / 1 | 23.293 (4.2%) | 23.855 (2.5%) | 1.479 (0.4%) | +2.41% |
| Integer loop / 1 | 1108.469 (4.5%) | 1241.358 (4.6%) | 683.407 (5.0%) | +11.99% |
| Positional calls / 1 | 545.046 (0.7%) | 600.607 (1.2%) | 122.522 (0.5%) | +10.19% |
| Keyword calls / 1 | 607.449 (0.5%) | 620.299 (1.2%) | 140.138 (0.2%) | +2.12% |
| Full stable sort + output / 1 | 2796.607 (9.2%) | 2632.306 (3.7%) | 302.413 (0.9%) | -5.87% |
| ASCII pipeline / 1 | 145.159 (2.8%) | 139.241 (1.2%) | 41.345 (0.5%) | -4.08% |
| Empty / 2 | 23.777 (6.4%) | 23.101 (2.7%) | 1.502 (1.3%) | -2.84% |
| Integer loop / 2 | 1069.598 (1.0%) | 1237.397 (0.9%) | 623.461 (2.0%) | +15.69% |
| Positional calls / 2 | 566.561 (1.0%) | 605.820 (1.0%) | 119.923 (0.5%) | +6.93% |
| Keyword calls / 2 | 606.904 (0.7%) | 657.448 (0.8%) | 136.165 (0.7%) | +8.33% |
| Full stable sort + output / 2 | 2970.868 (2.1%) | 2572.924 (7.4%) | 301.944 (1.2%) | -13.39% |
| ASCII pipeline / 2 | 137.816 (1.2%) | 138.901 (1.9%) | 41.305 (0.2%) | +0.79% |

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
| Empty / 1 | 29,400.00 | 29,344.00 | +56.00 |
| Integer loop / 1 | 1,079,432.40 | 1,079,376.40 | +56.00 |
| Positional calls / 1 | 1,146,362.72 | 1,031,625.12 | +114,737.60 |
| Keyword calls / 1 | 1,162,989.20 | 1,048,347.04 | +114,642.16 |
| Full stable sort + output / 1 | 3,538,675.68 | 3,517,168.32 | +21,507.36 |
| ASCII pipeline / 1 | 461,808.00 | 461,840.00 | -32.00 |
| Empty / 2 | 29,232.00 | 29,176.00 | +56.00 |
| Integer loop / 2 | 1,079,219.68 | 1,079,163.68 | +56.00 |
| Positional calls / 2 | 1,146,159.36 | 1,031,443.04 | +114,716.32 |
| Keyword calls / 2 | 1,162,807.36 | 1,048,179.04 | +114,628.32 |
| Full stable sort + output / 2 | 3,437,134.00 | 3,437,072.88 | +61.12 |
| ASCII pipeline / 2 | 461,808.00 | 461,840.00 | -32.00 |

## Separate native-code review

Two groups follow all ordinary timing and allocation. Only JIT disassembly flags
change; tiering/GC remain ordinary. All 24 helper rows verify complete outputs;
their allocation values are retained separately from ordinary allocation estimates.
The dump selects Execute and operand-stack transfer/cleanup methods in Lokad.Lython.
All emitted bodies and tiers remain visible;
missing tiers are not inferred. Inline summaries/call targets remain in evidence.

| Producer | Method | Emitted tier | Native bytes |
| --- | --- | --- | ---: |
| baseline | `System.Collections.Generic.Dictionary`2[System.__Canon,int]:get_Item` | Tier0 | 164 |
| baseline | `System.Collections.Generic.Dictionary`2[int,System.__Canon]:get_Item` | Tier0 | 89 |
| baseline | `System.Collections.Generic.Stack`1[int]:Push` | Tier0 | 132 |
| baseline | `System.SZArrayHelper:get_Item[Lokad.Parsing.Lexer.LexerRule`1[int]]` | Tier0 | 112 |
| baseline | `System.Collections.Generic.List`1[Lokad.Parsing.Lexer.LexerToken`1[int]]:get_Item` | Tier0 | 94 |
| baseline | `System.Collections.Generic.Stack`1[int]:Peek` | Tier0 | 99 |
| baseline | `System.Collections.Generic.Stack`1[System.ValueTuple`2[System.__Canon,int]]:Push` | Tier0 | 155 |
| baseline | `System.Collections.Generic.Stack`1[int]:Pop` | Tier0 | 122 |
| baseline | `System.Collections.Generic.List`1[Lokad.Lython.Frontend.ExecutableInstruction]:get_Item` | Tier0 | 107 |
| baseline | `ExecutionThreads+WorkItem:Execute` | Tier0 | 365 |
| baseline | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,136 |
| baseline | `ExecutableValueStack:RemoveTail` | Instrumented Tier0 | 391 |
| baseline | `System.Collections.Generic.Stack`1[System.ValueTuple`3[System.__Canon,System.__Canon,bool]]:Push` | Tier0 | 185 |
| baseline | `System.Collections.Generic.Dictionary`2[System.__Canon,Lokad.Lython.Frontend.AbstractValue]:get_Item` | Tier0 | 228 |
| baseline | `System.Collections.Generic.Stack`1[Lokad.Lython.Frontend.ExecutableScript+Builder+LoopContext]:Push` | Tier0 | 152 |
| baseline | `System.Collections.Generic.Stack`1[Lokad.Lython.Frontend.ExecutableScript+Builder+LoopContext]:Pop` | Tier0 | 138 |
| baseline | `System.Numerics.Vector`1[int]:get_Item` | Tier0 | 53 |
| baseline | `ExecutableValueStack:Push` | Tier0 | 174 |
| baseline | `LythonRuntime:Pop` | Tier0 | 103 |
| baseline | `ExecutableValueStack:Pop` | Tier0 | 131 |
| baseline | `ExecutableValueStack:get_Item` | Tier0 | 71 |
| baseline | `ExecutableValueStack:Peek` | Tier0 | 103 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 8,170 |
| baseline | `LythonRuntime:Pop` | Instrumented Tier0 | 103 |
| baseline | `ExecutableValueStack:Pop` | Instrumented Tier0 | 131 |
| baseline | `ExecutableValueStack:Push` | Instrumented Tier0 | 204 |
| baseline | `System.Collections.Generic.List`1[System.__Canon]:get_Item` | Instrumented Tier1 | 84 |
| baseline | `ExecutableValueStack:Peek` | Instrumented Tier0 | 103 |
| baseline | `LythonRuntime:Pop` | Tier1 | 178 |
| baseline | `ExecutableValueStack:Pop` | Tier1 | 125 |
| baseline | `ExecutableValueStack:Push` | Tier1 | 91 |
| baseline | `System.Collections.Generic.List`1[System.__Canon]:get_Item` | Tier1 | 40 |
| baseline | `System.SZArrayHelper:get_Item[System.__Canon]` | Instrumented Tier1 | 71 |
| baseline | `ExecutableValueStack:Peek` | Tier1 | 104 |
| baseline | `System.Collections.Generic.List`1[System.Nullable`1[Lokad.Lython.Frontend.AbstractValue]]:get_Item` | Tier0 | 97 |
| baseline | `ArgumentPresence:get_Item` | Tier0 | 134 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 3,891 |
| baseline | `ExecutableValueStack:get_Item` | Instrumented Tier0 | 71 |
| baseline | `ExecutableValueStack:RemoveTail` | Instrumented Tier0 | 390 |
| baseline | `System.SZArrayHelper:get_Item[System.__Canon]` | Tier1 | 27 |
| baseline | `ExecutionThreads+WorkItem:Execute` | Instrumented Tier0 | 379 |
| baseline | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,135 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 2,820 |
| baseline | `System.SZArrayHelper:get_Item[Lokad.Parsing.Lexer.LexerRule`1[int]]` | Instrumented Tier0 | 142 |
| baseline | `System.Reflection.Emit.DynamicScope:get_Item` | Instrumented Tier1 | 126 |
| baseline | `System.Collections.Generic.List`1[Lokad.Parsing.Lexer.LexerToken`1[int]]:get_Item` | Instrumented Tier0 | 124 |
| baseline | `System.Collections.Generic.List`1[int]:get_Item` | Instrumented Tier1 | 83 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 2,839 |
| baseline | `ArgumentPresence:get_Item` | Instrumented Tier0 | 164 |
| baseline | `ExecutableValueStack:get_Item` | Tier1 | 101 |
| baseline | `ExecutableValueStack:RemoveTail` | Tier1 | 335 |
| baseline | `System.Collections.Generic.Stack`1[int]:Peek` | Instrumented Tier0 | 129 |
| baseline | `System.Collections.Generic.Stack`1[System.ValueTuple`3[System.__Canon,System.__Canon,bool]]:Push` | Instrumented Tier0 | 219 |
| baseline | `System.Collections.Generic.Dictionary`2[System.__Canon,Lokad.Lython.Frontend.AbstractValue]:get_Item` | Instrumented Tier0 | 258 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1 | 2,682 |
| baseline | `ArgumentPresence:get_Item` | Tier1 | 112 |
| baseline | `PyList:get_Item` | Tier0 | 48 |
| baseline | `ArrayPyListStorage:get_Item` | Tier0 | 43 |
| baseline | `System.Collections.Generic.List`1[PyStableSort+Entry]:get_Item` | Tier0 | 101 |
| baseline | `System.SZArrayHelper:get_Item[PyStableSort+Entry]` | Tier0 | 111 |
| baseline | `ExecutionThreads+WorkItem:Execute` | Tier1 | 225 |
| baseline | `System.SZArrayHelper:get_Item[Lokad.Parsing.Lexer.LexerRule`1[int]]` | Tier1 | 48 |
| baseline | `System.Collections.Generic.List`1[Lokad.Parsing.Lexer.LexerToken`1[int]]:get_Item` | Tier1 | 72 |
| baseline | `System.Collections.Generic.List`1[int]:get_Item` | Tier1 | 39 |
| baseline | `System.Collections.Immutable.ImmutableArray`1[System.__Canon]:get_Item` | Instrumented Tier1 | 27 |
| baseline | `PyList:get_Item` | Instrumented Tier0 | 93 |
| baseline | `ArrayPyListStorage:get_Item` | Instrumented Tier0 | 43 |
| baseline | `System.Collections.Generic.List`1[PyStableSort+Entry]:get_Item` | Instrumented Tier0 | 131 |
| baseline | `System.SZArrayHelper:get_Item[PyStableSort+Entry]` | Instrumented Tier0 | 141 |
| baseline | `PyList:get_Item` | Tier1 | 77 |
| baseline | `ArrayPyListStorage:get_Item` | Tier1 | 44 |
| baseline | `System.Collections.Generic.List`1[PyStableSort+Entry]:get_Item` | Tier1 | 54 |
| baseline | `System.SZArrayHelper:get_Item[PyStableSort+Entry]` | Tier1 | 41 |
| baseline | `System.Collections.Generic.Stack`1[System.ValueTuple`3[System.__Canon,System.__Canon,bool]]:Push` | Tier1 | 112 |
| baseline | `System.Collections.Immutable.ImmutableArray`1[System.__Canon]:get_Item` | Tier1 | 27 |
| baseline | `System.Collections.Generic.Stack`1[int]:Push` | Instrumented Tier0 | 162 |
| baseline | `System.Collections.Generic.Stack`1[int]:Peek` | Tier1 | 33 |
| baseline | `System.Collections.Generic.Stack`1[System.ValueTuple`2[System.__Canon,int]]:Push` | Instrumented Tier0 | 185 |
| baseline | `System.Collections.Generic.Dictionary`2[System.__Canon,Lokad.Lython.Frontend.AbstractValue]:get_Item` | Tier1 | 815 |
| baseline | `System.Collections.Generic.Dictionary`2[int,int]:get_Item` | Instrumented Tier1 | 74 |
| baseline | `System.Collections.Generic.Stack`1[int]:Pop` | Instrumented Tier0 | 152 |
| baseline | `System.Collections.Generic.List`1[Lokad.Lython.Frontend.ExecutableInstruction]:get_Item` | Instrumented Tier0 | 137 |
| candidate | `System.Collections.Generic.Dictionary`2[System.__Canon,int]:get_Item` | Tier0 | 164 |
| candidate | `System.Collections.Generic.Dictionary`2[int,System.__Canon]:get_Item` | Tier0 | 89 |
| candidate | `System.Collections.Generic.Stack`1[int]:Push` | Tier0 | 132 |
| candidate | `System.SZArrayHelper:get_Item[Lokad.Parsing.Lexer.LexerRule`1[int]]` | Tier0 | 112 |
| candidate | `System.Collections.Generic.List`1[Lokad.Parsing.Lexer.LexerToken`1[int]]:get_Item` | Tier0 | 94 |
| candidate | `System.Collections.Generic.Stack`1[int]:Peek` | Tier0 | 99 |
| candidate | `System.Collections.Generic.Stack`1[System.ValueTuple`2[System.__Canon,int]]:Push` | Tier0 | 155 |
| candidate | `System.Collections.Generic.Stack`1[int]:Pop` | Tier0 | 122 |
| candidate | `System.Collections.Generic.List`1[Lokad.Lython.Frontend.ExecutableInstruction]:get_Item` | Tier0 | 107 |
| candidate | `ExecutionThreads+WorkItem:Execute` | Tier0 | 365 |
| candidate | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,136 |
| candidate | `ExecutableValueStack:RemoveTail` | Instrumented Tier0 | 586 |
| candidate | `System.Collections.Generic.Stack`1[System.ValueTuple`3[System.__Canon,System.__Canon,bool]]:Push` | Tier0 | 185 |
| candidate | `System.Collections.Generic.Dictionary`2[System.__Canon,Lokad.Lython.Frontend.AbstractValue]:get_Item` | Tier0 | 228 |
| candidate | `System.Collections.Generic.Stack`1[Lokad.Lython.Frontend.ExecutableScript+Builder+LoopContext]:Push` | Tier0 | 152 |
| candidate | `System.Collections.Generic.Stack`1[Lokad.Lython.Frontend.ExecutableScript+Builder+LoopContext]:Pop` | Tier0 | 138 |
| candidate | `System.Numerics.Vector`1[int]:get_Item` | Tier0 | 53 |
| candidate | `ExecutableValueStack:Push` | Tier0 | 479 |
| candidate | `ExecutableValueStack:SetInline` | Tier0 | 195 |
| candidate | `LythonRuntime:Pop` | Tier0 | 103 |
| candidate | `ExecutableValueStack:Pop` | Tier0 | 159 |
| candidate | `ExecutableValueStack:get_Item` | Tier0 | 123 |
| candidate | `ExecutableValueStack:GetInline` | Tier0 | 203 |
| candidate | `ExecutableValueStack:Peek` | Tier0 | 49 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 8,281 |
| candidate | `ExecutableValueStack:SetInline` | Instrumented Tier0 | 258 |
| candidate | `ExecutableValueStack:GetInline` | Instrumented Tier0 | 299 |
| candidate | `LythonRuntime:Pop` | Instrumented Tier0 | 103 |
| candidate | `ExecutableValueStack:Pop` | Instrumented Tier0 | 189 |
| candidate | `ExecutableValueStack:get_Item` | Instrumented Tier0 | 153 |
| candidate | `ExecutableValueStack:Push` | Instrumented Tier0 | 539 |
| candidate | `System.Collections.Generic.List`1[System.__Canon]:get_Item` | Instrumented Tier1 | 84 |
| candidate | `ExecutableValueStack:SetInline` | Tier1 | 143 |
| candidate | `ExecutableValueStack:GetInline` | Tier1 | 122 |
| candidate | `LythonRuntime:Pop` | Tier1 | 247 |
| candidate | `ExecutableValueStack:Pop` | Tier1 | 190 |
| candidate | `ExecutableValueStack:get_Item` | Tier1 | 114 |
| candidate | `ExecutableValueStack:Push` | Tier1 | 255 |
| candidate | `ExecutableValueStack:Peek` | Instrumented Tier0 | 49 |
| candidate | `System.Collections.Generic.List`1[System.__Canon]:get_Item` | Tier1 | 40 |
| candidate | `System.SZArrayHelper:get_Item[System.__Canon]` | Instrumented Tier1 | 71 |
| candidate | `ExecutableValueStack:Peek` | Tier1 | 119 |
| candidate | `System.Collections.Generic.List`1[System.Nullable`1[Lokad.Lython.Frontend.AbstractValue]]:get_Item` | Tier0 | 97 |
| candidate | `ArgumentPresence:get_Item` | Tier0 | 134 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 4,410 |
| candidate | `ExecutableValueStack:RemoveTail` | Instrumented Tier0 | 585 |
| candidate | `System.SZArrayHelper:get_Item[System.__Canon]` | Tier1 | 27 |
| candidate | `ExecutionThreads+WorkItem:Execute` | Instrumented Tier0 | 379 |
| candidate | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,135 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 2,853 |
| candidate | `System.SZArrayHelper:get_Item[Lokad.Parsing.Lexer.LexerRule`1[int]]` | Instrumented Tier0 | 142 |
| candidate | `System.Reflection.Emit.DynamicScope:get_Item` | Instrumented Tier1 | 126 |
| candidate | `System.Collections.Generic.List`1[Lokad.Parsing.Lexer.LexerToken`1[int]]:get_Item` | Instrumented Tier0 | 124 |
| candidate | `System.Collections.Generic.List`1[int]:get_Item` | Instrumented Tier1 | 83 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 2,864 |
| candidate | `ArgumentPresence:get_Item` | Instrumented Tier0 | 164 |
| candidate | `PyList:get_Item` | Tier0 | 48 |
| candidate | `ArrayPyListStorage:get_Item` | Tier0 | 43 |
| candidate | `System.Collections.Generic.List`1[PyStableSort+Entry]:get_Item` | Tier0 | 101 |
| candidate | `System.SZArrayHelper:get_Item[PyStableSort+Entry]` | Tier0 | 111 |
| candidate | `ExecutableValueStack:RemoveTail` | Tier1 | 343 |
| candidate | `System.Collections.Generic.Stack`1[int]:Peek` | Instrumented Tier0 | 129 |
| candidate | `System.Collections.Generic.Stack`1[System.ValueTuple`3[System.__Canon,System.__Canon,bool]]:Push` | Instrumented Tier0 | 219 |
| candidate | `System.Collections.Generic.Dictionary`2[System.__Canon,Lokad.Lython.Frontend.AbstractValue]:get_Item` | Instrumented Tier0 | 258 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1 | 2,852 |
| candidate | `ArgumentPresence:get_Item` | Tier1 | 112 |
| candidate | `ExecutionThreads+WorkItem:Execute` | Tier1 | 225 |
| candidate | `System.SZArrayHelper:get_Item[Lokad.Parsing.Lexer.LexerRule`1[int]]` | Tier1 | 48 |
| candidate | `System.Collections.Generic.List`1[Lokad.Parsing.Lexer.LexerToken`1[int]]:get_Item` | Tier1 | 72 |
| candidate | `System.Collections.Generic.List`1[int]:get_Item` | Tier1 | 39 |
| candidate | `System.Collections.Immutable.ImmutableArray`1[System.__Canon]:get_Item` | Instrumented Tier1 | 27 |
| candidate | `PyList:get_Item` | Instrumented Tier0 | 93 |
| candidate | `ArrayPyListStorage:get_Item` | Instrumented Tier0 | 43 |
| candidate | `System.Collections.Generic.List`1[PyStableSort+Entry]:get_Item` | Instrumented Tier0 | 131 |
| candidate | `System.SZArrayHelper:get_Item[PyStableSort+Entry]` | Instrumented Tier0 | 141 |
| candidate | `PyList:get_Item` | Tier1 | 77 |
| candidate | `ArrayPyListStorage:get_Item` | Tier1 | 44 |
| candidate | `System.Collections.Generic.List`1[PyStableSort+Entry]:get_Item` | Tier1 | 54 |
| candidate | `System.SZArrayHelper:get_Item[PyStableSort+Entry]` | Tier1 | 41 |
| candidate | `System.Collections.Immutable.ImmutableArray`1[System.__Canon]:get_Item` | Tier1 | 27 |
| candidate | `System.Collections.Generic.Stack`1[int]:Push` | Instrumented Tier0 | 162 |
| candidate | `System.Collections.Generic.Stack`1[int]:Peek` | Tier1 | 33 |
| candidate | `System.Collections.Generic.Stack`1[System.ValueTuple`2[System.__Canon,int]]:Push` | Instrumented Tier0 | 185 |
| candidate | `System.Collections.Generic.Stack`1[System.ValueTuple`3[System.__Canon,System.__Canon,bool]]:Push` | Tier1 | 112 |
| candidate | `System.Collections.Generic.Dictionary`2[int,int]:get_Item` | Instrumented Tier1 | 74 |
| candidate | `System.Collections.Generic.Stack`1[int]:Pop` | Instrumented Tier0 | 152 |
| candidate | `System.Collections.Generic.List`1[Lokad.Lython.Frontend.ExecutableInstruction]:get_Item` | Instrumented Tier0 | 137 |
| candidate | `System.Collections.Generic.Dictionary`2[System.__Canon,Lokad.Lython.Frontend.AbstractValue]:get_Item` | Tier1 | 806 |

Final emitted Tier1 Frame.Execute grows 2,682 to 2,852 bytes (+170 / 6.34%); inline summaries change 19 PGO / 55 single-block / 5 other to 20 / 58 / 5. Stack Push grows 91 to 255 bytes, Pop 125 to 190, Peek 104 to 119, indexer 101 to 114 and RemoveTail 335 to 343; added GetInline/SetInline emit 122/143 bytes separately. Early frame OSR grows 8,170 to 8,281 and 3,891 to 4,410; later bodies change 2,820/2,839 to 2,853/2,864. All bodies/tiers, inline summaries and call targets stay visible. These profile-dependent sizes do not prove total resident code-cache cost, dynamic call frequency or CPU causality; the repeated target regressions independently reject the candidate.

## Audit and cleanup

The independent audit recalculates all **516 normal responses**,
request IDs/counts, medians/IQRs, hashes/goldens, frozen producers and ordinary
worker defaults. It checks gate completion before declaration and journal ordering
through the separate diagnostics. All **18 owned services** are terminal,
**58 recorded PIDs** absent, both producers/helper rehashed and the shared VM
lease freshly free. No observation is discarded or recollected.

Raw declarations, tests, journals, native listings and cleanup proof stay private
under `.git/agent-notes/inline-operand-stack-20261010/`.
Maintained [evidence](evidence.json) carries audited observations and supporting
hashes. Further runtime work, original CSV/pipeline causes, external Utf8Regex
integration and milestone/secondary qualification remain pending.
