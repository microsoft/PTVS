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

from __future__ import with_statement, absolute_import

__author__ = "Microsoft Corporation <ptvshelp@microsoft.com>"
__version__ = "3.2.0.0"

# This module MUST NOT import threading in global scope. This is because in a direct (non-ptvsd)
# attach scenario, it is loaded on the injected debugger attach thread, and if threading module
# hasn't been loaded already, it will assume that the thread on which it is being loaded is the
# main thread. This will cause issues when the thread goes away after attach completes.

import json
import os.path
import itertools
import socket
import sys
import traceback
from ptvsd.util import to_bytes

_TRACE = None

def _str_or_call(m):
    try:
        callable = m.__call__
    except AttributeError:
        return str(m)
    else:
        return str(callable())

def _trace(*msg):
    if _TRACE:
        _TRACE(''.join(_str_or_call(m) for m in msg) + '\n')


SKIP_TB_PREFIXES = [
    os.path.normcase(os.path.dirname(os.path.abspath(__file__)))
]

class InvalidHeaderError(Exception): pass

class InvalidContentError(Exception): pass

class SocketIO(object):
    def __init__(self, *args, **kwargs):
        super(SocketIO, self).__init__(*args, **kwargs)
        self.__buffer = to_bytes('')
        self.__port = kwargs.get('port')
        self.__socket = kwargs.get('socket')
        self.__own_socket = kwargs.get('own_socket', True)
        self.__logfile = kwargs.get('logfile')
        if self.__socket is None and self.__port is None:
            raise ValueError("A 'port' or a 'socket' must be passed to SocketIO initializer as a keyword argument.")
        if self.__socket is None:
             self.__socket = socket.create_connection(('127.0.0.1', self.__port))

    def _send(self, **payload):
        content = json.dumps(payload).encode('utf-8')
        headers = ('Content-Length: %d\r\n\r\n' % (len(content), )).encode('ascii')
        if self.__logfile is not None:
            self.__logfile.write(content)
            self.__logfile.write('\n'.encode('utf-8'))
            self.__logfile.flush()
        self.__socket.send(headers)
        self.__socket.send(content)

    def _buffered_read_line_as_ascii(self):
        '''
        Reads bytes until it encounters newline chars, and returns the bytes
        ascii decoded, newline chars are excluded from the return value.
        Blocks until: newline chars are read OR socket is closed.
        '''
        newline = '\r\n'.encode('ascii')
        while newline not in self.__buffer:
            temp = self.__socket.recv(1024)
            if not temp:
                break
            self.__buffer += temp

        if not self.__buffer:
            return None

        try:
            index = self.__buffer.index(newline)
        except ValueError:
            raise InvalidHeaderError('Header line not terminated')

        line = self.__buffer[:index]
        self.__buffer = self.__buffer[index+len(newline):]
        return line.decode('ascii', 'replace')

    def _buffered_read_as_utf8(self, length):
        while len(self.__buffer) < length:
            temp = self.__socket.recv(1024)
            if not temp:
                break
            self.__buffer += temp

        if len(self.__buffer) < length:
            raise InvalidContentError('Expected to read {0} bytes of content, but only read {1} bytes.'.format(length, len(self.__buffer)))

        content = self.__buffer[:length]
        self.__buffer = self.__buffer[length:]
        return content.decode('utf-8', 'replace')

    def _wait_for_message(self):
        # base protocol defined at https://github.com/Microsoft/language-server-protocol/blob/master/protocol.md#base-protocol
        # read all headers, ascii encoded separated by '\r\n'
        # end of headers is indicated by an empty line
        headers = {}
        line = self._buffered_read_line_as_ascii()
        while line:
            parts = line.split(':')
            if len(parts) == 2:
                headers[parts[0]] = parts[1]
            else:
                raise InvalidHeaderError("Malformed header, expected 'name: value'\n{0}".format(line))
            line = self._buffered_read_line_as_ascii()

        # end of stream
        if not line and not headers:
            return

        # validate headers
        try:
            length_text = headers['Content-Length']
            try:
                length = int(length_text)
            except ValueError:
                raise InvalidHeaderError("Invalid Content-Length: {0}".format(length_text))
        except NameError:
            raise InvalidHeaderError('Content-Length not specified in headers')
        except KeyError:
            raise InvalidHeaderError('Content-Length not specified in headers')

        if length < 0 or length > 2147483647:
            raise InvalidHeaderError("Invalid Content-Length: {0}".format(length))

        # read content, utf-8 encoded
        content = self._buffered_read_as_utf8(length)
        try:
            msg = json.loads(content)
            self._receive_message(msg)
        except ValueError:
            raise InvalidContentError('Error deserializing message content.')
        except json.decoder.JSONDecodeError:
            raise InvalidContentError('Error deserializing message content.')

    def _close(self):
        if self.__own_socket:
            self.__socket.close()

