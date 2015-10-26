import _pickle

# raise an exception that's not a built-in
try:
	print('raising')
	raise _pickle.PickleError
except:
	print('caught')

try:
	print('raising')
	try:
		raise StopIteration
	finally:
		raise NameError
except:
	print('caught')

try:
	try:
		raise StopIteration
	except:
		raise NameError
except:
	pass

def g():
	def f():
		raise Exception

	try:
		f()
	except:
		print('line 33')
		print('line 34')
	print('after exception here')

g()

print('ex done')
print('no, really, its done')