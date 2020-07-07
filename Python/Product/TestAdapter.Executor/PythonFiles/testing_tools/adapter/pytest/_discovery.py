# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from __future__ import absolute_import, print_function

import sys
import pytest

from .. import util, discovery
from ._pytest_item import parse_item

#note: this must match testlauncher.py
def patch_translate_non_printable():
    import _pytest.compat
    translate_non_printable =  getattr(_pytest.compat, "_translate_non_printable", None)

    if translate_non_printable:
        def _translate_non_printable_patched(s):
            s = translate_non_printable(s)
            s = s.replace(':', '/:')  # pytest testcase not found error and VS TestExplorer FQN parsing
            s = s.replace('.', '_')   # VS TestExplorer FQN parsing
            s = s.replace('\n', '/n') # pytest testcase not found error 
            s = s.replace('\\', '/')  # pytest testcase not found error, fixes cases (actual backslash followed by n)
            s = s.replace('\r', '/r') # pytest testcase not found error
            return s

        _pytest.compat._translate_non_printable = _translate_non_printable_patched
    else:
        print("ERROR: failed to patch pytest, _pytest.compat._translate_non_printable")

patch_translate_non_printable()

def discover(pytestargs=None, hidestdio=False,
             _pytest_main=pytest.main, _plugin=None, **_ignored):
    """Return the results of test discovery."""
    if _plugin is None:
        _plugin = TestCollector()

    pytestargs = _adjust_pytest_args(pytestargs)
    # We use this helper rather than "-pno:terminal" due to possible
    # platform-dependent issues.
    with (util.hide_stdio() if hidestdio else util.noop_cm()) as stdio:
        ec = _pytest_main(pytestargs, [_plugin])
    # See: https://docs.pytest.org/en/latest/usage.html#possible-exit-codes
    if ec == 5:
        # No tests were discovered.
        pass
    elif ec != 0:
        print(('equivalent command: {} -m pytest {}'
               ).format(sys.executable, util.shlex_unsplit(pytestargs)))
        if hidestdio:
            print(stdio.getvalue(), file=sys.stderr)
            sys.stdout.flush()
        print('pytest discovery failed (exit code {})'.format(ec))
    if not _plugin._started:
        print(('equivalent command: {} -m pytest {}'
               ).format(sys.executable, util.shlex_unsplit(pytestargs)))
        if hidestdio:
            print(stdio.getvalue(), file=sys.stderr)
            sys.stdout.flush()
        raise Exception('pytest discovery did not start')
    return (
            _plugin._tests.parents,
            list(_plugin._tests),
            )


def _adjust_pytest_args(pytestargs):
    """Return a corrected copy of the given pytest CLI args."""
    pytestargs = list(pytestargs) if pytestargs else []
    # Duplicate entries should be okay.
    pytestargs.insert(0, '--collect-only')
    # TODO: pull in code from:
    #  src/client/testing/pytest/services/discoveryService.ts
    #  src/client/testing/pytest/services/argsService.ts
    return pytestargs


class TestCollector(object):
    """This is a pytest plugin that collects the discovered tests."""

    @classmethod
    def parse_item(cls, item):
        return parse_item(item)

    def __init__(self, tests=None):
        if tests is None:
            tests = discovery.DiscoveredTests()
        self._tests = tests
        self._started = False

    # Relevant plugin hooks:
    #  https://docs.pytest.org/en/latest/reference.html#collection-hooks

    def pytest_collection_modifyitems(self, session, config, items):
        self._started = True
        self._tests.reset()
        for item in items:
            test, parents = self.parse_item(item)
            self._tests.add_test(test, parents)

    # This hook is not specified in the docs, so we also provide
    # the "modifyitems" hook just in case.
    def pytest_collection_finish(self, session):
        self._started = True
        try:
            items = session.items
        except AttributeError:
            # TODO: Is there an alternative?
            return
        self._tests.reset()
        for item in items:
            test, parents = self.parse_item(item)
            self._tests.add_test(test, parents)
