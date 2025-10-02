# safe_attach_loader.py (minimal safe attach loader)
# Simplified listen debugpy bootstrap for PTVS safe attach.
from __future__ import print_function
import os, sys, time

HOST = os.environ.get("PTVS_DEBUG_HOST", "127.0.0.1")
PORT_FILE = os.environ.get("PTVS_DEBUG_PORT_FILE")  # optional in fixed-port mode
VERBOSE = os.environ.get("PTVS_SAFE_ATTACH_LOADER_VERBOSE", "1") != "0"
PAUSE = os.environ.get("PTVS_DEBUG_PAUSE") == "1" or os.environ.get("PTVS_DEBUG_BREAK") == "1" or os.environ.get("PTVS_WAIT_FOR_CLIENT") == "1"
REQUESTED_PORT = os.environ.get("PTVS_DEBUG_PORT")
try:
    REQ_PORT_INT = int(REQUESTED_PORT) if REQUESTED_PORT else 0
    if REQ_PORT_INT < 0 or REQ_PORT_INT > 65535: REQ_PORT_INT = 0
except Exception:
    REQ_PORT_INT = 0

_t0 = time.time()

def _log(msg):
    if VERBOSE:
        try: sys.stderr.write(f"[PTVS][safe_attach_loader] {time.time()-_t0:.03f} {msg}\n")
        except Exception: pass

try:
    base = os.path.dirname(__file__)
    if base and base not in sys.path: sys.path.insert(0, base)
    dbgpkg = os.path.join(base, 'debugpy')
    if os.path.isdir(dbgpkg) and dbgpkg not in sys.path: sys.path.insert(0, dbgpkg)
except Exception:
    pass

_log(f"start host={HOST} reqPort={REQ_PORT_INT} pause={PAUSE}")

try:
    import debugpy  # type: ignore
except Exception as exc:
    _log(f"import debugpy failed {exc.__class__.__name__}")
    raise SystemExit(0)

try:
    listen_port = REQ_PORT_INT if REQ_PORT_INT != 0 else 0
    debugpy.listen((HOST, listen_port))
except Exception as exc:
    _log(f"listen failed {exc.__class__.__name__}")
    raise SystemExit(0)

# Resolve actual bound port
try:
    addr = getattr(debugpy, 'address', None)
    actual_port = addr[1] if isinstance(addr, tuple) and len(addr) == 2 else REQ_PORT_INT
except Exception:
    actual_port = REQ_PORT_INT

_log(f"listening port={actual_port}")

if PORT_FILE and actual_port and actual_port > 0:
    try:
        with open(PORT_FILE, 'w') as f: f.write(str(actual_port))
        _log(f"port file written {PORT_FILE}")
    except Exception as exc:
        _log(f"port file write failed {exc.__class__.__name__}")

if PAUSE:
    try:
        _log("waiting for client")
        debugpy.wait_for_client()
        _log("client attached; breakpoint")
        debugpy.breakpoint()
    except Exception as exc:
        _log(f"pause sequence failed {exc.__class__.__name__}")

_log("done")
