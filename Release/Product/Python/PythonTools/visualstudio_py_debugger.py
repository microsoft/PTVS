import sys
try:
    import thread
except ImportError:
    import _thread as thread
import socket
import struct
import weakref
import traceback
import types
from os import path

try:
    xrange
except:
    xrange = range

if sys.platform == 'cli':
    import clr

# save start_new_thread so we can call it later, we'll intercept others calls to it.

DETACHED = True
def thread_creator(func, args, kwargs = {}):
    id = _start_new_thread(new_thread_wrapper, (func, ) + args, kwargs)
        
    return id

_start_new_thread = thread.start_new_thread
exit_lock = thread.allocate_lock()
exit_lock.acquire()
THREADS = {}
THREADS_LOCK = thread.allocate_lock()
MODULES = []

# Py3k compat - alias unicode to str
try:
    unicode
except:
    unicode = str

# dictionary of line no to break point info
BREAKPOINTS = {}

BREAK_WHEN_CHANGED_DUMMY = object()
# lock for calling .send on the socket
send_lock = thread.allocate_lock()

class LockWrapper(object):
    def __init__(self, lock):
        self.lock = lock
    def acquire(self):
        self.lock.acquire()
    def release(self):
        self.lock.release()

send_lock = LockWrapper(send_lock)

SEND_BREAK_COMPLETE = False

STEPPING_OUT = -1  # first value, we decrement below this
STEPPING_NONE = 0
STEPPING_BREAK = 1
STEPPING_LAUNCH_BREAK = 2
STEPPING_ATTACH_BREAK = 3
STEPPING_INTO = 4
STEPPING_OVER = 5     # last value, we increment past this.

def cmd(cmd_str):
    if sys.version >= '3.0':
        return bytes(cmd_str, 'ascii')
    return cmd_str

if sys.version[0] == '3':
  # work around a crashing bug on CPython 3.x where they take a hard stack overflow
  # we'll never see this exception but it'll allow us to keep our try/except handler
  # the same across all versions of Python
  class StackOverflowException(Exception): pass
else:
  StackOverflowException = RuntimeError
  
# we can't run the importer at some random point because we might be importing 
# something complete with the loader lock held.  Therefore we eagerly run a UTF8
# decode here so that any required imports for it to succeed later have already
# been imported.

cmd('').decode('utf8')
''.encode('utf8') # just in case they differ in what they import...

ASBR = cmd('ASBR')
SETL = cmd('SETL')
THRF = cmd('THRF')
DETC = cmd('DETC')
NEWT = cmd('NEWT')
EXTT = cmd('EXTT')
EXIT = cmd('EXIT')
EXCP = cmd('EXCP')
MODL = cmd('MODL')
STPD = cmd('STPD')
BRKS = cmd('BRKS')
BRKF = cmd('BRKF')
BRKH = cmd('BRKH')
LOAD = cmd('LOAD')
EXCE = cmd('EXCE')
EXCR = cmd('EXCR')
CHLD = cmd('CHLD')
OUTP = cmd('OUTP')
REQH = cmd('REQH')
UNICODE_PREFIX = cmd('U')
ASCII_PREFIX = cmd('A')
NONE_PREFIX = cmd('N')

def get_thread_from_id(id):
    THREADS_LOCK.acquire()
    try:
        return THREADS.get(id)
    finally:
        THREADS_LOCK.release()

def should_send_frame(frame):
    return  frame is not None and frame.f_code is not get_code(debug) and frame.f_code is not get_code(new_thread_wrapper)

def lookup_local(frame, name):
    bits = name.split('.')
    obj = frame.f_locals.get(bits[0]) or frame.f_globals.get(bits[0]) or frame.f_builtins.get(bits[0])
    bits.pop(0)
    while bits and obj is not None and type(obj) is types.ModuleType:
        obj = getattr(obj, bits.pop(0), None)
    return obj
        
BREAK_MODE_ALWAYS = 1
BREAK_MODE_UNHANDLED = 32
class ExceptionBreakInfo(object):
    def __init__(self):
        self.default_mode = BREAK_MODE_UNHANDLED
        self.break_on = { }
        self.handler_cache = { }
        self.handler_lock = thread.allocate_lock()
    
    def Clear(self):
        self.default_mode = BREAK_MODE_UNHANDLED
        self.break_on.clear()
        self.handler_cache.clear()

    def ShouldBreak(self, thread, ex_type, ex_value, trace):
        probe_stack()
        name = ex_type.__module__ + '.' + ex_type.__name__
        mode = self.break_on.get(name, self.default_mode)
        return (bool(mode & BREAK_MODE_ALWAYS) or
                (bool(mode & BREAK_MODE_UNHANDLED) and not self.IsHandled(thread, ex_type, ex_value, trace)))
    
    def IsHandled(self, thread, ex_type, ex_value, trace):
        if trace is None:
            # get out if we didn't get a traceback
            return False

        if trace.tb_next is not None:
            # don't break if this isn't the top of the traceback
            return True
            
        cur_frame = trace.tb_frame
        
        while should_send_frame(cur_frame) and cur_frame.f_code.co_filename is not None:
            handlers = self.handler_cache.get(cur_frame.f_code.co_filename)
            
            if handlers is None:
                # req handlers for this file from the debug engine
                self.handler_lock.acquire()
                
                send_lock.acquire()
                conn.send(REQH)
                write_string(cur_frame.f_code.co_filename)
                send_lock.release()

                # wait for the handler data to be received
                self.handler_lock.acquire()
                self.handler_lock.release()

                handlers = self.handler_cache.get(cur_frame.f_code.co_filename)

            if handlers is None:
                # no code available, so assume unhandled
                return False

            line = cur_frame.f_lineno
            for line_start, line_end, expressions in handlers:
                if line_start <= line < line_end:
                    if '*' in expressions:
                        return True

                    for text in expressions:
                        try:
                            res = lookup_local(cur_frame, text)
                            if res is not None and issubclass(ex_type, res):
                                return True
                        except:
                            print("Error resolving: " + str(text))
                            traceback.print_exc()

            cur_frame = cur_frame.f_back

        return False
    
    def AddException(self, name, mode=BREAK_MODE_UNHANDLED):
        if sys.version_info[0] >= 3 and name.startswith('exceptions.'):
            name = 'builtins' + name[10:]
        
        self.break_on[name] = mode

