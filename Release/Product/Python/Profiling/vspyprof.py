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

def profile(file, globals_obj, locals_obj, profdll):
    global profiler, pyprofdll

    pyprofdll = ctypes.PyDLL(profdll)
    pyprofdll.CreateProfiler.restype = ctypes.c_void_p
    pyprofdll.CloseThread.argtypes = [ctypes.c_void_p]
    pyprofdll.CloseProfiler.argtypes = [ctypes.c_void_p]
    pyprofdll.InitProfiler.argtypes = [ctypes.c_void_p]

    profiler = pyprofdll.CreateProfiler(sys.dllhandle)

    try:
        if sys.version_info[0] >= 3:
            # execfile's not available, and we want to start profiling
            # after we've compiled the users code.
            f = open(file, "r")
            code = compile(f.read(), file, 'exec')
            handle = start_profiling()
            exec(code, globals_obj, locals_obj)
        else:
            handle = start_profiling()
            execfile(file, globals_obj, locals_obj)
    finally:
        pyprofdll.CloseThread(handle)
        pyprofdll.CloseProfiler(profiler)
