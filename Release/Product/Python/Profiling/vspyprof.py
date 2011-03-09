try:
	import thread
except:
	import _thread as thread

import sys
import ctypes

def hidden_frame(func, posargs, kwargs):
	"""this is just an extra method for new thread so"""
	"""we have the same # of extra frames (1) as the main thread"""
	func(*posargs, **kwargs)

# set up tracing so we pick up other threads...
def new_thread(func, *posargs, **kwargs):
    handle = start_profiling()
    try:
        hidden_frame(func, posargs, kwargs)
    finally:
        pyprofdll.CloseThread(handle)

def start_new_thread(func, args, kwargs = {}):
    return _start_new_thread(new_thread, (func, ) + args, kwargs)

_start_new_thread = thread.start_new_thread
thread.start_new_thread = start_new_thread

def start_profiling():
	# load as PyDll so we're called w/ the GIL held
	return pyprofdll.InitProfiler(profiler)

try:
	execfile
except NameError:
	# Py3k, execfile no longer exists
	def execfile(file, globals, locals): 
		f = open(file, "r")
		try:
			exec(compile(f.read(), file, 'exec'), globals, locals) 
		finally:
			f.close()

def profile(file, globals_obj, locals_obj, profdll):
	global profiler, pyprofdll

	pyprofdll = ctypes.PyDLL(profdll)
	profiler = pyprofdll.CreateProfiler(sys.dllhandle)

	handle = start_profiling()

	try:
		execfile(file, globals_obj, locals_obj)
	finally:
		pyprofdll.CloseThread(handle)
		pyprofdll.CloseProfiler(profiler)