BREAK_ON = ExceptionBreakInfo()

def probe_stack(depth = 10):
  """helper to make sure we have enough stack space to proceed w/o corrupting 
     debugger state."""
  if depth == 0:
      return
  probe_stack(depth - 1)


# list of files that we shouldn't be debugging
DONT_DEBUG = [__file__]


def should_debug_code(code):
    return code.co_filename not in DONT_DEBUG


class Thread(object):
    def __init__(self, id = None):
        if id is not None:
            self.id = id 
        else:
            self.id = thread.get_ident()
        self._events = {'call' : self.handle_call, 
                        'line' : self.handle_line, 
                        'return' : self.handle_return, 
                        'exception' : self.handle_exception,
                        'c_call' : self.handle_c_call,
                        'c_return' : self.handle_c_return,
                        'c_exception' : self.handle_c_exception,
                       }
        self.cur_frame = None
        self.stepping = STEPPING_NONE
        self.unblock_work = None
        self._block_lock = thread.allocate_lock()
        self._block_lock.acquire()
        self._block_starting_lock = thread.allocate_lock()
        self._is_blocked = False
        self._is_working = False
        self.stopped_on_line = None
        self.detach = False
        self.trace_func = self.trace_func # replace self.trace_func w/ a bound method so we don't need to re-create these regularly
        self.prev_trace_func = None
        self.trace_func_stack = []
    
    def trace_func(self, frame, event, arg):
        
        try:
            if self.stepping == STEPPING_BREAK and should_debug_code(frame.f_code):
                if self.cur_frame is None:
                    # happens during attach, we need frame for blocking
                    self.cur_frame = frame

                if self.detach:
                    sys.settrace(None)
                    return None

                self.async_break()

            return self._events[event](frame, arg)
        except (StackOverflowException, KeyboardInterrupt):
            # stack overflow, disable tracing
            return self.trace_func
    
    def handle_call(self, frame, arg):
        self.cur_frame = frame

        if frame.f_code.co_name == '<module>' and frame.f_code.co_filename != '<string>':
            probe_stack()
            code, module = new_module(frame)
            if not DETACHED:
                report_module_load(module)

                # see if this module causes new break points to be bound
                bound = set()
                global PENDING_BREAKPOINTS
                for pending_bp in PENDING_BREAKPOINTS:
                    if check_break_point(code.co_filename, module, pending_bp.brkpt_id, pending_bp.lineNo, pending_bp.filename, pending_bp.condition, pending_bp.break_when_changed):
                        bound.add(pending_bp)
                PENDING_BREAKPOINTS -= bound

        stepping = self.stepping
        if stepping is not STEPPING_NONE:
            if stepping == STEPPING_INTO:
                # block when we hit the 1st line, not when we're on the function def
                self.stepping = STEPPING_OVER
            elif stepping >= STEPPING_OVER:
                self.stepping += 1
            elif stepping <= STEPPING_OUT:
                self.stepping -= 1

            if stepping is STEPPING_LAUNCH_BREAK and sys.platform == 'cli' and not DETACHED:
                # work around IronPython bug - http://ironpython.codeplex.com/workitem/30127
                self.handle_line(frame, arg)

        # forward call to previous trace function, if any, saving old trace func for when we return
        old_trace_func = self.prev_trace_func
        if old_trace_func is not None:
            self.trace_func_stack.append(old_trace_func)
            self.prev_trace_func = None  # clear first incase old_trace_func stack overflows
            self.prev_trace_func = old_trace_func(frame, 'call', arg)

        return self.trace_func
        
    def not_our_code(self, code_obj):
        if sys.version >= '3':
            return code_obj == execfile.__code__ or code_obj.co_filename.startswith(sys.prefix)
        else:
            return code_obj.co_filename.startswith(sys.prefix)

    def handle_line(self, frame, arg):
        if not DETACHED:
            stepping = self.stepping
            if stepping is not STEPPING_NONE:   # check for the common case of no stepping first...
                if (((stepping == STEPPING_OVER or stepping == STEPPING_INTO) and frame.f_lineno != self.stopped_on_line) 
                    or stepping == STEPPING_LAUNCH_BREAK 
                    or stepping == STEPPING_ATTACH_BREAK):
                    if ((stepping == STEPPING_LAUNCH_BREAK and not MODULES) or
                        (self.not_our_code(frame.f_code)) or
                        not should_debug_code(frame.f_code)):  # don't break into our own debugger
                        # don't break into inital Python code needed to set things up
                        return self.trace_func
            
                    probe_stack()
                    self.stepping = STEPPING_NONE
                    def block_cond():
                        if stepping == STEPPING_OVER or stepping == STEPPING_INTO:
                            return report_step_finished(self.id)
                        else:
                            return report_process_loaded(self.id)
                    self.block(block_cond)

            if BREAKPOINTS:
                bp = BREAKPOINTS.get(frame.f_lineno)
                if bp is not None:
                    for (filename, bp_id), condition in bp.items():
                        if filename == frame.f_code.co_filename:                        
                            if condition:                            
                                try:
                                    res = eval(condition.condition, frame.f_globals, frame.f_locals)
                                    if condition.break_when_changed:
                                        block = condition.last_value != res
                                        condition.last_value = res
                                    else:
                                        block = res
                                except:
                                    block = True
                            else:
                                block = True

                            if block:
                                probe_stack()
                                self.block(lambda: report_breakpoint_hit(bp_id, self.id))
                            break

        # forward call to previous trace function, if any, updating trace function appropriately
        old_trace_func = self.prev_trace_func
        if old_trace_func is not None:
            self.prev_trace_func = None  # clear first incase old_trace_func stack overflows
            self.prev_trace_func = old_trace_func(frame, 'line', arg)

        return self.trace_func
    
    def handle_return(self, frame, arg):
        if not DETACHED:
            stepping = self.stepping
            if stepping is not STEPPING_NONE:
                if stepping == STEPPING_OUT:
                    # break at the next line
                    self.stepping = STEPPING_OVER
                elif stepping == STEPPING_OVER:
                    if frame.f_code.co_name == "<module>" and should_debug_code(frame.f_code):
                        self.stepping = STEPPING_NONE
                        self.block(lambda: report_step_finished(self.id))
                elif stepping > STEPPING_OVER:
                    self.stepping -= 1
                elif stepping < STEPPING_OUT:
                    self.stepping += 1

        # forward call to previous trace function, if any
        old_trace_func = self.prev_trace_func
        if old_trace_func is not None:
            old_trace_func(frame, 'return', arg)

        # restore previous frames trace function if there is one
        if self.trace_func_stack:
            self.prev_trace_func = self.trace_func_stack.pop()

        self.cur_frame = frame.f_back
        
    def handle_exception(self, frame, arg):
        if not DETACHED and should_debug_code(frame.f_code) and BREAK_ON.ShouldBreak(self, *arg):
            self.block(lambda: report_exception(frame, arg, self.id))

        # forward call to previous trace function, if any, updating the current trace function
        # with a new one if available
        old_trace_func = self.prev_trace_func
        if old_trace_func is not None:
            self.prev_trace_func = old_trace_func(frame, 'exception', arg)

        return self.trace_func
        
    def handle_c_call(self, frame, arg):
        # break points?
        pass
        
    def handle_c_return(self, frame, arg):
        # step out of ?
        pass
        
    def handle_c_exception(self, frame, arg):
        pass

    def async_break(self):
        def async_break_send():
            send_lock.acquire()
            global SEND_BREAK_COMPLETE
            if SEND_BREAK_COMPLETE:
                # multiple threads could be sending this...
                SEND_BREAK_COMPLETE = False
                conn.send(ASBR)
                conn.send(struct.pack('i', self.id))
            send_lock.release()
        self.stepping = STEPPING_NONE
        self.block(async_break_send)

    def block(self, block_lambda):
        """blocks the current thread until the debugger resumes it"""
        assert not self._is_blocked
        assert self.id == thread.get_ident(), 'wrong thread identity' + str(self.id) + ' ' + str(thread.get_ident())    # we should only ever block ourselves
        
        # send thread frames before we block
        self.enum_thread_frames_locally()
        
        self.stopped_on_line = self.cur_frame.f_lineno
        # need to synchronize w/ sending the reason we're blocking
        self._block_starting_lock.acquire()
        self._is_blocked = True
        block_lambda()
        self._block_starting_lock.release()

        while 1:
            self._block_lock.acquire()
            if self.unblock_work is None:
                break

            # the debugger wants us to do something, do it, and then block again
            self._is_working = True
            self.unblock_work()
            self.unblock_work = None
            self._is_working = False
        
        self._block_starting_lock.acquire()
        assert self._is_blocked
        self._is_blocked = False
        self._block_starting_lock.release()

    def unblock(self):
        """unblocks the current thread allowing it to continue to run"""
        assert self._is_blocked 
        assert self.id != thread.get_ident()    # only someone else should unblock us
        
        self._block_lock.release()

    def schedule_work(self, work):
        self._block_starting_lock.acquire()
        self.unblock_work = work
        self.unblock()
        self._block_starting_lock.release()

    def run_on_thread(self, text, cur_frame, execution_id):
        if not self._is_working:
            self.schedule_work(lambda : self.run_locally(text, cur_frame, execution_id))
        else:
            report_execution_error('<error: previous evaluation has not completed>', execution_id)

    def run_locally(self, text, cur_frame, execution_id):
        try:
            try:
                code = compile(text, cur_frame.f_code.co_name, 'eval')
            except:
                code = compile(text, cur_frame.f_code.co_name, 'exec')

            res = eval(code, cur_frame.f_globals, cur_frame.f_locals)
            report_execution_result(execution_id, res)
        except:
            report_execution_exception(execution_id, sys.exc_info())

    def enum_child_on_thread(self, text, cur_frame, execution_id, child_is_enumerate):
        if not self._is_working:
            self.schedule_work(lambda : self.enum_child_locally(text, cur_frame, execution_id, child_is_enumerate))
        else:
            report_children(execution_id, [], False, False)

    def enum_child_locally(self, text, cur_frame, execution_id, child_is_enumerate):
        try:
            if child_is_enumerate:
                # remove index from eval, then get the index back.
                index_size = 0
                enumerate_index = 0
                for c in reversed(text):
                    index_size += 1
                    if c.isdigit():
                        enumerate_index = enumerate_index * 10 + (ord(c) - ord('0'))
                    elif c == '[':
                        text = text[:-index_size]
                        break
            
            code = compile(text, cur_frame.f_code.co_name, 'eval')
            res = eval(code, cur_frame.f_globals, cur_frame.f_locals)
            
            if child_is_enumerate:
                for index, value in enumerate(res):
                    if enumerate_index == index:
                        res = value
                        break
                else:
                    # value changed?
                    report_children(execution_id, [], False, False)
                    return
            
            is_index = False
            is_enumerate = False
            maybe_enumerate = False
            try:
                if isinstance(res, types.GeneratorType):
                    # go to the except block
                    raise Exception('generator')
                elif hasattr(res, 'items'):
                    # dictionary-like object
                    enum = res.items()
                else:
                    # indexable object
                    enum = enumerate(res)
                    maybe_enumerate = True

                items = []
                for index, item in enum:
                    try:
                        if len(items) > 10000:
                            # report at most 10000 items.
                            items.append( ('[...]', 'Evaluation halted because sequence included too many items...') )
                            break
                        
                        items.append( ('[' + repr(index) + ']', item) )
                        if maybe_enumerate and not is_enumerate:
                            # check if we can index back into this object, or if we have to use
                            # enumerate to get values out of it.
                            try:
                                fetched = res[index]
                                if fetched is not item:
                                    is_enumerate = True
                            except:
                                is_enumerate = True
                                
                    except:
                        # ignore bad objects for now...
                        pass

                is_index = True
            except:
                # non-indexable object, return attribute names, filter callables
                items = []
                for name in dir(res):
                    if not (name.startswith('__') and name.endswith('__')):
                        try:
                            item = getattr(res, name)
                            if not hasattr(item, '__call__'):
                                items.append( (name, item) )
                        except:
                            # skip this item if we can't display it...
                            pass
            report_children(execution_id, items, is_index, is_enumerate)
        except:
            report_children(execution_id, [], False, False)

    def enum_thread_frames_locally(self):
        # first gather all the frames (which can run user code)
        frames = []
        cur_frame = self.cur_frame
    
        while should_send_frame(cur_frame):
            # calculate the ending line number
            lineno = cur_frame.f_code.co_firstlineno
            try:
                linetable = cur_frame.f_code.co_lnotab
            except:
                try:
                    lineno = cur_frame.f_code.Span.End.Line
                except:
                    lineno = -1
            else:
                for line_incr in linetable[1::2]:
                    if sys.version >= '3':
                        lineno += line_incr
                    else:
                        lineno += ord(line_incr)
                
            if cur_frame.f_locals is cur_frame.f_globals:
                var_names = cur_frame.f_globals
            else:
                var_names = cur_frame.f_code.co_varnames
                        
            vars = []
            for var_name in var_names:
                try:
                    obj = cur_frame.f_locals[var_name]
                except:
                    obj = '<undefined>'
                try:
                    if sys.version[0] == '2' and type(obj) is types.InstanceType:
                        type_name = "instance (" + obj.__class__.__name__ + ")"
                    else:
                        type_name = type(obj).__name__
                except:
                    type_name = 'unknown'
                    
                vars.append((var_name, type(obj), safe_repr(obj), safe_hex_repr(obj), type_name))
                

            frames.append((cur_frame.f_code.co_firstlineno,
                           lineno, 
                           cur_frame.f_lineno, 
                           cur_frame.f_code.co_name,
                           get_code_filename(cur_frame.f_code),
                           cur_frame.f_code.co_argcount,
                           vars))

            cur_frame = cur_frame.f_back



        # then send them
        send_lock.acquire()
        conn.send(THRF)
        conn.send(struct.pack('i',self.id))
    
        # send the frame count
        conn.send(struct.pack('i', len(frames)))
        for firstlineno, lineno, curlineno, name, filename, argcount, variables in frames:
            # send each frame    
            conn.send(struct.pack('i', firstlineno))
            conn.send(struct.pack('i', lineno))
            conn.send(struct.pack('i', curlineno))
    
            write_string(name)
            write_string(filename)
            conn.send(struct.pack('i', argcount))
                
            conn.send(struct.pack('i', len(variables)))
            for name, type_obj, safe_repr_obj, hex_repr_obj, type_name in variables:
                write_string(name)
                    
                write_object(type_obj, safe_repr_obj, hex_repr_obj, type_name)
        
        send_lock.release()


