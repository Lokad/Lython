import json,pathlib,statistics
root=pathlib.Path(__file__).parent/'retrieved'
all_rows=[]
for path in sorted(root.glob('*-*/comparison.json')):
    if not path.stat().st_size: continue
    data=json.loads(path.read_text(encoding='utf-8-sig'))
    for case in data['observations']:
        samples=case['samples']
        values={image:{metric:statistics.median(s[metric]/s['jobs'] for s in samples if s['image']==image)
            for metric in ['elapsedMilliseconds','cpuMilliseconds','allocatedBytes']}
            for image in ['before','candidate','before-control']}
        baseline=values['before']
        row={'block':path.parent.name,'case':case['caseId'],'values':values,
            'candidatePercent':{m:100*(values['candidate'][m]/baseline[m]-1) for m in baseline},
            'controlPercent':{m:100*(values['before-control'][m]/baseline[m]-1) for m in baseline}}
        all_rows.append(row)
        print(row['block'],case['caseId'],
            'candidate elapsed/CPU/alloc',*(round(v,2) for v in row['candidatePercent'].values()),
            'control',*(round(v,2) for v in row['controlPercent'].values()))
(root.parent/'assessment.json').write_text(json.dumps(all_rows,indent=2),encoding='utf-8')
