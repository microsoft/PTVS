try:
    import thread
except ImportError:
    # Renamed in Python3k
    import _thread as thread

import threading
import sys
import socket
import time
import struct
import imp
import traceback
import random
import os
import inspect
import types
from collections import deque

try:
    unicode
except NameError:
    unicode = str

try:
    BaseException
except NameError:
    # BaseException not defined until Python 2.5
    BaseException = Exception

DEBUG = os.environ.get('DEBUG_REPL') is not None

__all__ = ['ReplBackend', 'BasicReplBackend', 'BACKEND']

def _debug_write(out):
    if DEBUG:
        sys.__stdout__.write(out)
        sys.__stdout__.flush()


def _cmd(cmd_str):
    """creates a command string for sending out via sockets - this handles Python v2 vs v3"""
    if sys.version >= '3.0':
        return bytes(cmd_str, 'ascii')
    return cmd_str


class UnsupportedReplException(Exception):
    def __init__(self, reason):
        self.reason = reason

# save the start_new_thread so we won't debug/break into the REPL comm thread.
start_new_thread = thread.start_new_thread
class ReplBackend(object):
    """back end for executing REPL code.  This base class handles all of the 
communication with the remote process while derived classes implement the 
actual inspection and introspection."""
    _MRES = _cmd('MRES')
    _SRES = _cmd('SRES')
    _MODS = _cmd('MODS')
    _IMGD = _cmd('IMGD')
    _PRPC = _cmd('PRPC')
    _RDLN = _cmd('RDLN')
    _STDO = _cmd('STDO')
    _STDE = _cmd('STDE')
    _DBGA = _cmd('DBGA')
    _DETC = _cmd('DETC')
    _UNICODE_PREFIX = _cmd('U')
    _ASCII_PREFIX = _cmd('A')
    
    def __init__(self):
        self.conn = None
        self.send_lock = threading.Lock()
        self.input_event = threading.Lock()
        self.input_event.acquire()  # lock starts acquired (we use it like a manual reset event)        
        self.input_string = None
    
    def connect(self, port):
        self.conn = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.conn.connect(('127.0.0.1', port))

        # start a new thread for communicating w/ the remote process
        start_new_thread(self._repl_loop, ())

    def _repl_loop(self):
        """loop on created thread which processes communicates with the REPL window"""    
        try:
            while True: 
                # we receive a series of 4 byte commands.  Each command then
                # has it's own format which we must parse before continuing to
                # the next command.
                self.flush()                
                inp = self.conn.recv(4)
                if inp == '':
                    break
                self.flush()
            
                cmd = ReplBackend._COMMANDS.get(inp)
                if cmd is not None:
                    cmd(self)
        except:
            _debug_write('error in repl loop')
            _debug_write(traceback.format_exc())
            try:
                self.interrupt_main()
            finally:
                self.exit_process()

    def _send(self, *data):
        self.send_lock.acquire()
        try:
            for d in data:
                _debug_write(d + '\n')
                if sys.version >= '3.0':
                    self.conn.send(bytes(str(d), 'ascii'))
                else:
                    self.conn.send(str(d))
        finally:
            self.send_lock.release()        

    def _read_string(self):
        """ reads length of text to read, and then the text encoded in UTF-8, and returns the string"""
        len, = struct.unpack('i', self.conn.recv(4))
        return self.conn.recv(len).decode('utf8')
    
    def _cmd_run(self):
        """runs the received snippet of code"""
        self.run_command(self._read_string())        

    def _cmd_abrt(self):
        """aborts the current running command"""
        # abort command, interrupts execution of the main thread.
        self.interrupt_main()

    def _cmd_exit(self):
        """exits the interactive process"""
        self.exit_process()

    def _cmd_mems(self):
        """gets the list of members available for the given expression"""
        expression = self._read_string()
        try:
            name, inst_members, type_members = self.get_members(expression)
        except:
            self._send('MERR')
            _debug_write('error in eval')
            _debug_write(traceback.format_exc())
        else:
            self.send_lock.acquire()
            self.conn.send(ReplBackend._MRES)
            self._write_string(name)
            self._write_member_dict(inst_members)
            self._write_member_dict(type_members)
            self.send_lock.release()

    def _cmd_sigs(self):
        """gets the signatures for the given expression"""
        expression = self._read_string()
        try:
            sigs = self.get_signatures(expression)
        except:
            self._send('SERR')
            _debug_write('error in eval')
            _debug_write(traceback.format_exc())
        else:
            self.send_lock.acquire()
            self.conn.send(ReplBackend._SRES)
            # single overload
            self.conn.send(struct.pack('i', len(sigs)))
            for doc, args, vargs, varkw, defaults in sigs:
                # write overload
                self._write_string((doc or '')[:256])
                arg_count = len(args) + (vargs is not None) + (varkw is not None)
                self.conn.send(struct.pack('i', arg_count))
                for arg in args:
                    self._write_string(arg)

                if vargs is not None:
                    self._write_string('*' + vargs)
                if varkw is not None:
                    self._write_string('**' + varkw)

            self.send_lock.release()
    
    def _cmd_setm(self):
        global exec_mod
        """sets the current module which code will execute against"""
        mod_name = self._read_string()
        self.set_current_module(mod_name)

    def _cmd_mods(self):
        """gets the list of available modules"""
        try:
            res = self.get_module_names()
            res.sort()
        except:
            res = []
        
        self.send_lock.acquire()
        self.conn.send(ReplBackend._MODS)
        self.conn.send(struct.pack('i', len(res)))
        for name, filename in res:
            self._write_string(name)
            self._write_string(filename)
    
        self.send_lock.release()

    def _cmd_inpl(self):
        """handles the input command which returns a string of input"""
        self.input_string = self._read_string()
        self.input_event.release()
    
    def _cmd_excf(self):
        """handles executing a single file"""
        self.execute_file(self._read_string())                

    def _cmd_debug_attach(self):
        port, = struct.unpack('i', self.conn.recv(4))
        id = self._read_string()
        self.attach_process(port, id)

    _COMMANDS = {
        _cmd('run ') : _cmd_run,
        _cmd('abrt') : _cmd_abrt,
        _cmd('exit'): _cmd_exit,
        _cmd('mems') : _cmd_mems,
        _cmd('sigs'): _cmd_sigs,
        _cmd('mods'): _cmd_mods,
        _cmd('setm') : _cmd_setm,
        _cmd('inpl'): _cmd_inpl,
        _cmd('excf'): _cmd_excf,
        _cmd('dbga'): _cmd_debug_attach,
    }

    def _write_member_dict(self, mem_dict):
        self.conn.send(struct.pack('i', len(mem_dict)))
        for name, type_name in mem_dict.items():
            self._write_string(name)
            self._write_string(type_name)

    def _write_string(self, string):
        if isinstance(string, unicode):
            bytes = string.encode('utf8')
            self.conn.send(ReplBackend._UNICODE_PREFIX)
            self.conn.send(struct.pack('i', len(bytes)))
            self.conn.send(bytes)
        else:
            self.conn.send(ReplBackend._ASCII_PREFIX)
            self.conn.send(struct.pack('i', len(string)))
            self.conn.send(string)

    def on_debugger_detach(self):
        self.send_lock.acquire()
        self.conn.send(ReplBackend._DETC)
        self.send_lock.release()

    def send_image(self, filename):
        self.send_lock.acquire()
        self.conn.send(ReplBackend._IMGD)
        self._write_string(filename)
        self.send_lock.release()

    def send_prompt(self, ps1, ps2):
        """sends the current prompt to the interactive window"""
        self.send_lock.acquire()
        self.conn.send(ReplBackend._PRPC)
        self._write_string(ps1)
        self._write_string(ps2)
        self.send_lock.release()
    
    def send_error(self):
        """reports that an error occured to the interactive window"""
        self._send('ERRE')
        
    def send_exit(self):
        """reports the that the REPL process has exited to the interactive window"""
        self._send('EXIT')

    def send_command_executed(self):
        self._send('DONE')
    
    def send_modules_changed(self):
        self._send('MODC')

    def read_line(self):    
        """reads a line of input from standard input"""
        self.send_lock.acquire()        
        self.conn.send(ReplBackend._RDLN)
        self.send_lock.release()

        self.input_event.acquire()
        return self.input_string

    def write_stdout(self, value):
        """writes a string to standard output in the remote console"""
        self.send_lock.acquire()
        self.conn.send(ReplBackend._STDO)
        self._write_string(value)
        self.send_lock.release()
    
    def write_stderr(self, value):
        """writes a string to standard input in the remote console"""
        self.send_lock.acquire()
        self.conn.send(ReplBackend._STDE)
        self._write_string(value)
        self.send_lock.release()

    ################################################################
    # Implementation of execution, etc...
    
    def execution_loop(self):
        """starts processing execution requests"""
        raise NotImplementedError
    
    def run_command(self, command):
        """runs the specified command which is a string containing code"""
        raise NotImplementedError
        
    def execute_file(self, filename):
        """executes the given filename as the main module"""
        raise NotImplementedError

    def interrupt_main(self):
        """aborts the current running command"""
        raise NotImplementedError
        
    def exit_process(self):
        """exits the REPL process"""
        raise NotImplementedError

    def get_members(self, expression):
        """returns a tuple of the type name, instance members, and type members"""
        raise NotImplementedError
        
    def get_signatures(self, expression):
        """returns doc, args, vargs, varkw, defaults."""
        raise NotImplementedError

    def set_current_module(self, module):
        """sets the module which code executes against"""
        raise NotImplementedError
        
    def get_module_names(self):
        """returns a list of module names"""
        raise NotImplementedError

    def flush(self):
        """flushes the stdout/stderr buffers"""
        raise NotImplementedError

    def attach_process(self, port, debugger_id):
        """starts processing execution requests"""
        raise NotImplementedError
    
