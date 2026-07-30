# Copyright (c) Microsoft Corporation.
# Licensed under the Apache License, Version 2.0.

import json
import os
import runpy
import subprocess
import sys


_CHILD_MARKER = "PTVS_DIAGHUB_PROFILE_CHILD"
_TARGET_ARGUMENTS = "PTVS_DIAGHUB_TARGET_ARGUMENTS"


def _is_supported_version(version):
    return (3, 12) <= tuple(version[:2]) <= (3, 14)


def _has_valid_target(arguments):
    return bool(arguments) and (arguments[0] != "-m" or len(arguments) >= 2)


def _run_profiled_child(arguments):
    environment = os.environ.copy()
    environment["DIAGHUB_PARENT_INITIALIZED"] = "1"
    environment[_CHILD_MARKER] = "1"
    environment[_TARGET_ARGUMENTS] = json.dumps(arguments)
    command = [sys.executable, os.path.abspath(__file__)]
    return subprocess.call(command, env=environment, shell=False)


def _get_profiled_arguments():
    try:
        arguments = json.loads(os.environ[_TARGET_ARGUMENTS])
    except (KeyError, TypeError, json.JSONDecodeError):
        return None
    if not isinstance(arguments, list) or not all(
        isinstance(argument, str) for argument in arguments
    ):
        return None
    return arguments


def _run_target(arguments):
    if arguments[0] == "-m" and len(arguments) >= 2:
        module_name = arguments[1]
        sys.argv[:] = [module_name, *arguments[2:]]
        sys.path[0] = os.getcwd()
        runpy.run_module(module_name, run_name="__main__", alter_sys=True)
    else:
        sys.argv[:] = arguments
        runpy.run_path(sys.argv[0], run_name="__main__")


def main():
    if not _is_supported_version(sys.version_info):
        print(
            "Visual Studio Python profiling supports Python 3.12 through 3.14.",
            file=sys.stderr,
        )
        return 2

    is_child = os.getenv(_CHILD_MARKER) == "1"
    arguments = _get_profiled_arguments() if is_child else sys.argv[1:]
    if not _has_valid_target(arguments):
        print("A Python script or module is required for profiling.", file=sys.stderr)
        return 2

    if not is_child:
        return _run_profiled_child(arguments)

    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

    try:
        from etwtrace import DiagnosticsHubTracer
        tracer = DiagnosticsHubTracer()
    except (ImportError, OSError, RuntimeError, AttributeError) as exc:
        print(
            "Visual Studio could not start Python profiling for "
            f"Python {sys.version_info[0]}.{sys.version_info[1]} "
            f"({sys.winver}).",
            file=sys.stderr,
        )
        print(str(exc), file=sys.stderr)
        return 2

    if arguments[0] != "-m":
        arguments[0] = os.path.abspath(arguments[0])
        sys.path[0] = os.path.dirname(arguments[0])

    tracer.ignore(os.path.abspath(__file__))
    tracer.enable()
    _run_target(arguments)
    return 0


if __name__ == "__main__":
    sys.exit(main())