class Module(object):
    """tracks information about a loaded module"""

    CurrentLoadIndex = 0

    
    def __init__(self, filename):
        # TODO: Module.CurrentLoadIndex thread safety
        self.module_id = Module.CurrentLoadIndex
        Module.CurrentLoadIndex += 1
        self.filename = filename


class ConditionInfo(object):
    def __init__(self, condition, break_when_changed):
        self.condition = condition
        self.break_when_changed = break_when_changed
        self.last_value = BREAK_WHEN_CHANGED_DUMMY

def get_code(func):
    return getattr(func, 'func_code', None) or func.__code__


class DebuggerExitException(Exception): pass

def check_break_point(modFilename, module, brkpt_id, lineNo, filename, condition, break_when_changed):
    if module.filename.lower() == path.abspath(filename).lower():
        cur_bp = BREAKPOINTS.get(lineNo)
        if cur_bp is None:
            cur_bp = BREAKPOINTS[lineNo] = dict()
        
        cond_info = None
        if condition:
            cond_info = ConditionInfo(condition, break_when_changed)
        cur_bp[(modFilename, brkpt_id)] = cond_info
        report_breakpoint_bound(brkpt_id)
        return True
    return False


class PendingBreakPoint(object):
    def __init__(self, brkpt_id, lineNo, filename, condition, break_when_changed):
        self.brkpt_id = brkpt_id
        self.lineNo = lineNo
        self.filename = filename
        self.condition = condition
        self.break_when_changed = break_when_changed

