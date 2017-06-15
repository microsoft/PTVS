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

__all__ = ['enable_attach', 'wait_for_attach', 'break_into_debugger', 'settrace', 'is_attached', 'AttachAlreadyEnabledError']

DEFAULT_PORT = 5678

# Importing attach_server directly causes many additional modules to load.
# In particular, it will indirectly load threading, which breaks things in
# non-ptvsd attach scenario due to it being loaded on injected thread.
# To avoid loading the extra stuff, defer loading the actual implementation
# from attach_server, and provide wrapper functions that load it when called.

def _attach_server():
    import ptvsd.attach_server
    return ptvsd.attach_server

class AttachAlreadyEnabledError(Exception):
    """`ptvsd.enable_attach` has already been called in this process."""

def enable_attach(secret, address=('0.0.0.0', DEFAULT_PORT), certfile=None, keyfile=None, redirect_output=True):
    """Enables Visual Studio to attach to this process remotely to debug Python
    code.

    Parameters
    ----------
    secret : str
        Used to validate the clients - only those clients providing the valid
        secret will be allowed to connect to this server. On client side, the
        secret is prepended to the Qualifier string, separated from the
        hostname by ``'@'``, e.g.: ``'secret@myhost.cloudapp.net:5678'``. If
        secret is ``None``, there's no validation, and any client can connect
        freely.
    address : (str, int), optional 
        Specifies the interface and port on which the debugging server should
        listen for TCP connections. It is in the same format as used for
        regular sockets of the `socket.AF_INET` family, i.e. a tuple of
        ``(hostname, port)``. On client side, the server is identified by the
        Qualifier string in the usual ``'hostname:port'`` format, e.g.:
        ``'myhost.cloudapp.net:5678'``. Default is ``('0.0.0.0', 5678)``.
    certfile : str, optional
        Used to enable SSL. If not specified, or if set to ``None``, the
        connection between this program and the debugger will be unsecure,
        and can be intercepted on the wire. If specified, the meaning of this
        parameter is the same as for `ssl.wrap_socket`. 
    keyfile : str, optional
        Used together with `certfile` when SSL is enabled. Its meaning is the
        same as for ``ssl.wrap_socket``.
    redirect_output : bool, optional
        Specifies whether any output (on both `stdout` and `stderr`) produced
        by this program should be sent to the debugger. Default is ``True``.

    Notes
    -----
    This function returns immediately after setting up the debugging server,
    and does not block program execution. If you need to block until debugger
    is attached, call `ptvsd.wait_for_attach`. The debugger can be detached
    and re-attached multiple times after `enable_attach` is called.

    This function can only be called once during the lifetime of the process. 
    On the second call, `AttachAlreadyEnabledError` is raised. In circumstances
    where the caller does not control how many times the function will be
    called (e.g. when a script with a single call is run more than once by
    a hosting app or framework), the call should be wrapped in ``try..except``.

    Only the thread on which this function is called, and any threads that are
    created after it returns, will be visible in the debugger once it is
    attached. Any threads that are already running before this function is
    called will not be visible.
    """
    return _attach_server().enable_attach(secret, address, certfile, keyfile, redirect_output)

# Alias for convenience of users of pydevd
settrace = enable_attach

def wait_for_attach(timeout=None):
    """If PTVS remote debugger is attached, returns immediately. Otherwise,
    blocks until a remote debugger attaches to this process, or until the
    optional timeout occurs.

    Parameters
    ----------
    timeout : float, optional
        The timeout for the operation in seconds (or fractions thereof).
    """
    return _attach_server().wait_for_attach(timeout)

def break_into_debugger():
    """If PTVS debugger is attached, pauses execution of all threads,
    and breaks into the debugger with current thread as active.
    """
    return _attach_server().break_into_debugger()

def is_attached():
    """Returns ``True`` if debugger is attached, ``False`` otherwise."""
    return _attach_server().is_attached()
