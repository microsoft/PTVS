from __future__ import print_function

import os
import re
import subprocess
import sys

SCRIPT_NAME = 'script{sys.version_info[0]}{sys.version_info[1]}.rsp'

TEMPLATE = r'''python {sys.version_info[0]}.{sys.version_info[1]} {sys.executable}
logs logs
module * {sys.prefix}\Lib\site-packages\{module}\**\*.py

enqueue *
analyze
'''

VERSION = '.'.join(str(i) for i in sys.version_info[:2])

if len(sys.argv) < 3:
    print('Usage:', sys.argv[0], '<path to AnalysisMemoryTester.exe> <output path>', file=sys.stderr)
    sys.exit(1)

if sys.version_info[0] == 2:
    import threading
    def wait(p, timeout):
        t = threading.Timer(timeout, p.kill)
        t.daemon = True
        t.start()
        p.wait()
        t.cancel()
else:
    def wait(p, timeout):
        p.wait(timeout)

TOOL = os.path.abspath(sys.argv[1])
OUTDIR = os.path.abspath(sys.argv[2] if len(sys.argv) > 2 else '.')

for module in os.listdir(os.path.join(sys.prefix, 'Lib', 'site-packages')):
    if module == '__pycache__':
        continue
    if not re.match(r'[a-z0-9_]+$', module, re.I):
        continue
    outdir = os.path.join(OUTDIR, module)
    try:
        os.makedirs(outdir)
    except OSError:
        if not os.path.isdir(outdir):
            raise
    script = os.path.join(outdir, SCRIPT_NAME.format(sys=sys, module=module))
    with open(script, 'w') as f:
        print(TEMPLATE.format(sys=sys, module=module), file=f)
    print("Testing", module)
    p = subprocess.Popen([TOOL, script])

    try:
        wait(p, 3600)
    except KeyboardInterrupt:
        p.kill()
        sys.exit(0)
    except:
        p.kill()