PENDING_BREAKPOINTS = set()

class DebuggerLoop(object):    
    def __init__(self, conn):
        self.conn = conn
        self.command_table = {
            cmd('exit') : self.command_exit,
            cmd('stpi') : self.command_step_into,
            cmd('stpo') : self.command_step_out,
            cmd('stpv') : self.command_step_over,
            cmd('brkp') : self.command_set_breakpoint,
            cmd('brkc') : self.command_set_breakpoint_condition,
            cmd('brkr') : self.command_remove_breakpoint,
            cmd('brka') : self.command_break_all,
            cmd('resa') : self.command_resume_all,
            cmd('rest') : self.command_resume_thread,
            cmd('exec') : self.command_execute_code,
            cmd('chld') : self.command_enum_children,
            cmd('setl') : self.command_set_lineno,
            cmd('detc') : self.command_detach,
            cmd('clst') : self.command_clear_stepping,
            cmd('sexi') : self.command_set_exception_info,
            cmd('sehi') : self.command_set_exception_handler_info,
        }

    def loop(self):
        try:
            while True:
                inp = conn.recv(4)
                cmd = self.command_table.get(inp)
                if cmd is not None:
                    cmd()
                else:
                    if inp:
                        print ('unknown command', inp)
                    break
        except DebuggerExitException:
            pass
        except socket.error:
            pass
        except:
            traceback.print_exc()
            
    def command_exit(self):
        exit_lock.release()

    def command_step_into(self):
        tid = read_int(self.conn)
        thread = get_thread_from_id(tid)
        if thread is not None:
            thread.stepping = STEPPING_INTO
            thread.unblock()

    def command_step_out(self):
        tid = read_int(self.conn)
        thread = get_thread_from_id(tid)
        if thread is not None:
            thread.stepping = STEPPING_OUT
            thread.unblock()    
    
    def command_step_over(self):
        # set step over
        tid = read_int(self.conn)
        thread = get_thread_from_id(tid)
        if thread is not None:
            thread.stepping = STEPPING_OVER
            thread.unblock()

    def command_set_breakpoint(self):
        brkpt_id = read_int(self.conn)
        lineNo = read_int(self.conn)
        filename = read_string(self.conn)
        condition = read_string(self.conn)
        break_when_changed = read_int(self.conn)
                                
        for modFilename, module in MODULES:
            if check_break_point(modFilename, module, brkpt_id, lineNo, filename, condition, break_when_changed):
                break
        else:
            # failed to set break point
            PENDING_BREAKPOINTS.add(PendingBreakPoint(brkpt_id, lineNo, filename, condition, break_when_changed))
            report_breakpoint_failed(brkpt_id)

    def command_set_breakpoint_condition(self):
        brkpt_id = read_int(self.conn)
        condition = read_string(self.conn)
        break_when_changed = read_int(self.conn)
        
        for line, bp_dict in BREAKPOINTS.items():
            for filename, id in bp_dict:
                if id == brkpt_id:
                    bp_dict[filename, id] = ConditionInfo(condition, break_when_changed)
                    break

    def command_remove_breakpoint(self):
        lineNo = read_int(self.conn)
        brkpt_id = read_int(self.conn)
        cur_bp = BREAKPOINTS.get(lineNo)
        if cur_bp is not None:
            for file, id in cur_bp:
                if id == brkpt_id:
                    del cur_bp[(file, id)]
                    break

    def command_break_all(self):
        global SEND_BREAK_COMPLETE
        SEND_BREAK_COMPLETE = True
        THREADS_LOCK.acquire()
        for thread in THREADS.values():
            thread.stepping = STEPPING_BREAK
        THREADS_LOCK.release()

    def command_resume_all(self):
        # resume all
        THREADS_LOCK.acquire()
        for thread in THREADS.values():
            thread._block_starting_lock.acquire()
            if thread._is_blocked:
                thread.unblock()
            thread._block_starting_lock.release()
        THREADS_LOCK.release()
    
    def command_resume_thread(self):
        tid = read_int(self.conn)

        THREADS_LOCK.acquire()
        thread = THREADS[tid]
        thread.unblock()
        THREADS_LOCK.release()
    
    def command_set_exception_info(self):
        BREAK_ON.Clear()
        BREAK_ON.default_mode = read_int(self.conn)

        break_on_count = read_int(self.conn)
        for i in xrange(break_on_count):
            mode = read_int(self.conn)
            name = read_string(self.conn)
            BREAK_ON.AddException(name, mode)

    def command_set_exception_handler_info(self):
        try:
            filename = read_string(self.conn)

            statement_count = read_int(self.conn)
            handlers = []
            for _ in xrange(statement_count):
                line_start, line_end = read_int(self.conn), read_int(self.conn)

                expressions = set()
                text = read_string(self.conn).strip()
                while text != '-':
                    expressions.add(text)
                    text = read_string(self.conn)

                if not expressions:
                    expressions = set('*')

                handlers.append((line_start, line_end, expressions))

            BREAK_ON.handler_cache[filename] = handlers
        finally:
            BREAK_ON.handler_lock.release()

    def command_clear_stepping(self):
        tid = read_int(self.conn)

        thread = get_thread_from_id(tid)
        if thread is not None:
            thread.stepping = STEPPING_NONE

    def command_set_lineno(self):
        tid = read_int(self.conn)
        fid = read_int(self.conn)
        lineno = read_int(self.conn)
        try:
            THREADS_LOCK.acquire()
            THREADS[tid].cur_frame.f_lineno = lineno
            newline = THREADS[tid].cur_frame.f_lineno
            THREADS_LOCK.release()
            send_lock.acquire()
            self.conn.send(SETL)
            self.conn.send(struct.pack('i', 1))
            self.conn.send(struct.pack('i', tid))
            self.conn.send(struct.pack('i', newline))
            send_lock.release()
        except:
            send_lock.acquire()
            self.conn.send(SETL)
            self.conn.send(struct.pack('i', 0))
            self.conn.send(struct.pack('i', tid))
            self.conn.send(struct.pack('i', 0))
            send_lock.release()

    def command_execute_code(self):
        # execute given text in specified frame
        text = read_string(self.conn)
        tid = read_int(self.conn) # thread id
        fid = read_int(self.conn) # frame id
        eid = read_int(self.conn) # execution id
                
        thread = get_thread_from_id(tid)
        if thread is not None:
            cur_frame = thread.cur_frame
            for i in xrange(fid):
                cur_frame = cur_frame.f_back

            thread.run_on_thread(text, cur_frame, eid)
    
    def command_enum_children(self):
        # execute given text in specified frame
        text = read_string(self.conn)
        tid = read_int(self.conn) # thread id
        fid = read_int(self.conn) # frame id
        eid = read_int(self.conn) # execution id
        child_is_enumerate = read_int(self.conn)
                
        thread = get_thread_from_id(tid)
        if thread is not None:
            cur_frame = thread.cur_frame
            for i in xrange(fid):
                cur_frame = cur_frame.f_back

            thread.enum_child_on_thread(text, cur_frame, eid, child_is_enumerate)
    
    def command_detach(self):
        global conn

        # tell all threads to stop tracing...
        THREADS_LOCK.acquire()
        for tid, pyThread in THREADS.items():
            if not _INTERCEPTING_FOR_ATTACH:
                pyThread.detach = True
                pyThread.stepping = STEPPING_BREAK

            if pyThread._is_blocked:
                pyThread.unblock()

        if not _INTERCEPTING_FOR_ATTACH:
            THREADS.clear()
        
        BREAKPOINTS.clear()

        THREADS_LOCK.release()

        global DETACHED
        send_lock.acquire()
        conn.send(DETC)
        DETACHED = True
        if not _INTERCEPTING_FOR_ATTACH:
            sys.stdout = sys.__stdout__
            sys.stderr = sys.__stderr__

        send_lock.release()

        if not _INTERCEPTING_FOR_ATTACH:
            thread.start_new_thread = _start_new_thread
            thread.start_new = _start_new_thread

        for callback in DETACH_CALLBACKS:
            callback()

        raise DebuggerExitException()


