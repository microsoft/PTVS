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

from __future__ import with_statement

__author__ = "Microsoft Corporation <ptvshelp@microsoft.com>"
__version__ = "3.0.0.0"

import io
import os.path
import sys
import json
import time
import unittest
import socket
import traceback
from types import CodeType, FunctionType
try:
    import thread
except:
    import _thread as thread

try:
    from unittest import TextTestResult
    _IS_OLD_UNITTEST = False
except:
    from unittest import _TextTestResult as TextTestResult
    _IS_OLD_UNITTEST = True

class _TestOutput(object):
    """file like object which redirects output to the repl window."""
    errors = 'strict'

    def __init__(self, old_out, is_stdout):
        self.is_stdout = is_stdout
        self.old_out = old_out
        if sys.version >= '3.' and hasattr(old_out, 'buffer'):
            self.buffer = _TestOutputBuffer(old_out.buffer, is_stdout)

    def flush(self):
        if self.old_out:
            self.old_out.flush()
    
    def writelines(self, lines):
        for line in lines:
            self.write(line)
    
    @property
    def encoding(self):
        return 'utf8'

    def write(self, value):
        _channel.send_event('stdout' if self.is_stdout else 'stderr', content=value)
        if self.old_out:
            self.old_out.write(value)
    
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

    def __getattr__(self, name):
        return getattr(self.old_out, name)

class _TestOutputBuffer(object):
    def __init__(self, old_buffer, is_stdout):
        self.buffer = old_buffer
        self.is_stdout = is_stdout

    def write(self, data):
        _channel.send_event('stdout' if self.is_stdout else 'stderr', content=data)
        self.buffer.write(data)

    def flush(self): 
        self.buffer.flush()

    def truncate(self, pos = None):
        return self.buffer.truncate(pos)

    def tell(self):
        return self.buffer.tell()

    def seek(self, pos, whence = 0):
        return self.buffer.seek(pos, whence)

class _IpcChannel(object):
    def __init__(self, socket):
        self.socket = socket
        self.seq = 0
        self.lock = thread.allocate_lock()

    def close(self):
        self.socket.close()

    def send_event(self, name, **args):
        with self.lock:
            body = {'type': 'event', 'seq': self.seq, 'event':name, 'body':args}
            self.seq += 1
            content = json.dumps(body).encode('utf-8')
            headers = ('Content-Length: %d\r\n\r\n' % (len(content), )).encode('ascii')
            self.socket.send(headers)
            self.socket.send(content)

_channel = None


class VsTestResult(TextTestResult):
    _start_time = None
    _result = None

    def startTest(self, test):
        super(VsTestResult, self).startTest(test)
        self._start_time = time.time()
        if _channel is not None:
            _channel.send_event(
                name='start', 
                test = test.test_id
            )

    def stopTest(self, test):
        # stopTest is called after tearDown on all Python versions
        # so it is the right time to send the result back to VS
        # (sending it in the addX methods is too early on Python <= 3.1)
        super(VsTestResult, self).stopTest(test)
        if _channel is not None and self._result is not None:
            _channel.send_event(**self._result)

    def addError(self, test, err):
        super(VsTestResult, self).addError(test, err)
        self._setResult(test, 'failed', err)

    def addFailure(self, test, err):
        super(VsTestResult, self).addFailure(test, err)
        self._setResult(test, 'failed', err)

    def addSuccess(self, test):
        super(VsTestResult, self).addSuccess(test)
        self._setResult(test, 'passed')

    def addSkip(self, test, reason):
        super(VsTestResult, self).addSkip(test, reason)
        self._setResult(test, 'skipped')

    def addExpectedFailure(self, test, err):
        super(VsTestResult, self).addExpectedFailure(test, err)
        self._setResult(test, 'failed', err)

    def addUnexpectedSuccess(self, test):
        super(VsTestResult, self).addUnexpectedSuccess(test)
        self._setResult(test, 'passed')

    def _setResult(self, test, outcome, trace = None):
        # If a user runs the unit tests without debugging and then attaches to them using the legacy debugger (TCP or PID)
        # Then the user will be able to debug this script. 
        # After attaching the debugger, this script must add itself to the DONT_DEBUG list so it's not debugged. 
        # Since this script doesn't know when the legacy debugger has been imported and attached, it's doing a check after every test
        # This code will be removed when the legacy debugger is removed. 
        # For more information, see https://github.com/microsoft/PTVS/pull/5447
        if ("ptvsd" in sys.modules
            and sys.modules["ptvsd"].__version__.startswith('3.') 
            and os.path.normcase(__file__) not in sys.modules["ptvsd"].debugger.DONT_DEBUG):
            sys.modules["ptvsd"].debugger.DEBUG_ENTRYPOINTS.add(sys.modules["ptvsd"].debugger.get_code(main))
            sys.modules["ptvsd"].debugger.DONT_DEBUG.append(os.path.normcase(__file__))

        tb = None
        message = None
        duration = time.time() - self._start_time if self._start_time else 0
        if trace is not None:
            traceback.print_exception(*trace)
            tb = _get_traceback(trace)
            message = str(trace[1])
        self._result = dict(
            name = 'result', 
            outcome = outcome,
            traceback = tb,
            message = message,
            durationInSecs = duration,
            test = test.test_id
        )

