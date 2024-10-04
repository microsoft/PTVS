# Python Tools for Visual Studio
# Copyright(c) Microsoft Corporation
# All rights reserved.
# 
# Licensed under the Apache License, Version 2.0 (the License); you may not use
# this file except in compliance with the License. You may obtain a copy of the
# License at http://www.apache.org/licenses/LICENSE-2.0
# 
# THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
# OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
# IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
# MERCHANTABILITY OR NON-INFRINGEMENT.
# 
# See the Apache Version 2.0 License for specific language governing
# permissions and limitations under the License.

from __future__ import absolute_import, print_function

"""Implements REPL support over IPython/ZMQ for VisualStudio"""

__author__ = "Microsoft Corporation <ptvshelp@microsoft.com>"
__version__ = "3.2.1.0"

import ast
import base64
import errno
import os
import re
import sys
import threading
import time
import traceback
from ptvsd.repl import BasicReplBackend, ReplBackend, UnsupportedReplException, _command_line_to_args_list, DEBUG
from ptvsd.util import to_bytes

try:
    import jupyter_client
    import jupyter_client.manager
    import zmq.error
except ImportError:
    raise UnsupportedReplException("Jupyter mode requires the jupyter_client and ipykernel packages. " + traceback.format_exc())

try:
    import _thread
except ImportError:
    import thread as _thread    # legacy name

try:
    from queue import Empty
except ImportError:
    from Queue import Empty

# Use safer eval
eval = ast.literal_eval

class Message(object):
    _sentinel = object()

    def __init__(self, msg):
        self._m = msg
        self._read = False

    def __getattr__(self, attr):
        v = self[attr, self._sentinel]
        if v is self._sentinel:
            return Message({})
        if isinstance(v, dict):
            return Message(v)
        return v

    def __getitem__(self, key):
        if isinstance(key, tuple):
            key, default_value = key
        else:
            default_value = None
        if not self._m:
            return self
        try:
            v = self._m[key]
        except KeyError:
            return default_value
        if isinstance(v, dict):
            return Message(v)
        return v

    def __repr__(self):
        return repr(self._m)

class IntrospectHandler(object):
    def __init__(self, client, on_reply, suppress_io):
        self._client = client
        self._on_reply = on_reply
        self._suppress_io = suppress_io
        self.callback = None
        self.error = None
        self.typename = None
        self.members = None
        self.responded = False

    def send(self, expression):
        if not expression:
            self.typename = ''
            msg_id = self._client.complete("")
            self._suppress_io.add(msg_id)
            self._on_reply.setdefault((msg_id, 'complete_reply'), []).append(self.complete_reply)
        else:
            msg_id = self._client.execute("_=" + expression,
                store_history=False, allow_stdin=False, silent=True,
                user_expressions={'m': 'getattr(type(_), "__module__", "") + "." + type(_).__name__'},
            )
            self._suppress_io.add(msg_id)
            self._on_reply.setdefault((msg_id, 'execute_reply'), []).append(self.typename_reply)
            msg_id = self._client.complete(expression + '.')
            self._suppress_io.add(msg_id)
            self._on_reply.setdefault((msg_id, 'complete_reply'), []).append(self.complete_reply)

    def _respond(self, success):
        if self.responded:
            return
        if not self.callback:
            raise RuntimeError("No callback provider to message handler")
        if success:
            self.callback(self.typename, self.members, {})
        else:
            self.error()
        self.responded = True

    def complete_reply(self, message):
        if message.content.status != 'ok':
            self._respond(False)
            return
        self.members = dict((m.rpartition('.')[-1], '') for m in message.content['matches', ()])
        omit = [m for m in self.members if m.startswith('__ptvs_repl_')]
        for m in omit:
            del self.members[m]
        if self.typename is not None:
            self._respond(True)

    def typename_reply(self, message):
        if message.content.status != 'ok' or message.content.user_expressions.m.status != 'ok':
            self._respond(False)
            return
        self.typename = eval(message.content.user_expressions.m.data['text/plain', '"object"'])
        m, _, n = self.typename.partition('.')
        if m == type(int).__module__:
            self.typename = n
        if self.members is not None:
            self._respond(True)

    def set_callback(self, success_callback, error_callback):
        self.callback = success_callback
        self.error = error_callback

