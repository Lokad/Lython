def make():
 value='outer'
 def peek():return value
 def collect():
  nonlocal value
  return [lambda:value for value in range(3)]
 fs=collect()
 return [f() for f in fs],value,peek()
print(make())
