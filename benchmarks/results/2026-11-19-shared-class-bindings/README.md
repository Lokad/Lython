# R687 shared class and function binding facts

Class-cell discovery can inspect a nested declaration before lexical
preparation reaches it. `5e1cc5f5b` computes declaration bindings once and
reuses the membership sets and ordered slots when the enclosing class-cell
requirement is resolved. The comparison uses `47fbd2c1b`, which has the
same runtime and test sources as the delivered `6772c0cf2` checkpoint.

| Workload | Reference allocation | Candidate allocation | Change |
| --- | ---: | ---: | ---: |
| Nested closures and generators | 99,064 B | 98,464 B | −0.61% |
| Independent nested class cells | 58,128 B | 55,392 B | −4.71% |
| Explicit local and global class bindings | 50,000 B | 48,640 B | −2.72% |
| Lexical class and receiver for `super` | 86,640 B | 83,992 B | −3.06% |
| Generated validator | 3,765,669 B | 3,765,669 B | 0.00% |
| Configuration manifest | 328,216 B | 328,216 B | 0.00% |

Each row is the median of five current-thread allocation observations during
public compilation and compiled-owner disposal on local Windows .NET 11 RC3
on November 19, 2026 UTC. The borrowed allowance returns to baseline after
every invocation. The class cases come from the public regression suite.
The validator and manifest are unchanged catalog controls. This allocation
diagnostic provides no elapsed or process CPU measurement.

Windows .NET 11 and Ubuntu .NET 10 each passed 122 public and 54 white-box
checks. They cover transitive and shadowed class cells, lexical `super`,
prepared class methods, cancellation and admission. Retain the change for
eliminating repeated binding collection on the affected paths.

[Samples and identities](summary.json) · [Source and checks](checks-and-procedures.tar.xz).
