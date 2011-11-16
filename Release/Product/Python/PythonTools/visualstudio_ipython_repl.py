"""Implements REPL support over IPython/ZMQ for VisualStudio"""

from visualstudio_py_repl import BasicReplBackend, ReplBackend, UnsupportedReplException
try:
    from IPython.zmq import kernelmanager
    from IPython.zmq.kernelmanager import ShellSocketChannel, KernelManager, SubSocketChannel, StdInSocketChannel, HBSocketChannel
    from IPython.utils.traitlets import Type
except ImportError:
    import sys
    exc_value = sys.exc_info()[1]

    raise UnsupportedReplException('IPython mode requires IPython 0.11 or later: ' + str(exc_value))

import thread
import sys
from base64 import decodestring

# TODO: SystemExit exceptions come back to us as strings, can we automatically exit when ones raised somehow?

#####
# Channels which forward events

# Description of the messaging protocol
# http://ipython.scipy.org/doc/manual/html/development/messaging.html 

def unknown_command(content): 
    import pprint
    pprint.pprint(content)

class DefaultHandler(object):
    def call_handlers(self, msg):
        # msg_type:
        #   execute_reply
        msg_type = 'handle_' + msg['msg_type']
        
        getattr(self, msg_type, unknown_command)(msg['content'])
    
class VsShellSocketChannel(DefaultHandler, ShellSocketChannel):    
    
    def handle_execute_reply(self, content):
        # we could have a payload here...
        payload = content['payload']
        
        for item in payload:
            output = item.get('text', None)
            if output is not None:
                self._vs_backend.write_stdout(output)
        self._vs_backend.send_command_executed()     
        
    def handle_object_info_reply(self, content):
        self._vs_backend.object_info_reply = content        
        self._vs_backend.members_lock.release()

    def handle_complete_reply(self, content):
        self._vs_backend.complete_reply = content        
        self._vs_backend.members_lock.release()

class VsSubSocketChannel(DefaultHandler, SubSocketChannel):    
    def call_handlers(self, msg):
        msg_type = 'handle_' + msg['msg_type']
        getattr(self, msg_type, unknown_command)(msg['content'])
        
    def handle_display_data(self, content):
        # called when user calls display()
        data = content.get('data', None)
        
        if data is not None:
            self.write_data(data)
    
    def handle_stream(self, content):
        stream_name = content['name']
        output = content['data']
        if stream_name == 'stdout':
            self._vs_backend.write_stdout(output)
        elif stream_name == 'stderr':
            self._vs_backend.write_stderr(output)
        # TODO: stdin can show up here, do we echo that?
    
    def handle_pyout(self, content):
        # called when an expression statement is printed, we treat 
        # identical to stream output but it always goes to stdout
        output = content['data']
        execution_count = content['execution_count']
        self._vs_backend.execution_count = execution_count + 1
        self._vs_backend.send_prompt('\r\nIn [%d]: ' % (execution_count + 1), '   ' + ('.' * (len(str(execution_count + 1)) + 2)) + ': ', False)
        self.write_data(output, execution_count)
        
    def write_data(self, data, execution_count = None):
        
        output_png = data.get('image/png', None)
        if output_png is not None:
            try:            
                self._vs_backend.write_png(decodestring(output_png))
                self._vs_backend.write_stdout('\n') 
                return
            except:
                pass
            
        output_str = data.get('text/plain', None)
        if output_str is not None:
            if execution_count is not None:
                output_str = 'Out[' + str(execution_count) + ']: ' + output_str

            self._vs_backend.write_stdout(output_str)        
            self._vs_backend.write_stdout('\n') 
            return

    def handle_pyerr(self, content):
        # TODO: this includes escape sequences w/ color, we need to unescape that
        ename = content['ename']
        evalue = content['evalue']
        tb = content['traceback']
        self._vs_backend.write_stderr('\n'.join(tb))
        self._vs_backend.write_stdout('\n')
    
    def handle_pyin(self, content):
        # just a rebroadcast of the command to be executed, can be ignored
        self._vs_backend.execution_count += 1
        self._vs_backend.send_prompt('\r\nIn [%d]: ' % (self._vs_backend.execution_count), '   ' + ('.' * (len(str(self._vs_backend.execution_count)) + 2)) + ': ', False)
        pass
        
    def handle_status(self, content):
        pass


class VsStdInSocketChannel(DefaultHandler, StdInSocketChannel):
    def handle_input_request(self, content):
        # queue this to another thread so we don't block the channel
        def read_and_respond():
            value = self._vs_backend.read_line()
        
            self.input(value)
            
        thread.start_new_thread(read_and_respond, ())