class SignaturesHandler(object):
    def __init__(self, client, on_reply, suppress_io):
        self._client = client
        self._on_reply = on_reply
        self._suppress_io = suppress_io
        self.callback = None
        self.error = None
        self.signatures = None
        self.responded = False

    def send(self, expression):
        if not expression:
            self.signatures = []
            self._respond(False)
            return

        msg_id = self._client.execute("pass",
            store_history=False, allow_stdin=False, silent=True,
            user_expressions={'sigs': '__ptvs_repl_sig(' + expression + ')'},
        )
        self._suppress_io.add(msg_id)
        self._on_reply.setdefault((msg_id, 'execute_reply'), []).append(self.signatures_reply)

    def _respond(self, success):
        if self.responded:
            return
        if not self.callback:
            raise RuntimeError("No callback provider to message handler")
        if success:
            self.callback(self.signatures)
        else:
            self.error()
        self.responded = True

    def signatures_reply(self, message):
        if message.content.status != 'ok' or message.content.user_expressions.sigs.status != 'ok':
            self._respond(False)
            return
        self.signatures = eval(message.content.user_expressions.sigs.data['text/plain', '[]'])
        self._respond(True)

    def set_callback(self, success_callback, error_callback):
        self.callback = success_callback
        self.error = error_callback

EXEC_HELPERS_COMMAND = """#nohistory
def __ptvs_repl_exec_helpers():
    with open(%r, 'rb') as f:
        content = f.read().replace('\\r\\n'.encode('ascii'), '\\n'.encode('ascii'))
    exec(content, globals())
__ptvs_repl_exec_helpers()
""" % os.path.join(os.path.dirname(os.path.abspath(__file__)), 'jupyter_client-helpers.py')