DETACH_CALLBACKS = []

def new_thread_wrapper(func, *posargs, **kwargs):
    cur_thread = new_thread()
    try:
        sys.settrace(cur_thread.trace_func)
        func(*posargs, **kwargs)
    finally:
        THREADS_LOCK.acquire()
        if not cur_thread.detach:
            del THREADS[cur_thread.id]
            report_thread_exit(cur_thread)
        THREADS_LOCK.release()

def write_string(string):
    if string is None:
        conn.send(NONE_PREFIX)
    elif isinstance(string, unicode):
        bytes = string.encode('utf8')
        conn.send(UNICODE_PREFIX)
        conn.send(struct.pack('i', len(bytes)))
        conn.send(bytes)
    else:
        conn.send(ASCII_PREFIX)
        conn.send(struct.pack('i', len(string)))
        conn.send(string)

def read_string(conn):
    str_len = read_int(conn)
    return conn.recv(str_len).decode('utf8')

def read_int(conn):
    return struct.unpack('i', conn.recv(4))[0]

def report_new_thread(new_thread):
    ident = new_thread.id
    send_lock.acquire()
    conn.send(NEWT)
    conn.send(struct.pack('i', ident))
    send_lock.release()

def report_thread_exit(old_thread):
    ident = old_thread.id
    send_lock.acquire()
    conn.send(EXTT)
    conn.send(struct.pack('i', ident))
    send_lock.release()

