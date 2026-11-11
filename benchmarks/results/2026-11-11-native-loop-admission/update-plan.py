from pathlib import Path
import shutil
r=Path(__file__).parent;p=Path('PLAN.md');shutil.copyfile(p,r/'PLAN-before-r247-active.md');t=p.read_text(encoding='utf-8')
idx=t.index('| R245 retention; delivery pending |')
t=t[:idx]+'| R247 active prototype, uncommitted | One-file native-loop admission scope consolidation. First admission remains after first binding/basic guards; per-word entry/exit and inner cleanup removed, cancellation/refusal checks remain. Empty/type/protocol/growth fallbacks unwind before ordinary code; private totals publish after scope leaves. One extra logical nesting level may span binding reentry, conservatively rejecting earlier. Focused actual11/10 and controlled source pair pending; no performance or qualification claim. |\n'+t[idx:]
t=t.replace('## P2 — native-loop admission source gate','## P2 — R247 native-loop admission prototype')
t=t.replace('Assess one managed admission scope across an already-native word segment,','Prototype one managed admission scope across an already-native word segment,')
t=t.replace('Source gate stops at a complete effect/lifetime proof or a concrete obstacle;','Source gate selected one cleanup flag and existing outer finally; no new guard\nframework or configuration. First entry remains at its original call boundary;\nempty/type/total-mismatch exits before entry. Native iteration cannot suspend;\ncallbacks/ordinary fallback leave the scope on exit. Validate the conservative\nnesting tradeoff rather than asserting identical boundary behavior.\n\nThe investigation stops at a usable bounded observation or a concrete obstacle;')
p.write_text(t,encoding='utf-8');print('R247 is clearly an uncommitted prototype; retained/current CPython rows and full delivery refer to8f862/458be840, not this source.')