class VsHBSocketChannel(DefaultHandler, HBSocketChannel):
    pass


class VsKernelManager(KernelManager):
    shell_channel_class = Type(VsShellSocketChannel)
    sub_channel_class = Type(VsSubSocketChannel)
    stdin_channel_class = Type(VsStdInSocketChannel)
    hb_channel_class = Type(VsHBSocketChannel)


class IPythonBackend(ReplBackend):
    def __init__(self, mod_name = '__main__', launch_file = None):
        ReplBackend.__init__(self)
        self.launch_file = launch_file
        self.mod_name = mod_name
        self.km = VsKernelManager()
        self.km.start_kernel(**{'ipython': True, 'extra_arguments': self.get_extra_arguments()})
        self.km.start_channels()
        self.exit_lock = thread.allocate_lock()
        self.exit_lock.acquire()     # used as an event
        self.members_lock = thread.allocate_lock()
        self.members_lock.acquire()
        
        self.km.shell_channel._vs_backend = self
        self.km.stdin_channel._vs_backend = self
        self.km.sub_channel._vs_backend = self
        self.km.hb_channel._vs_backend = self        
        self.execution_count = 1        

    def get_extra_arguments(self):
        if sys.version <= '2.':
            return [unicode('--pylab=inline')]
        return ['--pylab=inline']
        
    def execute_file_as_main(self, filename):
        contents = open(filename, 'rb').read().replace("\r\n", "\n")
        code = '''
import sys
sys.argv = [%(filename)r]
__file__ = %(filename)r
del sys
exec(compile(%(contents)r, %(filename)r, 'exec')) 
''' % {'filename' : filename, 'contents':contents}
        
        self.run_command(code, True)

    def execution_loop(self):
        # launch the startup script if one has been specified
        if self.launch_file:
            self.execute_file_as_main(self.launch_file)

        # we've got a bunch of threads setup for communication, we just block
        # here until we're requested to exit.  
        self.send_prompt('\r\nIn [1]: ', '   ...: ', False)
        self.exit_lock.acquire()
    
    def run_command(self, command, silent = False):
        self.km.shell_channel.execute(command, silent)
        
    def execute_file(self, filename):
        self.execute_file_as_main(filename)

    def exit_process(self):
        self.exit_lock.release()

    def get_members(self, expression):
        """returns a tuple of the type name, instance members, and type members"""      
        text = expression + '.'
        self.km.shell_channel.complete(text, text, 1)
                
        self.members_lock.acquire()
        
        reply = self.complete_reply
        
        res = {}
        text_len = len(text)
        for member in reply['matches']:
            res[member[text_len:]] = 'object'

        return ('unknown', res, {})
        
    def get_signatures(self, expression):
        """returns doc, args, vargs, varkw, defaults."""
        
        self.km.shell_channel.object_info(expression)
        
        self.members_lock.acquire()
        
        reply = self.object_info_reply 
        argspec = reply['argspec']
        return [(reply['docstring'], argspec['args'], argspec['varargs'], argspec['varkw'], argspec['defaults'])]

    def interrupt_main(self):
        """aborts the current running command"""
        self.km.interrupt_kernel()
        
    def set_current_module(self, module):
        pass
        
    def get_module_names(self):
        """returns a list of module names"""
        return []

    def flush(self):
        pass

    def init_debugger(self):
        from os import path
        self.run_command('''
def __visualstudio_debugger_init():    
    import sys
    sys.path.append(''' + repr(path.dirname(__file__)) + ''')
    import visualstudio_py_debugger
    new_thread = visualstudio_py_debugger.new_thread()
    sys.settrace(new_thread.trace_func)
    visualstudio_py_debugger.intercept_threads(True)

__visualstudio_debugger_init()
del __visualstudio_debugger_init
''', True)

    def attach_process(self, port, debugger_id):
        self.run_command('''
def __visualstudio_debugger_attach():
    import visualstudio_py_debugger

    def do_detach():
        visualstudio_py_debugger.DETACH_CALLBACKS.remove(do_detach)

    visualstudio_py_debugger.DETACH_CALLBACKS.append(do_detach)
    visualstudio_py_debugger.attach_process(''' + str(port) + ''', ''' + repr(debugger_id) + ''', True)        

__visualstudio_debugger_attach()
del __visualstudio_debugger_attach
''', True)

class IPythonBackendWithoutPyLab(IPythonBackend):
    def get_extra_arguments(self):
        return []