def report_process_exit(exit_code):
    send_lock.acquire()
    conn.send(EXIT)
    conn.send(struct.pack('i', exit_code))
    send_lock.release()

    # wait for exit event to be received
    exit_lock.acquire()


def report_exception(frame, exc_info, tid):
    exc_type = exc_info[0]
    exc_value = exc_info[1]
    tb_value = exc_info[2]
    exc_name = exc_type.__module__ + '.' + exc_type.__name__

    if sys.version >= '3':
        excp_text = ''.join(traceback.format_exception(exc_type, exc_value, tb_value, chain = False))
    else:
        excp_text = ''.join(traceback.format_exception(exc_type, exc_value, tb_value))

    send_lock.acquire()
    conn.send(EXCP)
    write_string(exc_name)
    conn.send(struct.pack('i', tid))
    write_string(excp_text)
    send_lock.release()

def new_module(frame):
    mod = Module(get_code_filename(frame.f_code))
    MODULES.append((frame.f_code.co_filename, mod))

    return frame.f_code, mod

def report_module_load(mod):
    send_lock.acquire()
    conn.send(MODL)
    conn.send(struct.pack('i', mod.module_id))
    write_string(mod.filename)
    send_lock.release()

def report_step_finished(tid):
    send_lock.acquire()
    conn.send(STPD)
    conn.send(struct.pack('i', tid))
    send_lock.release()

