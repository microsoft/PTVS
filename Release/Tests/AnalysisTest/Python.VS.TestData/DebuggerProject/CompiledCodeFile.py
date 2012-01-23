import os

def f():
    print(f.__code__.co_filename)
    print(os.path.abspath(f.__code__.co_filename))



def g():
	pass