def exit_work_item():
    sys.exit(0)


if sys.platform == 'cli':
    # We need special handling to reset the abort for keyboard interrupt exceptions
    class ReplAbortException(Exception): pass

    import clr
    clr.AddReference('Microsoft.Dynamic')
    clr.AddReference('Microsoft.Scripting')
    clr.AddReference('IronPython')
    from Microsoft.Scripting import KeyboardInterruptException
    from Microsoft.Scripting import ParamDictionaryAttribute
    from IronPython.Runtime.Operations import PythonOps
    from IronPython.Runtime import PythonContext

    from System import DBNull, ParamArrayAttribute
    builtin_method_descriptor_type = type(list.append)

class _OldClass:
    pass

_OldClassType = type(_OldClass)

class BasicReplBackend(ReplBackend):
    future_bits = 0x3e010   # code flags used to mark future bits

    """Basic back end which executes all Python code in-proc"""
    def __init__(self, mod_name = '__main__', launch_file = None):
        ReplBackend.__init__(self)
        sys.modules[mod_name] = self.exec_mod = imp.new_module(mod_name)
        self.launch_file = launch_file
        self.code_flags = 0
        self.execute_item = None
        self.execute_item_lock = threading.Lock()
        self.execute_item_lock.acquire()    # lock starts acquired (we use it like manual reset event)

    def connect(self, port):
        ReplBackend.connect(self, port)
        sys.stdout = _ReplOutput(self, is_stdout = True)
        sys.stderr = _ReplOutput(self, is_stdout = False)
        sys.stdin = _ReplInput(self)
        if sys.platform == 'cli':
            import System
            System.Console.SetOut(DotNetOutput(self, True))
            System.Console.SetError(DotNetOutput(self, False))

    def run_file_as_main(self, filename):
        contents = open(filename, 'rb').read().replace(_cmd('\r\n'), _cmd('\n'))
        self.code_flags = 0
        code = compile(contents, filename, 'exec')
        self.code_flags |= (code.co_flags & BasicReplBackend.future_bits)
        self.exec_mod.__file__ = filename
        sys.argv = [filename]
        exec(code, self.exec_mod.__dict__, self.exec_mod.__dict__) 

    def execute_code_work_item(self):
        _debug_write('Executing: ' + repr(self.current_code))
        stripped_code = self.current_code.strip()
        code = compile(self.current_code, '<stdin>', 'single', self.code_flags)
        self.code_flags |= (code.co_flags & BasicReplBackend.future_bits)
        exec(code, self.exec_mod.__dict__, self.exec_mod.__dict__)
        self.current_code = None

    def run_one_command(self, cur_modules, cur_ps1, cur_ps2):
        # runs a single iteration of an input, execute file, etc...
        # This is extracted into it's own method so we play nice w/ IronPython thread abort.
        # Otherwise we have a nested exception hanging around and the 2nd abort doesn't
        # work (that's probably an IronPython bug)
        try:    
            new_modules = self._get_cur_module_set()
            try:
                if new_modules != cur_modules:
                    self.send_modules_changed()
            except:
                pass
            cur_modules = new_modules
        
            self.execute_item_lock.acquire()
        
            if self.execute_item is not None:
                try:
                    self.execute_item()
                finally:
                    self.execute_item = None
            
            try:
                self.send_command_executed()
            except SocketError:
                return True, None, None, None
        
            try:
                if cur_ps1 != sys.ps1 or cur_ps2 != sys.ps2:
                    new_ps1 = str(sys.ps1)
                    new_ps2 = str(sys.ps2)
                
                    self.send_prompt(new_ps1, new_ps2)
        
                    cur_ps1 = new_ps1
                    cur_ps2 = new_ps2
            except:
                pass
        except SystemExit:
            self.send_error()
            self.send_exit()
            return True, None, None, None
        except BaseException:
            _debug_write('Exception')
            exc_type, exc_value, exc_tb = sys.exc_info()
            if sys.platform == 'cli':
                if isinstance(exc_value.clsException, System.Threading.ThreadAbortException):
                    try:
                        System.Threading.Thread.ResetAbort()
                    except SystemError:
                        pass
                    sys.stderr.write('KeyboardInterrupt')
                else:
                    # let IronPython format the exception so users can do -X:ExceptionDetail or -X:ShowClrExceptions
                    excep_text = clr.GetCurrentRuntime().GetLanguage(PythonContext).FormatException(exc_value.clsException)
                    sys.stderr.write(excep_text)
            else:
                exc_next = exc_tb.tb_next.tb_next
                sys.stderr.write(''.join(traceback.format_exception(exc_type, exc_value, exc_next)))

            try:
                self.send_error()
            except SocketError:
                _debug_write('err sending DONE')
                return True, None, None, None
        
        return False, cur_modules, cur_ps1, cur_ps2

    def execution_loop(self):
        """loop on the main thread which is responsible for executing code"""
        
        if sys.platform == 'cli' and sys.version_info[:3] < (2, 7, 1):
            # IronPython doesn't support thread.interrupt_main until 2.7.1
            import System
            self.main_thread = System.Threading.Thread.CurrentThread

        # save our selves so global lookups continue to work (required pre-2.6)...
        cur_modules = set()
        try:
            cur_ps1 = sys.ps1
            cur_ps2 = sys.ps2
        except:
            # CPython/IronPython don't set sys.ps1 for non-interactive sessions, Jython and PyPy do
            sys.ps1 = cur_ps1 = '>>> '
            sys.ps2 = cur_ps2 = '... '

        self.send_prompt(cur_ps1, cur_ps2)

        # launch the startup script if one has been specified
        if self.launch_file:
            try:
                self.run_file_as_main(self.launch_file)
            except:
                print('error in launching startup script:')
                traceback.print_exc()

        while True:
            exit, cur_modules, cur_ps1, cur_ps2 = self.run_one_command(cur_modules, cur_ps1, cur_ps2)
            if exit:
                return

    def execute_file_work_item(self):
        self.run_file_as_main(self.current_code)

    @staticmethod
    def _get_cur_module_set():
        """gets the set of modules avoiding exceptions if someone puts something"""
        """weird in there"""

        try:
            return set(sys.modules)
        except:
            res = set()
            for name in sys.modules:
                try:
                    res.add(name)
                except:
                    pass
            return res


    def run_command(self, command):
        self.current_code = command
        self.execute_item = self.execute_code_work_item
        self.execute_item_lock.release()

    def execute_file(self, filename):
        self.current_code = filename
        self.execute_item = self.execute_file_work_item
        self.execute_item_lock.release()

    def interrupt_main(self):
        if sys.platform == 'cli' and sys.version_info[:3] < (2, 7, 1):
            # IronPython doesn't get thread.interrupt_main until 2.7.1
            self.main_thread.Abort(ReplAbortException())
        else:
            thread.interrupt_main()

    def exit_process(self):
        self.execute_item = exit_work_item
        self.execute_item_lock.release()
        sys.exit(0)

    def get_members(self, expression):
        """returns a tuple of the type name, instance members, and type members"""
        val = eval(expression, self.exec_mod.__dict__, self.exec_mod.__dict__)
        t = type(val)

        inst_members = {}
        if hasattr(val, '__dict__'):
            # collect the instance members
            try:
                for mem_name in val.__dict__:
                    mem_t = self._get_member_type(val, mem_name, True)
                    if mem_t is not None:
                        inst_members[mem_name] = mem_t
            except:
                pass

        # collect the type members
        type_members = {}
        for mem_name in dir(val):
            if mem_name not in inst_members:
                mem_t = self._get_member_type(val, mem_name, False)
                if mem_t is not None:
                    type_members[mem_name] = mem_t

        return t.__module__ + '.' + t.__name__, inst_members, type_members

    def get_ipy_sig(self, obj, ctor):
        args = []
        vargs = None
        varkw = None
        defaults = []
        for param in ctor.GetParameters():
            if param.IsDefined(ParamArrayAttribute, False):
                vargs = param.Name
            elif param.IsDefined(ParamDictionaryAttribute, False):
                varkw = param.Name
            else:
                args.append(param.Name)

            if param.DefaultValue is not DBNull.Value:
                defaults.append(repr(param.DefaultValue))

        return obj.__doc__, args, vargs, varkw, tuple(defaults)
    
    def get_signatures(self, expression):
        val = eval(expression, self.exec_mod.__dict__, self.exec_mod.__dict__)
        doc = val.__doc__
        type_obj = None
        if isinstance(val, type) or isinstance(val, _OldClassType):
            type_obj = val
            val = val.__init__

        try:
            args, vargs, varkw, defaults = inspect.getargspec(val)
        except TypeError:
            # we're not doing inspect on a Python function...
            if sys.platform == 'cli':
                if type_obj is not None:
                    clr_type = clr.GetClrType(type_obj)
                    ctors = clr_type.GetConstructors()
                    return [self.get_ipy_sig(type_obj, ctor) for ctor in ctors]
                elif type(val) is types.BuiltinFunctionType:
                    return [self.get_ipy_sig(target, target.Targets[0]) for target in val.Overloads.Functions]
                elif type(val) is builtin_method_descriptor_type:
                    val = PythonOps.GetBuiltinMethodDescriptorTemplate(val)
                    return [self.get_ipy_sig(target, target.Targets[0]) for target in val.Overloads.Functions]
            raise

        remove_self = type_obj is not None or (type(val) is types.MethodType and 
                        ((sys.version >= '3.0' and val.__self__ is not None) or
                        (sys.version < '3.0' and val.im_self is not None)))

        if remove_self:
            # remove self for instance methods and types
            args = args[1:]
            
        if defaults is not None:
            defaults = [repr(default) for default in defaults]
        return [(doc, args, vargs, varkw, defaults)]

    def set_current_module(self, module):
        mod = sys.modules.get(module)
        if mod is not None:
            _debug_write('Setting module to ' + module)
            self.exec_mod = mod
        else:
            _debug_write('Unknown module ' + module)

    def get_module_names(self):
        res = []
        for name, module in sys.modules.items():
            try:
                if (name == '__main__' or 
                   (name != 'visualstudio_py_repl' and module.__file__ is not None)):
                    res.append((name, module.__file__))
            except:
                pass
        return res
    
    def flush(self):
        sys.stdout.flush()

    def do_detach(self):
        import visualstudio_py_debugger
        visualstudio_py_debugger.DETACH_CALLBACKS.remove(self.do_detach)
        self.on_debugger_detach()

    def attach_process(self, port, debugger_id):
        def execute_attach_process_work_item():
            import visualstudio_py_debugger
            visualstudio_py_debugger.DETACH_CALLBACKS.append(self.do_detach)
            visualstudio_py_debugger.attach_process(port, debugger_id, True)        
        
        self.execute_item = execute_attach_process_work_item
        self.execute_item_lock.release()
    
    @staticmethod
    def _get_member_type(inst, name, from_dict):
        try:
            if from_dict:
                val = inst.__dict__[name] 
            else:
                val = getattr(type(inst), name)
            mem_t = type(val)
            mem_t_name = mem_t.__module__ + '.' + mem_t.__name__
            return mem_t_name
        except:
            return


