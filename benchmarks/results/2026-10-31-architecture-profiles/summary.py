import json
from pathlib import Path
root=Path('/home/lokad/lython-bench-checks/architecture-20261031-07c150a6')
for case in ['csv-warm','text-warm','json-warm','text-compile']:
 c=json.loads((root/'perf'/case/'cpu-leaves.json').read_text())
 a=json.loads((root/'eventpipe'/case/'summary.json').read_text())
 print(json.dumps(dict(case=case,samples=c['samples'],cpu=c['cpuSeconds'],unknown=c['unknown'],badMapLines=len(c['malformedMapLines']),leaves=c['topLeafCpuSamples'][:12],allocation=a['allocationTicksApproximateBytes'][:9])))
