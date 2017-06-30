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

__author__ = "Microsoft Corporation <ptvshelp@microsoft.com>"
__version__ = "3.2.0.0"

import atexit
import getpass
import os
import os.path
import platform
import socket
import struct
import sys
import threading
import traceback
try:
    import thread
except ImportError:
    import _thread as thread
try:
    import ssl
except ImportError:
    ssl = None

import ptvsd
import ptvsd.debugger as vspd
import ptvsd.repl as vspr
import ptvsd.ipcjson as vsipc


# The server (i.e. the Python app) waits on a TCP port provided. Whenever
# anything connects to that port, it immediately sends a legacyRemoteConnected
# event that includes the protocol name and version.

# It then waits for the client to send requests, the first of which will be
# legacyRemoteDebuggerAuthenticate, which includes the protocol name and version,
# and the string secret which can be an empty string to designate the lack of a
# specified secret.
#
# If the secret does not match the one expected by the server, it responds
# accepted=False, and then closes the connection. Otherwise, the server responds
# accepted=True, and continues waiting for requests.
#
# If the client does not send a legacyRemoteDebuggerAuthenticate request, then
# any other request will fail and the connection will be closed.
#
# The following commands are recognized:
#
# 'legacyRemoteDebuggerInfo'
#   Report information about the process. The server responds with the following:
#       - Process ID
#       - Executable name
#       - User name
#       - Implementation name
#   and then immediately closes connection. Note, all string fields can be
#   empty or null strings.
#
# 'legacyRemoteDebuggerAttach'
#   Attach debugger to the process. If successful, the server responds with
#   accepted=True, along with process ID, and the Python language version that
#   the server is running represented by three integers - major, minor, micro.
#   From there on the socket is assumed to be using the normal PTVS debugging protocol.
#   If attaching was not successful (which can happen if some other debugger is
#   already attached), the server responds with accepted=False and closes the connection. 

PTVSDBG_VER = 8 # must be kept in sync with DebuggerProtocolVersion in PythonRemoteProcess.cs
PTVSDBG = 'PTVSDBG'

_attach_enabled = False
_attached = threading.Event()
vspd.DONT_DEBUG.append(os.path.normcase(__file__))


def enable_attach(secret, address=('0.0.0.0', ptvsd.DEFAULT_PORT), certfile=None, keyfile=None, redirect_output=True):
    if not ssl and (certfile or keyfile):
        raise ValueError('could not import the ssl module - SSL is not supported on this version of Python')

    if sys.platform == 'cli':
        # Check that IronPython was launched with -X:Frames and -X:Tracing, since we can't register our trace
        # func on the thread that calls enable_attach otherwise
        import clr
        x_tracing = clr.GetCurrentRuntime().GetLanguageByExtension('py').Options.Tracing
        x_frames = clr.GetCurrentRuntime().GetLanguageByExtension('py').Options.Frames
        if not x_tracing or not x_frames:
            raise RuntimeError('IronPython must be started with -X:Tracing and -X:Frames options to support PTVS remote debugging.')

    global _attach_enabled
    if _attach_enabled:
        raise ptvsd.AttachAlreadyEnabledError('ptvsd.enable_attach() has already been called in this process.')
    _attach_enabled = True

    atexit.register(vspd.detach_process_and_notify_debugger)

    server = socket.socket(socket.AF_INET, socket.SOCK_STREAM, socket.IPPROTO_TCP)
    server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    server.bind(address)
    server.listen(1)
    def server_thread_func():
        while True:
            client = None
            connection = None
            try:
                client, addr = server.accept()
                if certfile:
                    client = ssl.wrap_socket(client, server_side = True, ssl_version = ssl.PROTOCOL_TLSv1, certfile = certfile, keyfile = keyfile)

                connection = AttachLoop(client, secret, redirect_output, None)
                connection.send_event(
                    name='legacyRemoteConnected',
                    debuggerName=PTVSDBG,
                    debuggerProtocolVersion=PTVSDBG_VER,
                )
                connection.process_messages()
            except (socket.error, OSError):
                pass
            finally:
                if connection:
                    connection.close()

    server_thread = threading.Thread(target = server_thread_func)
    server_thread.setDaemon(True)
    server_thread.start()

    frames = []
    f = sys._getframe()
    while True:
        f = f.f_back
        if f is None:
            break
        frames.append(f)
    frames.reverse()
    cur_thread = vspd.new_thread()
    for f in frames:
        cur_thread.push_frame(f)
    def replace_trace_func():
        for f in frames:
            f.f_trace = cur_thread.trace_func
    replace_trace_func()
    sys.settrace(cur_thread.trace_func)
    vspd.intercept_threads(for_attach = True)


