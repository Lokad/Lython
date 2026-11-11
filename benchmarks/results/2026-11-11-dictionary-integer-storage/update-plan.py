from pathlib import Path
p=Path('PLAN.md');t=p.read_text(encoding='utf-8')
start=t.index('## P1 — coherent native input proof / execution design')
end=t.index('## Remaining coherent roadmap')
t=t[:start]+'''## P1 — R245: native dictionary value storage

**Active uncommitted prototype, not a delivered gain.** Current captured profiles
associate157leaves with dictionary-loop work. Existing counting updates box an
exact integer after every word even when Python reads only the final counts.
Hypothesis: retain a signed machine value in the existing private dictionary entry,
and materialize/cache one immutable BigInteger at its first actual Python read.
Remove intermediate BigInteger arithmetic/boxing, without a new value API, side
table, scalar cache, owner, dispatch or compiler special case. Generic wider and
protocol operations retain their ordinary implementation before effects.

The entry grows32→40bytes; increase capacity and replacement admission explicitly.
Existing coarse numeric-slot headroom funds eventual immutable boxes. Copying and
shared-record publication materialize before exposing aliases/frozen key metadata.
Moving/growing private storage preserves the inline value; deletion shifts it with
its key. Shared CSV value arrays keep adopted object storage and their current
ownership. Conservative earlier admission is intentional, no tiny-budget parity.

Acceptance: repeated-read/copy identity, old aliases after updates, deletion/order,
growth/clear/pop/setdefault, shared-record isolation, exact signed boundary and wide
promotion, custom protocols, first refusal before growth/publication, retained
results, shared allowance reuse, actual pending cancellation and joined cleanup.
Focused actual11/10 checks precede one complete-script pair: captured/ordinary
text and CSV, search/report/JSON controls, both APIs and separate compilation.
Decide on allocation benefit, source simplification and remaining regression debt
separately from CPU. Preserve adverse observations and original conditions.

Completion: retain only if the coherent representation change justifies its
invariants and measured tradeoffs; otherwise remove the entry field/materialization
branches together and preserve useful semantic tests/evidence. Bounded refinement
is allowed for a demonstrated source/measurement issue; no fixed threshold veto.

The preceding proof source gate found that numeric shape relationships, conditional
unions and formatted dictionary reads require live checks. Replacing recursion
with a second proof representation has no demonstrated phase-removal benefit.
Keep complete pre-effect proof and its current evaluator; do not add that framework.
This is a scoped design assessment, not a permanent veto on compiler consolidation.
R244's two withdrawn forms are archived; the retained690source remains qualified.

''' +t[end:]
t=t.replace('before a representation\n  redesign.','before any additional representation\n  redesign. R245 assesses actual dictionary storage rather than operand banks.')
p.write_text(t,encoding='utf-8')
print('PLAN now prioritizes the bounded dictionary storage milestone with code removal, ownership tradeoffs, semantic acceptance and measurement boundary.')
