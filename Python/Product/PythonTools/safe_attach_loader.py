# safe_attach_loader.py
# Bootstrap script for managed safe attach (PEP 768) in PTVS.
# Executed inside target process after writing path into remote_support script buffer.
# Responsibilities:
#  * Determine host / port from environment (or allocate free port if 0 / empty)
#  * Start debugpy listener (importing debugpy lazily)
#  * Optionally wait for a client (wait_for_client) based on env flag
#  * Emit chosen port to an optional sidecar file so the host (VS) can discover it
#  * (Optional) Emit diagnostic logging when enabled
#
# Environment variables (set by host before writing path):
#  PTVS_DEBUG_HOST               Host interface to bind (default: 127.0.0.1)
#  PTVS_DEBUG_PORT               Port to bind. If "0" or unset, allocate a free TCP port.
#  PTVS_WAIT_FOR_CLIENT          If "1" (default) block in debugpy.wait_for_client()
#  PTVS_DEBUG_SESSION            Optional session identifier (GUID string)
#  PTVS_DEBUG_PORT_FILE          Optional filesystem path; if provided, write the selected port (as text) after listener bound
#  PTVS_SAFE_ATTACH_LOADER_VERBOSE If "1" emit diagnostic messages (stderr or log file)
#  PTVS_DEBUG_LOG_FILE           Optional path to append log lines (used only if verbose enabled)
#
# This loader intentionally avoids importing threading preemptively; debugpy will import what it needs.

from __future__ import print_function
import os, socket, sys, time

HOST = os.environ.get("PTVS_DEBUG_HOST", "127.0.0.1")
PORT_ENV = os.environ.get("PTVS_DEBUG_PORT", "0").strip()
WAIT = os.environ.get("PTVS_WAIT_FOR_CLIENT", "1") == "1"
PORT_FILE = os.environ.get("PTVS_DEBUG_PORT_FILE")
SESSION = os.environ.get("PTVS_DEBUG_SESSION")
# Verbose ON by default now; set PTVS_SAFE_ATTACH_LOADER_VERBOSE=0 to disable
VERBOSE = os.environ.get("PTVS_SAFE_ATTACH_LOADER_VERBOSE", "1") != "0"
LOG_FILE = os.environ.get("PTVS_DEBUG_LOG_FILE") if VERBOSE else None
# Break control: explicit flag OR implicit when WAIT is true (behaves like legacy Break Immediately)
BREAK_FLAG = os.environ.get("PTVS_DEBUG_BREAK") == "1" or WAIT

_ts0 = time.time()

def _log(msg):
    if not VERBOSE:
        return
    line = "[PTVS][safe_attach_loader] %.03f %s" % (time.time() - _ts0, msg)
    try:
        if LOG_FILE:
            with open(LOG_FILE, "a") as f:
                f.write(line + "\n")
        else:
            sys.stderr.write(line + "\n")
    except Exception:
        pass

_log("starting host=%s portEnv=%s wait=%s break=%s session=%s" % (HOST, PORT_ENV, WAIT, BREAK_FLAG, SESSION))

# Acquire a concrete port (pre-bind) if caller requested dynamic.
# Doing our own ephemeral selection ensures we can publish the exact port before client connects.
def _allocate_port(host):
    s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    s.bind((host, 0))
    addr, port = s.getsockname()
    s.close()
    return port

try:
    if PORT_ENV and PORT_ENV.isdigit():
        desired_port = int(PORT_ENV, 10)
    else:
        desired_port = 0
except Exception:
    desired_port = 0

if desired_port == 0:
    try:
        desired_port = _allocate_port(HOST)
        _log("allocated dynamic port=%d" % desired_port)
    except Exception:
        _log("failed to allocate dynamic port; aborting")
        sys.exit(0)

try:
    import debugpy  # Prefer already-installed debugpy
    _log("imported debugpy version=%s" % getattr(debugpy, '__version__', 'unknown'))
except Exception as exc:  # Fallback to legacy ptvsd if debugpy not present
    _log("debugpy import failed (%s); trying ptvsd fallback" % exc.__class__.__name__)
    try:
        import ptvsd as debugpy  # type: ignore
        _log("imported ptvsd as debugpy fallback")
    except Exception as exc2:
        _log("ptvsd fallback failed (%s); exiting" % exc2.__class__.__name__)
        sys.exit(0)

try:
    debugpy.listen((HOST, desired_port))
    _log("listener started port=%d" % desired_port)
except Exception as exc:
    _log("listen failed (%s)" % exc.__class__.__name__)
    if PORT_ENV == "0":
        try:
            desired_port = _allocate_port(HOST)
            debugpy.listen((HOST, desired_port))
            _log("retry listener started port=%d" % desired_port)
        except Exception as exc2:
            _log("retry failed (%s); exiting" % exc2.__class__.__name__)
            sys.exit(0)
    else:
        sys.exit(0)

# Emit selected port if sidecar file requested.
if PORT_FILE:
    try:
        with open(PORT_FILE, "w") as f:
            f.write(str(desired_port))
        _log("wrote port file=%s" % PORT_FILE)
    except Exception as exc:
        _log("failed writing port file (%s)" % exc.__class__.__name__)

# Block until a client connects if requested.
if WAIT:
    try:
        _log("waiting for client")
        debugpy.wait_for_client()
        _log("client attached")
    except Exception as exc:
        _log("wait_for_client failed (%s); continuing" % exc.__class__.__name__)

# Trigger initial breakpoint if requested.
if BREAK_FLAG:
    try:
        _log("triggering initial breakpoint")
        debugpy.breakpoint()
    except Exception as exc:
        _log("initial breakpoint failed (%s)" % exc.__class__.__name__)

_log("completed")