def wait_for_attach(timeout = None):
    if vspd.DETACHED:
        _attached.clear()
        _attached.wait(timeout)


def break_into_debugger():
    if not vspd.DETACHED:
        vspd.SEND_BREAK_COMPLETE = thread.get_ident()
        vspd.mark_all_threads_for_break()


def is_attached():
    return not vspd.DETACHED


class AttachLoop(vsipc.SocketIO, vsipc.IpcChannel):
    def __init__(self, socket, secret, redirect_output, logfile):
        super(AttachLoop, self).__init__(socket=socket, own_socket=False, logfile=logfile)
        self.__secret = secret
        self.__redirect_output = redirect_output
        self.__owned_socket = socket
        self.__waiting_for_authentication = True

    def close(self):
        if self.__owned_socket:
            self.__owned_socket.close()

    def on_legacyRemoteDebuggerAuthenticate(self, request, args):
        debugger_name = args['debuggerName']
        protocol_version = args['debuggerProtocolVersion']
        client_secret = args['clientSecret']

        supported = debugger_name == PTVSDBG and protocol_version == PTVSDBG_VER
        authenticated = self.__secret is None or client_secret == self.__secret
        accepted = supported and authenticated
        self.send_response(request, accepted=accepted)

        self.__waiting_for_authentication = False
        if not accepted:
            self.set_exit()

    def on_legacyRemoteDebuggerInfo(self, request, args):
        if self.__waiting_for_authentication:
            self.send_response(
                request,
                success=False,
                message='legacyRemoteDebuggerAuthenticate request must be sent first.',
            )
            self.set_exit()
            return

        try:
            try:
                pid = os.getpid()
            except AttributeError:
                pid = 0

            exe = sys.executable or ''

            try:
                username = getpass.getuser()
            except AttributeError:
                username = ''

            try:
                impl = platform.python_implementation()
            except AttributeError:
                try:
                    impl = sys.implementation.name
                except AttributeError:
                    impl = 'Python'

            major, minor, micro, release_level, serial = sys.version_info

            os_and_arch = platform.system()
            if os_and_arch == "":
                os_and_arch = sys.platform
            try:
                if sys.maxsize > 2**32:
                    os_and_arch += ' 64-bit'
                else:
                    os_and_arch += ' 32-bit'
            except AttributeError:
                pass

            version = '%s %s.%s.%s (%s)' % (impl, major, minor, micro, os_and_arch)

            self.send_response(
                request,
                processId=pid,
                executable=exe,
                user=username,
                pythonVersion=version,
            )
        finally:
            self.set_exit()

    def on_legacyRemoteDebuggerAttach(self, request, args):
        if self.__waiting_for_authentication:
            self.send_response(
                request,
                success=False,
                message='legacyRemoteDebuggerAuthenticate request must be sent first.',
            )
            self.set_exit()
            return

        try:
            debug_options = vspd.parse_debug_options(args['debugOptions'])
            if self.__redirect_output:
                debug_options.add('RedirectOutput')

            if vspd.DETACHED:
                try:
                    pid = os.getpid()
                except AttributeError:
                    pid = 0

                major, minor, micro, release_level, serial = sys.version_info

                self.send_response(
                    request,
                    accepted=True,
                    processId=pid,
                    pythonMajor=major,
                    pythonMinor=minor,
                    pythonMicro=micro,
                )

                vspd.attach_process_from_socket(self.__owned_socket, debug_options, report = True)
                vspd.mark_all_threads_for_break(vspd.STEPPING_ATTACH_BREAK)

                _attached.set()

                # Prevent from closing the socket, it will be used by debugger
                self.__owned_socket = None
            else:
                self.send_response(
                    request,
                    accepted=False,
                )
        finally:
            self.set_exit()
