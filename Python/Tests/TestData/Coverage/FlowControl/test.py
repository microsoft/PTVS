if True:
    print('covered')
else:
    print('uncovered')
    
if False:
    print('uncovered')
else:
    print('covered')
    
while False:
    print('uncovered')
    
for i in []:
    print('uncovered')
    
for i in [1,2,3]:
    print('covered')
    
try:
    raise Exception('covered')
except:
    print('covered')
    
    
try:
    raise Exception('covered')
except StopIteration:
    print('uncovered')
except:
    print('covered')
    
def f():
    return 42
    print('uncovered')
    
a = f()


for i in [1,2,3]:
    break
    print('uncovered')
    

for i in [1,2,3]:
    continue
    print('uncovered')
    
for i in [1,2,3]:
    if not i:
        break
    print('covered')
    

for i in [1,2,3]:
    if not i:
        continue
    print('covered')
    
