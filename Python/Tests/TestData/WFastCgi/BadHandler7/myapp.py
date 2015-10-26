import sys
sys.stderr.write('something to std err')
print('something to std out')
raise Exception('handler file is raising')
