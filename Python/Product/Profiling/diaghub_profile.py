# Copyright (c) Microsoft Corporation.
# Licensed under the Apache License, Version 2.0.

import os
import runpy
import subprocess
import sys


_CHILD_MARKER = "PTVS_DIAGHUB_PROFILE_CHILD"


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


def _run_profiled_child():
    environment = os.environ.copy()
    environment["DIAGHUB_PARENT_INITIALIZED"] = "1"
    environment[_CHILD_MARKER] = "1"
    command = [sys.executable, os.path.abspath(__file__), *sys.argv[1:]]
    return subprocess.call(command, env=environment)


def _run_target(arguments):
    sys.argv[:] = arguments
    if sys.argv[0] == "-m" and len(sys.argv) >= 2:
        runpy.run_module(sys.argv.pop(1), run_name="__main__")
    else:
        runpy.run_path(sys.argv[0], run_name="__main__")


def main():
    if not ((3, 9) <= sys.version_info[:2] <= (3, 14)):
        print(
            "Visual Studio Python profiling supports Python 3.9 through 3.14.",
            file=sys.stderr,
        )
        return 2

    if len(sys.argv) < 2:
        print("A Python script is required for profiling.", file=sys.stderr)
        return 2

    if os.getenv(_CHILD_MARKER) != "1":
        return _run_profiled_child()

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
    except ImportError as exc:
        print(
            "Visual Studio does not include an etwtrace binary for "
            f"Python {sys.version_info[0]}.{sys.version_info[1]} "
            f"({sys.winver}).",
            file=sys.stderr,
        )
        print(str(exc), file=sys.stderr)
        return 2

    arguments = sys.argv[1:]
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