class _ReplOutput(object):
    """file like object which redirects output to the repl window."""
    def __init__(self, backend, is_stdout):
        self.backend = backend
        self.is_stdout = is_stdout

    def flush(self):
        pass
    
    def writelines(self, lines):
        pass
    
    def write(self, value):
        _debug_write('printing ' + repr(value) + '\n')
        if self.is_stdout:
            self.backend.write_stdout(value)
        else:
            self.backend.write_stderr(value)
    
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


class _ReplInput(object):
    """file like object which redirects input from the repl window"""
    def __init__(self, backend):
        self.backend = backend
    
    def readline(self):
        return self.backend.read_line()
    
    def readlines(self, size = None):
        res = []
        while True:
            line = self.readline()
            if line is not None:
                res.append(line)
            else:
                break
        
        return res

    def xreadlines(self):
        return self
    
    def write(self, *args):
        raise IOError("File not open for writing")

    def flush(self): pass

    def isatty(self):
        return True

    def __iter__(self):
        return self

    def next(self):
        return self.readline()


if sys.platform == 'cli':
    import System
    class DotNetOutput(System.IO.TextWriter):        
        def __new__(cls, backend, is_stdout):
            return System.IO.TextWriter.__new__(cls)
        
        def __init__(self, backend, is_stdout):
            self.backend = backend
            self.is_stdout = is_stdout

        def Write(self, value, *args):
            if not args:
                if type(value) is str or type(value) is System.Char:
                    if self.is_stdout:
                        self.backend.write_stdout(str(value).replace('\r\n', '\n'))
                    else:
                        self.backend.write_stderr(str(value).replace('\r\n', '\n'))
                else:
                    super(DotNetOutput, self).Write(value)
            else:
                super(DotNetOutput, self).Write(value, *args)

        def WriteLine(self, value, *args):
            if not args:
                if type(value) is str or type(value) is System.Char:
                    if self.is_stdout:
                        self.backend.write_stdout(str(value).replace('\r\n', '\n') + '\n')
                    else:
                        self.backend.write_stderr(str(value).replace('\r\n', '\n') + '\n')
                else:
                    super(DotNetOutput, self).Write(value)
            else:
                super(DotNetOutput, self).WriteLine(value, *args)

        @property
        def Encoding(self):
            return System.Text.Encoding.UTF8
    

