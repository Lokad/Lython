import collections
import json
import pathlib

notes = pathlib.Path(__file__).parent / 'collection'
data = json.loads((notes / 'text.speedscope.json').read_text(encoding='utf-8-sig'))
names = [f['name'] for f in data['shared']['frames']]
profiles = []
monitors = collections.Counter()
all_weights = collections.Counter()
managed_exclusive = collections.Counter()
managed_inclusive = collections.Counter()
monitor_states = collections.Counter()
for profile in data['profiles']:
    assert profile['type'] == 'evented' and profile['unit'] == 'milliseconds'
    stack = []
    previous = profile['startValue']
    exclusive = collections.Counter()
    inclusive = collections.Counter()
    for event in profile['events']:
        now = event['at']
        assert now >= previous
        elapsed = now - previous
        if elapsed > 0 and stack:
            exclusive[stack[-1]] += elapsed
            all_weights[tuple(stack)] += elapsed
            for frame in set(stack):
                inclusive[frame] += elapsed
            real = [frame for frame in stack if names[frame] not in ('CPU_TIME', 'UNMANAGED_CODE_TIME')]
            if names[stack[-1]] == 'CPU_TIME':
                managed_exclusive[real[-1]] += elapsed
                for frame in set(real):
                    managed_inclusive[frame] += elapsed
            if 'Monitor.Enter_Slowpath' in names[real[-1]]:
                monitors[tuple(stack)] += elapsed
                monitor_states[names[stack[-1]]] += elapsed
        if event['type'] == 'O':
            stack.append(event['frame'])
        else:
            assert event['type'] == 'C' and stack.pop() == event['frame']
        previous = now
    assert not stack
    profiles.append({'thread': profile['name'], 'spanMs': profile['endValue'] - profile['startValue'],
        'weightedMs': sum(exclusive.values()),
        'exclusive': [{'frame': names[k], 'ms': v} for k, v in exclusive.most_common(18)],
        'lythonInclusive': [{'frame': names[k], 'ms': v} for k, v in inclusive.most_common()
            if 'Lokad.Lython!' in names[k]][:22]})
payload = {'meaning': 'Evented stack interval weights; wall-time across threads, not hardware CPU measurement. CPU_TIME/UNMANAGED_CODE_TIME are exporter state markers. Inclusive stacks overlap.',
    'profiles': profiles, 'monitorPaths': [{'ms': ms, 'stack': [names[i] for i in stack]}
        for stack, ms in monitors.most_common(12)],
    'managedStateExclusive': [{'frame': names[k], 'ms': v} for k, v in managed_exclusive.most_common(35)],
    'managedStateLythonInclusive': [{'frame': names[k], 'ms': v} for k, v in managed_inclusive.most_common()
        if 'Lokad.Lython!' in names[k]][:35],
    'monitorStates': dict(monitor_states)}
(notes / 'call-profile.json').write_text(json.dumps(payload, indent=2) + '\n', encoding='utf-8')
for profile in profiles:
    if profile['lythonInclusive']:
        print(profile['thread'], 'weighted ms', round(profile['weightedMs'], 2))
        print('EXCLUSIVE', [(round(r['ms'], 2), r['frame']) for r in profile['exclusive'][:7]])
        print('LYTHON INCLUSIVE', [(round(r['ms'], 2), r['frame']) for r in profile['lythonInclusive'][:8]])
print('MONITOR PATHS')
for stack, ms in monitors.most_common(3):
    print(round(ms, 2), '\n'.join(names[i] for i in stack[-12:]))
print('MANAGED STATE EXCLUSIVE', [(round(v, 2), names[k]) for k, v in managed_exclusive.most_common(18)])
print('MANAGED STATE LYTHON INCLUSIVE', [(round(v, 2), names[k]) for k, v in managed_inclusive.most_common()
    if 'Lokad.Lython!' in names[k]][:18])
