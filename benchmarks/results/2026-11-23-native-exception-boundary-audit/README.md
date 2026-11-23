# Exception-boundary queries during native preparation

The retained `a2bde38b2` source and its matching `73e28cac2` core
compile an existing corpus of 20 scripts and three constructed protected
aggregation fixtures. The tape inspector checks the compiled owner refunds
and identifies the eligible sites on each final instruction tape.

An accepted dictionary update, dictionary loop or closure arithmetic
segment visits every exception region in its code object. Counting these
accepted sites bounds the visits from below. Counting every opcode triple
that could lead to one of the checks bounds visits from above, including
checks that might decline early. The following values are structural.

| Corpus | Instructions or input shape | Region visits per compilation |
| --- | --- | ---: |
| Protected dictionary updates | 8 independent guarded updates | 64–128 |
| Protected dictionary updates | 32 independent guarded updates | 1,024–2,048 |
| Protected dictionary updates | 128 independent guarded updates | 16,384–32,768 |
| Sparse generated validator | 2,937 instructions and 97 regions | 0 |
| Protected file search | 68 instructions and three regions | 0 |
| Existing JSON and text workloads | At most two regions in an affected code object | 0–4 |

The constructed series establishes quadratic work in a supported shape.
The existing corpus gives no reason to allocate and fill a second
boundary representation. Keep the current preparation and revisit if a
real generated workload contains many protected accumulation sites and
compilation attribution identifies the scan. No timing or runtime change
is implied by this inspection.

[Counts and bounds](summary.json) · [Inspector and sources](procedure.tar.xz).