class JupyterClientBackend(ReplBackend):
    def __init__(self, mod_name='__main__', launch_file=None):
        super(JupyterClientBackend, self).__init__()
        self.__client = None

        # This lock will be released when we should shut down
        self.__exit = threading.Lock()
        self.__exit.acquire()

        self.__lock = threading.RLock()
        self.__status = 'idle'
        self.__msg_buffer = []
        self.__cmd_buffer = [EXEC_HELPERS_COMMAND]
        self.__on_reply = {}
        self.__suppress_io = set()

    def execution_loop(self):
        """starts processing execution requests"""
        try:
            return self._execution_loop()
        except:
            # TODO: Better fatal error handling
            traceback.print_exc()
            try:
                raw_input()
            except NameError:
                input()
            raise

    def _execution_loop(self):
        km, kc = jupyter_client.manager.start_new_kernel()
        try:
            self.exit_requested = False
            self.__client = kc
            self.send_cwd()

            self.__shell_thread = _thread.start_new_thread(self.__shell_threadproc, (kc,))
            self.__iopub_thread = _thread.start_new_thread(self.__iopub_threadproc, (kc,))

            self.__exit.acquire()

            self.send_exit()
        finally:
            kc.stop_channels()
            km.shutdown_kernel(now=True)

    def __command_executed(self, msg):
        if msg.msg_type == 'execute_reply':
            self.__handle_payloads(msg.content['payload'])

        self.send_command_executed()

    def run_command(self, command):
        """runs the specified command which is a string containing code"""
        if self.__client:
            with self.__lock:
                self.__exec(command, store_history=True, silent=False).append(self.__command_executed)
            return True

        self.__cmd_buffer.append(command)
        return False

    def __exec(self, command, store_history=False, allow_stdin=False, silent=True, get_vars=None):
        with self.__lock:
            msg_id = self.__client.execute(
                command,
                store_history=store_history,
                allow_stdin=allow_stdin,
                silent=silent,
                user_expressions=get_vars,
            )
            return self.__on_reply.setdefault((msg_id, 'execute_reply'), [])

    def execute_file_ex(self, filetype, filename, args):
        """executes the given filename as a 'script', 'module' or 'process'."""
        if filetype == 'process':
            command = "!%s %s" % (filename, args)
        else:
            command = "__ptvs_repl_exec_%s(%r, %r, globals(), locals())" % (filetype, filename, args)
        if self.__client:
            self.__exec(command, silent=False).append(self.__command_executed)
            return True

        self.__cmd_buffer.append(command)
        return False

    def interrupt_main(self):
        """aborts the current running command"""
        #raise NotImplementedError
        pass

    def exit_process(self):
        """exits the REPL process"""
        self.exit_requested = True
        self.__exit.release()

    def get_members(self, expression):
        handler = IntrospectHandler(self.__client, self.__on_reply, self.__suppress_io)
        with self.__lock:
            handler.send(expression)
            return handler.set_callback

    def get_signatures(self, expression):
        """returns doc, args, vargs, varkw, defaults."""
        handler = SignaturesHandler(self.__client, self.__on_reply, self.__suppress_io)
        with self.__lock:
            handler.send(expression)
            return handler.set_callback

    def set_current_module(self, module):
        """sets the module which code executes against"""
        pass    # not supported

    def set_current_thread_and_frame(self, thread_id, frame_id, frame_kind):
        """sets the current thread and frame which code will execute against"""
        pass    # not supported

    def get_module_names(self):
        """returns a list of module names"""
        return []   # not supported

    def flush(self):
        """flushes the stdout/stderr buffers"""
        pass

    def __shell_threadproc(self, client):
        try:
            last_exec_count = None
            on_replies = self.__on_reply
            while not self.exit_requested:
                while self.__cmd_buffer and not self.exit_requested:
                    cmd = self.__cmd_buffer.pop(0)
                    if cmd.startswith('#nohistory'):
                        self.__exec(cmd)
                    else:
                        self.run_command(cmd)
                if self.exit_requested:
                    break

                try:
                    m = Message(client.get_shell_msg(timeout=0.1))
                    msg_id = m.msg_id
                    msg_type = m.msg_type

                    print('%s: %s' % (msg_type, msg_id))

                    exec_count = m.content['execution_count', None]
                    if exec_count != last_exec_count and exec_count is not None:
                        last_exec_count = exec_count
                        exec_count = int(exec_count) + 1
                        ps1 = 'In [%s]: ' % exec_count
                        ps2 = ' ' * (len(ps1) - 5) + '...: '
                        self.send_prompt('\n' + ps1, ps2, allow_multiple_statements=True)

                    parent_id = m.parent_header['msg_id', None]
                    if parent_id:
                        on_reply = on_replies.pop((parent_id, msg_type), ())
                        for callable in on_reply:
                            callable(m)
                except zmq.Again:
                    pass  # Handle timeout without hanging
        except zmq.error.ZMQError:
            self.exit_process()
        except KeyboardInterrupt:
            self.exit_process()
        except:
            # TODO: Better fatal error handling
            traceback.print_exc()
            try:
                raw_input()
            except NameError:
                input()
            self.exit_process()

    def __iopub_threadproc(self, client):
        try:
            last_exec_count = None
            while not self.exit_requested:
                m = Message(client.get_iopub_msg(block=True))

                if m.parent_header.msg_id in self.__suppress_io:
                    if m.msg_type != 'status':
                        self.__suppress_io.discard(m.parent_header.msg_id)
                    continue

                if m.msg_type == 'execute_input':
                    pass
                elif m.msg_type == 'execute_result':
                    self.__write_result(m.content)
                elif m.msg_type == 'display_data':
                    self.__write_content(m.content)
                elif m.msg_type == 'stream':
                    self.__write_stream(m.content)
                elif m.msg_type == 'error':
                    self.__write_result(m.content, treat_as_error=True)
                elif m.msg_type == 'status':
                    self.__status = m.content['execution_state', 'idle']
                else:
                    print("Received: " + m.msg_type + ":" + str(m) + "\n")
                    self.write_stdout(str(m) + '\n')

        except zmq.error.ZMQError:
            self.exit_process()
        except KeyboardInterrupt:
            self.exit_process()
        except:
            # TODO: Better fatal error handling
            traceback.print_exc()
            try:
                raw_input()
            except NameError:
                input()
            self.exit_process()

    def __write_stream(self, content):
        if content.name == 'stderr':
            f = self.write_stderr
        else:
            f = self.write_stdout
        text = content.text
        if text:
            f(text)

    def __write_result(self, content, treat_as_error=False):
        exec_count = content['execution_count']
        if exec_count is not None:
            prefix = 'Out [%s]: ' % exec_count
        else:
            prefix = 'Out: '

        if treat_as_error or content['status'] == 'error':
            tb = content['traceback']
            if tb:
                self.write_stderr(prefix + '\n')
                for line in tb:
                    self.write_stderr(line + '\n')
                return

        if content['status', 'ok'] == 'ok':
            output_str = content.data['text/plain']
            if output_str is None:
                output_str = str(content.data)
            if '\n' in output_str:
                output_str = '%s\n%s\n' % (prefix, output_str)
            else:
                output_str = prefix + output_str + '\n'
            self.write_stdout(output_str)
            return

        self.write_stderr(str(content) + '\n')
        self.send_error()

    def __handle_payloads(self, payloads):
        if not payloads:
            return
        for p in payloads:
            print(p['source'], p)

    def __write_content(self, content):
        if content['status', 'ok'] != 'ok':
            return

        output_xaml = content.data['application/xaml+xml']
        if output_xaml is not None:
            try:
                if isinstance(output_xaml, str) and sys.version_info[0] >= 3:
                    output_xaml = output_xaml.encode('ascii')
                self.write_xaml(base64.decodestring(output_xaml))
                self.write_stdout('\n')
                return
            except Exception:
                if DEBUG:
                    raise

        output_png = content.data['image/png', None]
        if output_png is not None:
            try:
                if isinstance(output_png, str) and sys.version_info[0] >= 3:
                    output_png = output_png.encode('ascii')
                self.write_png(base64.decodestring(output_png))
                self.write_stdout('\n')
                return
            except Exception:
                if DEBUG:
                    raise

