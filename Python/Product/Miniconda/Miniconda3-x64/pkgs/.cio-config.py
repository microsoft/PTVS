import os
import sys
import shutil
import tempfile
from subprocess import Popen, PIPE
from os.path import abspath, dirname, isfile, join


ENV = os.environ
ENV['PREFIX'] = sys.prefix
if sys.platform == 'win32':
    ENV['PATH'] = r'%s\Scripts;%s' % (sys.prefix, os.getenv('PATH'))
else:
    ENV['PATH'] = '%s/bin:%s' % (sys.prefix, os.getenv('PATH'))


def parse(path):
    with open(path) as fi:
        data = '\n' + fi.read().replace('\r', '')
    for sec in data.split('\n#@'):
        sec = sec.strip()
        if not sec:
            continue
        parts = sec.split('\n', 1)
        if len(parts) == 1:
            parts.append('')
        assert len(parts) == 2, parts
        yield tuple(parts)


def process(path):
    with open(path, 'rb') as fi:
        header = fi.read(2)
        if header != b'#@':
            print("Warning: ignoring '%s' (unexpected header '%s')" %
                  (path, header))
            return
    for cmd, dat in parse(path):
        cmd = cmd.replace('$PREFIX', sys.prefix)
        if dat:
            tmp_dir = tempfile.mkdtemp()
            filepath = join(tmp_dir, 'config')
            with open(filepath, 'w') as fo:
                fo.write(dat + '\n')
            cmd = cmd.replace('$FILE', filepath)
        # We require shell so that builtins like 'md' can be used
        # See https://goo.gl/kCkJU2 for why stdout and sterr are required
        p = Popen(cmd, env=ENV, stdin=PIPE, stdout=PIPE,
                  stderr=PIPE, shell=True)
        p.communicate(input=(dat + '\n').encode('utf-8'))
        if dat:
            shutil.rmtree(tmp_dir)


def usage():
    sys.exit("""\
Usage: %s INSTALLER_PATH

Executes the .aic files next to the Miniconda or Anaconda installer.
INSTALLER_PATH is the path to the installer itself.
""" % sys.argv[0])


def main():
    if len(sys.argv) != 2:
        usage()
    if sys.argv[1] == '--help':
        usage()

    installer_path = abspath(sys.argv[1])
    if not isfile(installer_path):
        print("Warning: no such file: %s" % installer_path)
        return

    dir_path = dirname(installer_path)
    for fn in os.listdir(dir_path):
        path = join(dir_path, fn)
        if fn.endswith('.aic') and isfile(path):
            process(path)


if __name__ == '__main__':
    main()