def report_breakpoint_bound(id):
    send_lock.acquire()
    conn.send(BRKS)
    conn.send(struct.pack('i', id))
    send_lock.release()

def report_breakpoint_failed(id):
    send_lock.acquire()
    conn.send(BRKF)
    conn.send(struct.pack('i', id))
    send_lock.release()

def report_breakpoint_hit(id, tid):    
    send_lock.acquire()
    conn.send(BRKH)
    conn.send(struct.pack('i', id))
    conn.send(struct.pack('i', tid))
    send_lock.release()

def report_process_loaded(tid):
    send_lock.acquire()
    conn.send(LOAD)
    conn.send(struct.pack('i', tid))
    send_lock.release()

def report_execution_error(exc_text, execution_id):
    send_lock.acquire()    
    conn.send(EXCE)
    conn.send(struct.pack('i', execution_id))
    write_string(exc_text)
    send_lock.release()

def report_execution_exception(execution_id, exc_info):
    try:
        exc_text = str(exc_info[1])
    except:
        exc_text = 'An exception was thrown'

    report_execution_error(exc_text, execution_id)

def safe_repr(obj):
    try:
        return repr(obj)
    except:
        return '__repr__ raised an exception'

def safe_hex_repr(obj):
    try:
        return hex(obj)
    except:
        return None

def report_execution_result(execution_id, result):
    obj_repr = safe_repr(result)
    hex_repr = safe_hex_repr(result)
    res_type = type(result)
    type_name = type(result).__name__
    

    send_lock.acquire()
    conn.send(EXCR)
    conn.send(struct.pack('i', execution_id))
    write_object(res_type, obj_repr, hex_repr, type_name)
    send_lock.release()

def report_children(execution_id, children, is_index, is_enumerate):
    children = [(index, safe_repr(result), safe_hex_repr(result), type(result), type(result).__name__) for index, result in children]

    send_lock.acquire()
    conn.send(CHLD)
    conn.send(struct.pack('i', execution_id))
    conn.send(struct.pack('i', len(children)))
    conn.send(struct.pack('i', is_index))
    conn.send(struct.pack('i', is_enumerate))
    for child_name, obj_repr, hex_repr, res_type, type_name in children:
        write_string(child_name)
        write_object(res_type, obj_repr, hex_repr, type_name)

    send_lock.release()

def get_code_filename(code):
    return path.abspath(code.co_filename)

NONEXPANDABLE_TYPES = [int, str, bool, float, object, type(None), unicode]
try:
    NONEXPANDABLE_TYPES.append(long)
except NameError: pass

def write_object(obj_type, obj_repr, hex_repr, type_name):
    write_string(obj_repr)
    write_string(hex_repr)
    write_string(type_name)
    if obj_type in NONEXPANDABLE_TYPES:
        conn.send(struct.pack('i', 0))
    else:
        conn.send(struct.pack('i', 1))


try:
    execfile
except NameError:
    # Py3k, execfile no longer exists
    def execfile(file, globals, locals): 
        f = open(file, "rb")
        try:
            exec(compile(f.read().replace(cmd('\r\n'), cmd('\n')), file, 'exec'), globals, locals) 
        finally:
            f.close()


debugger_thread_id = -1
_INTERCEPTING_FOR_ATTACH = False
def intercept_threads(for_attach = False):
    thread.start_new_thread = thread_creator
    thread.start_new = thread_creator
    global _INTERCEPTING_FOR_ATTACH
    _INTERCEPTING_FOR_ATTACH = for_attach

