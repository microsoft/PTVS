"""Implements REPL support over IPython/ZMQ for VisualStudio"""

from visualstudio_py_repl import BasicReplBackend, ReplBackend, UnsupportedReplException
try:
    from IPython.zmq import kernelmanager
    from IPython.zmq.kernelmanager import XReqSocketChannel, KernelManager, SubSocketChannel, RepSocketChannel, HBSocketChannel
    from IPython.utils.traitlets import Type
except ImportError:
    raise UnsupportedReplException('IPython mode requires IPython 0.11 or later.')

import thread

# TODO: SystemExit exceptions come back to us as strings, can we automatically exit when ones raised somehow?

x = []

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

        x.append((self.__class__.__name__, msg))
    
class VsXReqSocketChannel(DefaultHandler, XReqSocketChannel):    
    
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
        
        x.append(('sub', msg))
        
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
        self._vs_backend.write_stdout(output)        
        self._vs_backend.write_stdout('\n') 
        
    def handle_pyerr(self, content):
        # TODO: this includes escape sequences w/ color, we need to unescape that
        ename = content['ename']
        evalue = content['evalue']
        tb = content['traceback']
        self._vs_backend.write_stderr('\n'.join(tb))
        self._vs_backend.write_stdout('\n')
    
    def handle_pyin(self, content):
        # just a rebroadcast of the command to be executed, can be ignored
        pass
        
    def handle_status(self, content):
        pass


class VsRepSocketChannel(DefaultHandler, RepSocketChannel):
    def handle_input_request(self, content):
        # queue this to another thread so we don't block the channel
        def read_and_respond():
            value = self._vs_backend.read_line()
        
            self.input(value)
            
        thread.start_new_thread(read_and_respond, ())


class VsHBSocketChannel(DefaultHandler, HBSocketChannel):
    pass


class VsKernelManager(KernelManager):
    xreq_channel_class = Type(VsXReqSocketChannel)
    sub_channel_class = Type(VsSubSocketChannel)
    rep_channel_class = Type(VsRepSocketChannel)
    hb_channel_class = Type(VsHBSocketChannel)


class IPythonBackend(ReplBackend):
    def __init__(self, mod_name = '__main__', launch_file = None):
        ReplBackend.__init__(self)
        self.launch_file = launch_file
        self.mod_name = mod_name
        self.km = VsKernelManager()
        self.km.start_kernel()
        self.km.start_channels()
        self.exit_lock = thread.allocate_lock()
        self.exit_lock.acquire()     # used as an event
        self.members_lock = thread.allocate_lock()
        self.members_lock.acquire()
        
        self.km.xreq_channel._vs_backend = self
        self.km.rep_channel._vs_backend = self
        self.km.sub_channel._vs_backend = self
        self.km.hb_channel._vs_backend = self        
        

    def execution_loop(self):
        # we've got a bunch of threads setup for communication, we just block
        # here until we're requested to exit.    
        self.exit_lock.acquire()
    
    def run_command(self, command):
        self.km.xreq_channel.execute(command, False)
        
    def execute_file(self, filename):
        self.km.xreq_channel.execute(file(filename).read(), False)

    def exit_process(self):
        self.exit_lock.release()

    def get_members(self, expression):
        """returns a tuple of the type name, instance members, and type members"""      
        text = expression + '.'
        self.km.xreq_channel.complete(text, text, 1)
                
        self.members_lock.acquire()
        
        reply = self.complete_reply
        
        res = {}
        text_len = len(text)
        for member in reply['matches']:
            res[member[text_len:]] = 'object'

        return ('unknown', res, {})
        
    def get_signatures(self, expression):
        """returns doc, args, vargs, varkw, defaults."""
        
        self.km.xreq_channel.object_info(expression)
        
        self.members_lock.acquire()
        
        reply = self.object_info_reply 
        argspec = reply['argspec']
        return reply['docstring'], argspec['args'], argspec['varargs'], argspec['varkw'], argspec['defaults']

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

#
if __name__ == '__main__':
    print 'starting'
    km = VsKernelManager()
    km.start_kernel()
    km.start_channels()