'''
class StandardIO(object):
    def __init__(self, stdin, stdout, *args, **kwargs):
        super(StandardIO, self).__init__(*args, **kwargs)
        try:
            self.__stdin = stdin.buffer
            self.__stdout = stdout.buffer
        except AttributeError:
            self.__stdin = stdin
            self.__stdout = stdout

    def _send(self, **payload):
        data = json.dumps(payload).encode('utf-8') + NEWLINE_BYTES
        self.__stdout.write(data)
        self.__stdout.flush()

    def _wait_for_message(self):
        msg = json.loads(self.__stdin.readline().decode('utf-8', 'replace').rstrip())
        self._receive_message(msg)

    def _close(self):
        pass
'''

class IpcChannel(object):
    def __init__(self, *args, **kwargs):
        # This class is meant to be last in the list of base classes
        # Don't call super because object's __init__ doesn't take arguments
        try:
            import thread
        except:
            import _thread as thread
        self.__seq = itertools.count()
        self.__exit = False
        self.__lock = thread.allocate_lock()
        self.__message = []
        self.__exit_on_unknown_command = True

    def close(self):
        self._close()

    def send_event(self, name, **kwargs):
        with self.__lock:
            self._send(
                type='event',
                seq=next(self.__seq),
                event=name,
                body=kwargs,
            )

    def send_response(self, request, success=True, message=None, **kwargs):
        with self.__lock:
            self._send(
                type='response',
                seq=next(self.__seq),
                request_seq=int(request.get('seq', 0)),
                success=success,
                command=request.get('command', ''),
                message=message or '',
                body=kwargs,
            )

    def set_exit(self):
        self.__exit = True

    def process_messages(self):
        while True:
            if self.process_one_message():
                return

    def process_one_message(self):
        try:
            msg = self.__message.pop(0)
        except IndexError:
            self._wait_for_message()
            try:
                msg = self.__message.pop(0)
            except IndexError:
                return self.__exit

        _trace('Received ', msg)

        try:
            if msg['type'] == 'request':
                self.on_request(msg)
            elif msg['type'] == 'response':
                self.on_response(msg)
            elif msg['type'] == 'event':
                self.on_event(msg)
            else:
                self.on_invalid_request(msg, {})
        except AssertionError:
            raise
        except Exception:
            _trace('Error ', traceback.format_exc)
            traceback.print_exc()

        _trace('self.__exit is ', self.__exit)
        return self.__exit

    def on_request(self, request):
        assert request.get('type', '') == 'request', "Only handle 'request' messages in on_request"

        cmd = request.get('command', '')
        args = request.get('arguments', {})
        target = getattr(self, 'on_' + cmd, self.on_invalid_request)
        try:
            _trace('Calling ', repr(target))
            target(request, args)
        except AssertionError:
            raise
        except Exception:
            self.send_response(
                request,
                success=False,
                message=traceback.format_exc(),
            )

    def on_response(self, msg):
        # this class is only used for server side only for now
        raise NotImplementedError

    def on_event(self, msg):
        # this class is only used for server side only for now
        raise NotImplementedError

    def on_invalid_request(self, request, args):
        self.send_response(request, success=False, message='Unknown command')
        if self.__exit_on_unknown_command:
            self.__exit = True

    def _receive_message(self, message):
        with self.__lock:
            self.__message.append(message)
