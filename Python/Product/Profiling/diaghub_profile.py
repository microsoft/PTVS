# Copyright (c) Microsoft Corporation.
# Licensed under the Apache License, Version 2.0.

import json
import os
import runpy
import subprocess
import sys


_CHILD_MARKER = "PTVS_DIAGHUB_PROFILE_CHILD"
_TARGET_ARGUMENTS = "PTVS_DIAGHUB_TARGET_ARGUMENTS"


def _load_legacy_collector():
    import ctypes

    collector_root = os.getenv("DIAGHUB_INSTR_COLLECTOR_ROOT")
    runtime_name = os.getenv("DIAGHUB_INSTR_RUNTIME_NAME")
    if not collector_root or not runtime_name:
        raise RuntimeError(
            "Python profiling must be launched from the Visual Studio "
            "Performance Profiler."
        )

    if sys.winver.endswith("-32"):
        architecture = "x86"
    elif sys.winver.endswith("-arm64"):
        architecture = "arm64"
    else:
        architecture = "amd64"

    collector_path = os.path.join(collector_root, architecture, runtime_name)
    collector = ctypes.WinDLL(collector_path)
    collector.ChildAttach.argtypes = []
    collector.ChildAttach.restype = ctypes.c_int
    if not collector.ChildAttach():
        raise RuntimeError("Failed to attach Python to the profiling session.")
    return collector


def _is_supported_version(version):
    return (3, 9) <= tuple(version[:2]) <= (3, 14)


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
            "Visual Studio Python profiling supports Python 3.9 through 3.14.",
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

    package_root = os.path.dirname(os.path.abspath(__file__))
    is_legacy = sys.version_info[:2] <= (3, 11)
    if is_legacy:
        package_root = os.path.join(package_root, "etwtrace_legacy")
    sys.path.insert(0, package_root)

    try:
        # etwtrace 0.1b8 expects Visual Studio to load and attach the collector.
        # Keep this reference alive while the extension uses the collector.
        collector = _load_legacy_collector() if is_legacy else None
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

    if not hasattr(tracer, "_data"):
        # etwtrace 0.1b8 only initializes this field for its test collector.
        tracer._data = []
    tracer.ignore(os.path.abspath(__file__))
    # Keep profiling and the compatibility collector alive until interpreter
    # shutdown. Worker threads may retain native profile callbacks after the
    # top-level script returns.
    tracer._collector_keepalive = collector
    tracer.enable()
    _run_target(arguments)
    return 0


if __name__ == "__main__":
    sys.exit(main())
