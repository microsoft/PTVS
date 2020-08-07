# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from __future__ import absolute_import
import io
import argparse
import os
import sys

from . import report, unittest
from .errors import UnsupportedToolError, UnsupportedCommandError

def default_subparser(cmd, name, parent):
    parser = parent.add_parser(name)
    if cmd == 'discover':
        # For now we don't have any tool-specific CLI options to add.
        pass
    else:
        raise UnsupportedCommandError(cmd)
    return parser


def pytest_add_cli_subparser(cmd, name, parent):
    # Handle the case where the user hasn't installed pytest but we still want to see it appear as an option
    try:
        from . import pytest
        return pytest.add_cli_subparser(cmd, name, parent)
    except ImportError:
        return default_subparser(cmd, name, parent)


def pytest_discover(pytestargs=None, **_ignored):
    # Delay importing pytest until actually used
    from . import pytest
    return pytest.discover(pytestargs, _ignored)

TOOLS = {
    'pytest': {
        '_add_subparser': pytest_add_cli_subparser,
        'discover': pytest_discover,
        },
    'unittest': {
        '_add_subparser': unittest.add_unittest_cli_subparser,
        'discover': unittest.discover,
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
                subsub.add_argument('--test-list',  metavar='<file>', type=str, help='read tests from this file')
                subsub.add_argument('--output-file',  metavar='<file>', type=str, help='file name to use for tests found')

    # Parse the args!
    if '--' in argv:
        sep_index = argv.index('--')
        toolargs = argv[sep_index + 1:]
        argv = argv[:sep_index]
    else:
        toolargs = []
    args = parser.parse_args(argv)
    ns = vars(args)

    # Append tests pass by file test_list to toolargs
    if args.test_list and os.path.exists(args.test_list):
        with io.open(args.test_list, 'r', encoding='utf-8') as tests:
            toolargs.extend(t.strip() for t in tests)
   
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
    
    if 'output_file' in subargs:
        with io.open(subargs['output_file'], 'wt') as handle:
            def file_print(message):
                if sys.version_info > (3,):
                    handle.write(message)
                else:
                    handle.write(message.decode('utf-8'))
            
            report_result(result, 
                parents, 
                _send=file_print,
                **subargs
                )
    else:
        report_result(result, parents,
            **subargs
            )


if __name__ == '__main__':
    tool, cmd, subargs, toolargs = parse_args()
    main(tool, cmd, subargs, toolargs)
