# B1528 allocation after the supplied integer changes

The exact retained `be20f34fc` runtime ran the two nested factory scripts and
the quote-heavy invoice on Windows x64 with .NET 11 RC3 on November 23,
2026 UTC. The 32-file frozen image matches the B1526 candidate. The workers
match CPython goldens before and after each six-second EventPipe capture.
All 313 observed batches verify and the workers and profilers join.

| Leading source | Postponed factories | Eager factories | Quote-heavy invoice |
| --- | ---: | ---: | ---: |
| Executable function | 55.48% | 55.43% | Below leading sources |
| Boxed BigInteger | 25.33% | 25.62% | 23.09% |
| Singleton positional-default tuple | 17.27% | 17.10% | Below leading sources |
| Lython string | Below leading sources | Below leading sources | 34.83% |
| Executable slot array | Below leading sources | Below leading sources | 9.12% |

Percentages are sampled allocation tick weights. They measure allocation
attribution. Each factory executes 2,048 iterations per invocation.
Nested factories construct observable fresh callable objects and one
dynamic singleton positional-default tuple. The current tuple holds the
canonical item and lazy integer identity. A plausible next design stores
that single default with the existing callable through normal binding,
while retaining capacity for materializing its canonical tuple on
inspection. Testing must cover observed tuple and item aliases, changes
to `__defaults__`, reentry, cleanup and refusal.

The invoice's largest sampled allocation sources differ from the factories.
Earlier invoice CPU attribution identifies dictionary and call handling
as well as parsing. These samples do not establish elapsed or CPU savings
for a rearranged default representation.
[Earlier allocation profile](../2026-11-23-retained-factory-allocation/README.md) ·
[Invoice CPU attribution](../2026-11-23-retained-csv-profile/README.md).

[Summary](summary.json) · [Observations and procedures](observations-and-procedures.tar.xz).