def attach_process(port_num, debug_id, report_and_block = False):
    global conn
    for i in xrange(50):
        try:
            conn = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            conn.connect(('127.0.0.1', port_num))
            write_string(debug_id)
            break
        except:
            import time
            time.sleep(50./1000)
    else:
        raise Exception('failed to attach')

    global DETACHED
    DETACHED = False

    # start the debugging loop
    global debugger_thread_id
    debugger_thread_id = _start_new_thread(DebuggerLoop(conn).loop, ())

    if report_and_block:
        THREADS_LOCK.acquire()
        main_thread = THREADS[thread.get_ident()]
        for cur_thread in THREADS.values():
            report_new_thread(cur_thread)

        THREADS_LOCK.release()

        for filename, module in MODULES:
            report_module_load(module)            

        main_thread.block(lambda: report_process_loaded(thread.get_ident()))

    # intercept all new thread requests
    if not _INTERCEPTING_FOR_ATTACH:
        intercept_threads()


def new_thread(tid = None, set_break = False, frame = None):
    # called during attach w/ a thread ID provided.
    if tid == debugger_thread_id:
        return None

    cur_thread = Thread(tid)    
    THREADS_LOCK.acquire()
    THREADS[cur_thread.id] = cur_thread
    THREADS_LOCK.release()
    cur_thread.cur_frame = frame
    if set_break:
        cur_thread.stepping = STEPPING_ATTACH_BREAK
    if not DETACHED:
        report_new_thread(cur_thread)
    return cur_thread

def do_wait():
    import msvcrt    
    sys.__stdout__.write('Press any key to continue . . . ')
    sys.__stdout__.flush()
    msvcrt.getch()

class _DebuggerOutput(object):
    """file like object which redirects output to the repl window."""
    def __init__(self, is_stdout):
        self.is_stdout = is_stdout

    def flush(self):
        pass
    
    def writelines(self, lines):
        for line in lines:
            self.write(line)
    
    @property
    def encoding(self):
        return 'utf8'

    def write(self, value):
        if not DETACHED:
            probe_stack(3)
            send_lock.acquire()
            conn.send(OUTP)
            conn.send(struct.pack('i', thread.get_ident()))
            write_string(value)
            send_lock.release()
        if self.is_stdout:
            sys.__stdout__.write(value)
        else:
            sys.__stderr__.write(value)
    
    def isatty(self):
        return True

    def next(self):
        pass
    
    @property
    def name(self):
        if self.is_stdout:
            return "<stdout>"
        else:
            return "<stderr>"


def is_same_py_file(file1, file2):
    """compares 2 filenames accounting for .pyc files"""
    if file1.endswith('.pyc'):
        if file2.endswith('.pyc'):
            return file1 == file2
        return file1[:-1] == file2
    elif file2.endswith('.pyc'):
        return file1 == file2[:-1]
    else:
        return file1 == file2


def print_exception():
    # count the debugger frames to be removed
    tb_value = sys.exc_info()[2]
    debugger_count = 0
    while tb_value is not None:
        if is_same_py_file(tb_value.tb_frame.f_code.co_filename, __file__):
            debugger_count += 1
        tb_value = tb_value.tb_next
        
    # print the traceback
    tb = traceback.extract_tb(sys.exc_info()[2])[debugger_count:]         
    if tb:
        print('Traceback (most recent call last):')
        for out in traceback.format_list(tb):
            sys.stdout.write(out)
    
    # print the exception
    for out in traceback.format_exception_only(sys.exc_info()[0], sys.exc_info()[1]):
        sys.stdout.write(out)
    

def debug(file, port_num, debug_id, globals_obj, locals_obj, wait_on_exception, redirect_output, wait_on_exit):
    # remove us from modules so there's no trace of us
    sys.modules['$visualstudio_py_debugger'] = sys.modules['visualstudio_py_debugger']
    __name__ = '$visualstudio_py_debugger'
    del sys.modules['visualstudio_py_debugger']
    del globals_obj['port_num']
    del globals_obj['visualstudio_py_debugger']
    del globals_obj['wait_on_exception']
    del globals_obj['redirect_output']
    del globals_obj['wait_on_exit']
    del globals_obj['debug_id']

    attach_process(port_num, debug_id)

    if redirect_output:
        sys.stdout = _DebuggerOutput(is_stdout = True)
        sys.stderr = _DebuggerOutput(is_stdout = False)

    # setup the current thread
    cur_thread = new_thread()
    cur_thread.stepping = STEPPING_LAUNCH_BREAK

    # start tracing on this thread
    sys.settrace(cur_thread.trace_func)

    # now execute main file
    try:
        try:
            execfile(file, globals_obj, locals_obj)
        finally:
            sys.settrace(None)
            THREADS_LOCK.acquire()
            del THREADS[cur_thread.id]
            THREADS_LOCK.release()
            report_thread_exit(cur_thread)

        if wait_on_exit:
            do_wait()
    except SystemExit:
        report_process_exit(sys.exc_info()[1].code)
        if wait_on_exception and sys.exc_info()[1].code != 0:
            print_exception()
            do_wait()
        raise
    except:
        print_exception()
        if wait_on_exception:
            do_wait()
        report_process_exit(1)
        raise
    
    report_process_exit(0)