def _get_traceback(trace):
    def norm_module(file_path):
        return os.path.splitext(os.path.normcase(file_path))[0] + '.py'

    def is_framework_frame(f):
        return is_excluded_module_path(norm_module(f[0]))

    if _IS_OLD_UNITTEST:
        def is_excluded_module_path(file_path):
            # unittest is a module, not a package on 2.5, 2.6, 3.0, 3.1
            return file_path == norm_module(unittest.__file__) or file_path == norm_module(__file__)

    else:
        def is_excluded_module_path(file_path):
            for lib_path in unittest.__path__:
                # if it's in unit test package or it's this module
                if file_path.startswith(os.path.normcase(lib_path)) or file_path == norm_module(__file__):
                    return True
            return False

    all = traceback.extract_tb(trace[2])
    filtered = [f for f in reversed(all) if not is_framework_frame(f)]

    # stack trace parser needs the Python version, it parses the user's
    # code to create fully qualified function names
    lang_ver = '{0}.{1}'.format(sys.version_info[0], sys.version_info[1])
    tb = ''.join(traceback.format_list(filtered))
    return lang_ver + '\n' + tb

def main():
    import os
    from optparse import OptionParser
    global _channel

    parser = OptionParser(prog = 'visualstudio_py_testlauncher', usage = 'Usage: %prog [<option>] <test names>... ')
    parser.add_option('-s', '--secret', metavar='<secret>', help='restrict server to only allow clients that specify <secret> when connecting (legacy debugger only)')
    parser.add_option('-p', '--port', type='int', metavar='<port>', help='listen for debugger connections on <port>')
    parser.add_option('-d', '--debugger-search-path', type='str', metavar='<debugger_path>', help='Path to the debugger directory')
    parser.add_option('-x', '--mixed-mode', action='store_true', help='wait for mixed-mode debugger to attach')
    parser.add_option('-t', '--test', type='str', dest='tests', action='append', help='specifies a test to run')
    parser.add_option('-c', '--coverage', type='str', help='enable code coverage and specify filename')
    parser.add_option('-r', '--result-port', type='int', help='connect to port on localhost and send test results')
    parser.add_option('--test-list', metavar='<file>', type='str', help='read tests from this file')
    parser.add_option('--dry-run', action='store_true', help='prints a list of tests without executing them')
    (opts, _) = parser.parse_args()
    
    sys.path[0] = os.getcwd()
    if opts.debugger_search_path:
        sys.path.insert(0, opts.debugger_search_path)

    if opts.result_port:
        _channel = _IpcChannel(socket.create_connection(('127.0.0.1', opts.result_port)))
        sys.stdout = _TestOutput(sys.stdout, is_stdout = True)
        sys.stderr = _TestOutput(sys.stderr, is_stdout = False)

    if opts.secret and opts.port:
        from ptvsd.debugger import DONT_DEBUG, DEBUG_ENTRYPOINTS, get_code
        from ptvsd import DEFAULT_PORT, enable_attach, wait_for_attach

        DONT_DEBUG.append(os.path.normcase(__file__))
        DEBUG_ENTRYPOINTS.add(get_code(main))

        enable_attach(opts.secret, ('127.0.0.1', getattr(opts, 'port', DEFAULT_PORT)), redirect_output = True)
        wait_for_attach()
    elif opts.port:   
        from ptvsd import enable_attach, wait_for_attach
        
        enable_attach(('127.0.0.1', getattr(opts, 'port', 5678)))
        wait_for_attach()
    elif opts.mixed_mode:
        # For mixed-mode attach, there's no ptvsd and hence no wait_for_attach(), 
        # so we have to use Win32 API in a loop to do the same thing.
        from time import sleep
        from ctypes import windll, c_char
        while True:
            if windll.kernel32.IsDebuggerPresent() != 0:
                break
            sleep(0.1)
        try:
            debugger_helper = windll['Microsoft.PythonTools.Debugger.Helper.x86.dll']
        except WindowsError:
            debugger_helper = windll['Microsoft.PythonTools.Debugger.Helper.x64.dll']
        isTracing = c_char.in_dll(debugger_helper, "isTracing")
        while True:
            if isTracing.value != 0:
                break
            sleep(0.1)

    if opts.debugger_search_path:
        del sys.path[0]

    all_tests = list(opts.tests or [])
    if opts.test_list:
        with io.open(opts.test_list, 'r', encoding='utf-8') as test_list:
            all_tests.extend(t.strip() for t in test_list)

    if opts.dry_run:
        if _channel:
            for test in all_tests:
                print(test)
                _channel.send_event(
                    name='start', 
                    test = test
                )
                _channel.send_event(
                    name='result', 
                    outcome='passed',
                    test = test
                )
        else:
            for test in all_tests:
                print(test)
        sys.exit(0)

    cov = None
    try:
        if opts.coverage:
            try:
                import coverage
                cov = coverage.coverage(opts.coverage)
                cov.load()
                cov.start()
            except:
                pass

        tests = []
        for test in all_tests:
            if not test:
                continue
            try:
                for loaded_test in unittest.defaultTestLoader.loadTestsFromName(test):
                    # Starting with Python 3.5, rather than letting any import error
                    # exception propagate out of loadTestsFromName, unittest catches it and
                    # creates instance(s) of unittest.loader._FailedTest.
                    # Those have an unexpected test.id(), ex: 'unittest.loader._FailedTest.test1'
                    # Store the test id passed in as an additional attribute and
                    # VsTestResult will use that instead of test.id().
                    loaded_test.test_id = test
                    tests.append(loaded_test)
            except Exception:
                trace = sys.exc_info()

                traceback.print_exception(*trace)
                tb = _get_traceback(trace)
                message = str(trace[1])

                if _channel is not None:
                    _channel.send_event(
                        name='start', 
                        test = test
                    )
                    _channel.send_event(
                        name='result', 
                        outcome='failed',
                        traceback = tb,
                        message = message,
                        test = test
                    )
        if _IS_OLD_UNITTEST:
            def _makeResult(self):
                return VsTestResult(self.stream, self.descriptions, self.verbosity)

            unittest.TextTestRunner._makeResult = _makeResult

            runner = unittest.TextTestRunner(verbosity=0)
        else:
            runner = unittest.TextTestRunner(verbosity=0, resultclass=VsTestResult)
        
        result = runner.run(unittest.defaultTestLoader.suiteClass(tests))

        sys.exit(not result.wasSuccessful())
    finally:
        if cov is not None:
            cov.stop()
            cov.save()
            cov.xml_report(outfile = opts.coverage + '.xml', omit=__file__)
        if _channel is not None:
            _channel.send_event(
                name='done'
            )
            _channel.close()

if __name__ == '__main__':
    main()
