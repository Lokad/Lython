import json
records=[]
for n in [4093,4095,4096,4097]:
 for a in [False,True]:
  p='a'*n
  text=p+'😀\n"\\é\x7f'+p
  expected='"'+p+('\\ud83d\\ude00\\n\\"\\\\\\u00e9\\u007f' if a else '😀\\n\\"\\\\é\x7f')+p+'"'
  assert json.dumps(text,ensure_ascii=a)==expected
  records.append(dict(prefix=n,ensureAscii=a,passed=True))
print(json.dumps(records))
