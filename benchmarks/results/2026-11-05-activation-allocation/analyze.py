from pathlib import Path
import collections,datetime,hashlib,json,tarfile

root=Path(__file__).resolve().parent
assert hashlib.sha256((root/'observations.tgz').read_bytes()).hexdigest()=='48f791a8754e31f60975cf89627efbbaf9aefc338662fd7c326c1c6f6f8316e6'
with tarfile.open(root/'observations.tgz') as archive:archive.extractall(root/'retrieved',filter='data')
rows=[]
for case in ('text','ordinary'):
    folder=root/'retrieved'/case
    record=json.loads((folder/'record.json').read_text(encoding='utf-8'))
    begin=datetime.datetime.fromisoformat(record['captureBeginUtc']);end=datetime.datetime.fromisoformat(record['captureEndUtc'])
    ticks=[json.loads(line) for line in (folder/'allocation-ticks.jsonl').read_text(encoding='utf-8').splitlines()]
    in_phase=[e for e in ticks if begin<=datetime.datetime.fromisoformat(e['utc'].replace('Z','+00:00'))<=end]
    guest=[e for e in in_phase if e['threadId']==record['guestThreadId']]
    types=collections.defaultdict(lambda:dict(ticks=0,sampledBytes=0,sizes=collections.Counter()))
    for e in guest:
        group=types[e['type']];group['ticks']+=1;group['sampledBytes']+=e['bytesSinceTick'];group['sizes'][e['objectBytes']]+=1
    total=sum(v['sampledBytes'] for v in types.values())
    ordered=[]
    for name,value in sorted(types.items(),key=lambda kv:kv[1]['sampledBytes'],reverse=True):
        ordered.append(dict(type=name,ticks=value['ticks'],sampledBytes=value['sampledBytes'],sampledPercent=100*value['sampledBytes']/total,sizes=value['sizes'].most_common()))
    result=dict(case=case,allEvents=len(ticks),eventsInPhase=len(in_phase),guestEvents=len(guest),guestThreadId=record['guestThreadId'],guestThreadName=record['threadsAfter'][str(record['guestThreadId'])]['name'],eventsLost=record['collector']['eventsLost'],captureInvocations=record['captureInvocations'],guestCpuSeconds=record['guestThreadCpuSeconds'],types=ordered)
    rows.append(result)
    print(case,'events',len(ticks),'inphase',len(in_phase),'guest',len(guest),'thread',result['guestThreadName'],'CPU',result['guestCpuSeconds'],'lost',result['eventsLost'])
    for value in ordered[:18]:print(round(value['sampledPercent'],2),value['ticks'],value['type'],value['sizes'][:4])
(root/'assessment.json').write_text(json.dumps(dict(purpose='Sampled allocation-tick attribution, not exact counts/live heap or CPU gains',rows=rows),indent=2)+'\n',encoding='utf-8')