BACKEND = None

def _run_repl():
    from optparse import OptionParser

    parser = OptionParser(prog='repl', description='Process REPL options')
    parser.add_option('--port', dest='port',
                   help='the port to connect back to')
    parser.add_option('--launch_file', dest='launch_file',
                   help='the script file to run on startup')
    parser.add_option('--execution_mode', dest='backend',
                   help='the backend to use')
    parser.add_option('--enable-attach', dest='enable_attach', 
                    action="store_true", default=False,
                   help='enable attaching the debugger via $attach')

    (options, args) = parser.parse_args()
    
    # kick off repl
    # make us available under our "normal" name, not just __main__ which we'll likely replace.
    sys.modules['visualstudio_py_repl'] = sys.modules['__main__']
    global __name__
    __name__ = 'visualstudio_py_repl'
    
    if options.enable_attach:
        # importing this hooks start_new_thread
        import visualstudio_py_debugger
        visualstudio_py_debugger.DONT_DEBUG.append(__file__)
        new_thread = visualstudio_py_debugger.new_thread()
        sys.settrace(new_thread.trace_func)
        visualstudio_py_debugger.intercept_threads(True)

    backend_type = BasicReplBackend
    backend_error = None
    if options.backend is not None and options.backend.lower() != 'standard':
        try:
            split_backend = options.backend.split('.')
            backend_mod_name = '.'.join(split_backend[:-1])
            backend_name = split_backend[-1]
            backend_type = getattr(__import__(backend_mod_name), backend_name)
        except UnsupportedReplException:
            backend_error = sys.exc_info()[1].reason
        except:
            backend_error = traceback.format_exc()

    # fix sys.path so that cwd is where the project lives.
    sys.path[0] = os.getcwd()
    # remove all of our parsed args in case we have a launch file that cares...
    sys.argv = args or ['']

    global BACKEND
    BACKEND = backend_type(launch_file=options.launch_file)
    BACKEND.connect(int(options.port))

    if backend_error is not None:
        sys.stderr.write('Error using selected REPL back-end:\n')
        sys.stderr.write(backend_error + '\n')
        sys.stderr.write('Using standard backend instead\n')

    # execute code on the main thread which we can interrupt
    BACKEND.execution_loop()    

if __name__ == '__main__':
    try:
        _run_repl()
    except:
        if DEBUG:
            _debug_write(traceback.format_exc())
            _debug_write('exiting')
            input()
        raise
