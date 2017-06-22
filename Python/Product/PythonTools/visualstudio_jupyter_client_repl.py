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
# MERCHANTABLITY OR NON-INFRINGEMENT.
# 
# See the Apache Version 2.0 License for specific language governing
# permissions and limitations under the License.

from __future__ import print_function

"""Implements REPL support over IPython/ZMQ for VisualStudio"""

__author__ = "Microsoft Corporation <ptvshelp@microsoft.com>"
__version__ = "3.0.0.0"

import ast
import re
import sys
import time
import traceback
from visualstudio_py_repl import BasicReplBackend, ReplBackend, UnsupportedReplException, _command_line_to_args_list
from visualstudio_py_util import to_bytes

try:
    import jupyter_client
    import jupyter_client.manager
except ImportError:
    raise UnsupportedReplException("Jupyter mode requires the jupyter_client and ipykernel packages")

try:
    import _thread
except ImportError:
    import thread as _thread    # legacy name

try:
    from queue import Empty
except ImportError:
    from Queue import Empty

class Message(object):
    _sentinel = object()

    def __init__(self, msg):
        self._m = msg
        self._read = False

    def __getattr__(self, attr):
        return self[attr]

    def __getitem__(self, key):
        if not self._m:
            return self
        try:
            v = self._m[key]
        except KeyError:
            return Message({})
        if not isinstance(v, dict):
            return v
        return Message(v)

    def __repr__(self):
        return repr(self._m)

class JupyterClientBackend(ReplBackend):
    def __init__(self, mod_name='__main__', launch_file=None):
        super(JupyterClientBackend, self).__init__()
        self.__client = None
        self.__msg_buffer = []
        self.__cur_ps1, self.__cur_ps2 = None, None
        self.__cmd_buffer = []

    def __fill_message_buffer(self, timeout=None):
        mb = self.__msg_buffer
        removed = len(mb)
        mb[:] = (m for m in mb if not m._read)
        removed -= len(mb)

        added = -len(mb)
        try:
            while True:
                msg = self.__client.shell_channel.get_msg(block=True, timeout=timeout)
                mb.append(Message(msg))
        except (TimeoutError, Empty):
            pass
        added += len(mb)
        return added, removed

    def __get_reply(self, msg_id, msg_type, timeout=1.0):
        stop_at = time.clock() + timeout
        added = 1
        while added or time.clock() < stop_at:
            for m in self.__msg_buffer:
                if not m._read and m.parent_header.msg_id == msg_id and m.header.msg_type == msg_type:
                    m._read = True
                    return m
            added, _ = self.__fill_message_buffer(timeout=0.1)

    def execution_loop(self):
        """starts processing execution requests"""
        with jupyter_client.run_kernel() as client:
            self.exit_requested = False
            self.__client = client
            while not self.exit_requested:
                # Forward output
                self.flush()

                # Get the current prompts
                self.__get_prompts()

                self.__fill_message_buffer(timeout=1.0)

    def run_command(self, command):
        """runs the specified command which is a string containing code"""
        print('executing', command)
        msg_id = self.__client.execute(command, store_history=False, allow_stdin=False)
        self.__get_reply(msg_id, 'execute_reply')
        self.send_command_executed()

    def __get_prompts(self):
        try:
            prompts = self.__exec("import sys", dict(ps1="sys.ps1", ps2="sys.ps2"))
            try:
                cur_ps1 = prompts.content.user_expressions.ps1.data['text/plain']
                cur_ps2 = prompts.content.user_expressions.ps2.data['text/plain']
            except KeyboardInterrupt:
                self.exit_process()
            except Exception:
                pass
            else:
                if cur_ps1 != self.__cur_ps1 or cur_ps2 != self.__cur_ps2:
                    self.__cur_ps1 = cur_ps1
                    self.__cur_ps2 = cur_ps2
                    self.send_prompt(cur_ps1, cur_ps2, allow_multiple_statements=True)
        except:
            traceback.print_exc()
            input()


    def __exec(self, command, silent, vars):
        msg_id = self.__client.execute(
            command,
            store_history=False,
            allow_stdin=False,
            silent=silent,
            user_expressions=vars,
        )
        msg = self.__get_reply(msg_id, 'execute_reply')
        return msg

    def execute_file_ex(self, filetype, filename, args):
        """executes the given filename as a 'script', 'module' or 'process'."""
        if filetype == 'script':
            pass
        elif filetype == 'module':
            pass
        elif filetype == 'process':
            pass

    def interrupt_main(self):
        """aborts the current running command"""
        #raise NotImplementedError
        pass

    def exit_process(self):
        """exits the REPL process"""
        pass

    def get_members(self, expression):
        """returns a tuple of the type name, instance members, and type members"""
        raise NotImplementedError

    def get_signatures(self, expression):
        """returns doc, args, vargs, varkw, defaults."""
        raise NotImplementedError

    def set_current_module(self, module):
        """sets the module which code executes against"""
        raise NotImplementedError

    def set_current_thread_and_frame(self, thread_id, frame_id, frame_kind):
        """sets the current thread and frame which code will execute against"""
        raise NotImplementedError

    def get_module_names(self):
        """returns a list of module names"""
        raise NotImplementedError

    def flush(self):
        """flushes the stdout/stderr buffers"""
        if not self.__client:
            return

        while True:
            try:
                m = Message(self.__client.iopub_channel.get_msg(block=True, timeout=0.1))
            except KeyboardInterrupt:
                self.exit_process()
                break
            except Empty:
                break
            else:
                if m.msg_type == 'status':
                    continue
                self.write_stdout(str(m) + '\n')

if __name__ == '__main__':
    b = JupyterClientBackend()
    b.execution_loop()
