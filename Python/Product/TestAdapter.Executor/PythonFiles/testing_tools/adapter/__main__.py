# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from __future__ import absolute_import

import argparse
import sys
import unittest
#import ptvsd

from . import pytest, report
from .errors import UnsupportedToolError, UnsupportedCommandError


# 5678 is the default attach port in the VS Code debug configurations
#print("Waiting for debugger attach")
#ptvsd.enable_attach(address=('localhost', 5678), redirect_output=True)
#ptvsd.wait_for_attach()
#breakpoint()

import inspect
import types
def unittest_discover(pytestargs=None, hidestdio=False,
             _pytest_main=None, _plugin=None, **_ignored):
    
    suites = unittest.defaultTestLoader.discover(pytestargs[0], pytestargs[1])
    
    root={}

    for suite in suites._tests:
        for cls in suite._tests:
            try:
                for test in cls._tests:
                    #print(test.id())
                    parts = test.id().split('.')
                    error_case, error_message = None, None
                    
                    parts_copy = parts[:]
                    while parts_copy:
                        try:
                            module_name = '.'.join(parts_copy)
                            module = __import__(module_name)
                            break
                        except ImportError:
                            next_attribute = parts_copy.pop()
                            
                    parts = parts[1:]
         
                    setattr(test, 'module', module)
                    filename = inspect.getsourcefile(module)
                    setattr(test, 'source', filename)

                    obj = module
                    for part in parts:
                        try:
                            parent, obj = obj, getattr(obj, part)
                        except AttributeError as e:
                            pass

                    if isinstance(obj, types.FunctionType):
                        _, lineno = inspect.getsourcelines(obj)
                        setattr(test, 'lineno', lineno)
                        #print(lineno)

            except:
                pass
    return (
            {},
            suites,
            )

def add_unittest_subparser(cmd, name, parent):
    """Add a new subparser to the given parent and add args to it."""
    parser = parent.add_parser(name)
    if cmd == 'discover':
        # For now we don't have any tool-specific CLI options to add.
        pass
    else:
        raise UnsupportedCommandError(cmd)
    return parser


TOOLS = {
    'pytest': {
        '_add_subparser': pytest.add_cli_subparser,
        'discover': pytest.discover,
        },
    'unittest': {
        '_add_subparser': add_unittest_subparser,
        'discover': unittest_discover,
        },
    }
REPORTERS = {
    'pytest': {
        'discover': report.report_discovered,
        },
    'unittest': {
        'discover': report.report_unittest_discovered
        }
    }



def parse_args(
        argv=sys.argv[1:],
        prog=sys.argv[0],
        ):
    """
    Return the subcommand & tool to run, along with its args.

    This defines the standard CLI for the different testing frameworks.
    """
    parser = argparse.ArgumentParser(
            description='Run Python testing operations.',
            prog=prog,
            )
    cmdsubs = parser.add_subparsers(dest='cmd')

    # Add "run" and "debug" subcommands when ready.
    for cmdname in ['discover']:
        sub = cmdsubs.add_parser(cmdname)
        subsubs = sub.add_subparsers(dest='tool')
        for toolname in sorted(TOOLS):
            try:
                add_subparser = TOOLS[toolname]['_add_subparser']
            except KeyError:
                continue
            subsub = add_subparser(cmdname, toolname, subsubs)
            if cmdname == 'discover':
                subsub.add_argument('--simple', action='store_true')
                subsub.add_argument('--no-hide-stdio', dest='hidestdio',
                                    action='store_false')
                subsub.add_argument('--pretty', action='store_true')

    # Parse the args!
    if '--' in argv:
        seppos = argv.index('--')
        toolargs = argv[seppos + 1:]
        argv = argv[:seppos]
    else:
        toolargs = []
    args = parser.parse_args(argv)
    ns = vars(args)

    cmd = ns.pop('cmd')
    if not cmd:
        parser.error('missing command')

    tool = ns.pop('tool')
    if not tool:
        parser.error('missing tool')

    return tool, cmd, ns, toolargs


def main(toolname, cmdname, subargs, toolargs,
         _tools=TOOLS, _reporters=REPORTERS):
    try:
        tool = _tools[toolname]
    except KeyError:
        raise UnsupportedToolError(toolname)

    try:
        run = tool[cmdname]
        report_result = _reporters[toolname][cmdname]
    except KeyError:
        raise UnsupportedCommandError(cmdname)

    parents, result = run(toolargs, **subargs)
    report_result(result, parents,
                  **subargs
                  )


if __name__ == '__main__':
    tool, cmd, subargs, toolargs = parse_args()
    main(tool, cmd, subargs, toolargs)
