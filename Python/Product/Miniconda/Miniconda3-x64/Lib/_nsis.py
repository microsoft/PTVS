# (c) Anaconda, Inc. / https://anaconda.com
# All Rights Reserved
# This file is under the BSD license

# Helper script which is called from within the nsis install process
# on Windows.  The fact that we put this file into the standard library
# directory is merely a convenience.  This way, functionally can easily
# be tested in an installation.

import os
import sys
import traceback
from os.path import isdir, isfile, join, exists

ROOT_PREFIX = sys.prefix

# Install an exception hook which pops up a message box.
# Ideally, exceptions will get returned to NSIS and logged there,
# etc, but this is a stopgap solution for now.
old_excepthook = sys.excepthook

def gui_excepthook(exctype, value, tb):
    try:
        import ctypes, traceback
        MB_ICONERROR = 0x00000010
        title = u'Installation Error'
        msg = u''.join(traceback.format_exception(exctype, value, tb))
        ctypes.windll.user32.MessageBoxW(0, msg, title, MB_ICONERROR)
    finally:
        # Also call the old exception hook to let it do
        # its thing too.
        old_excepthook(exctype, value, tb)
sys.excepthook = gui_excepthook

# If pythonw is being run, there may be no write function
if sys.stdout and sys.stdout.write:
    out = sys.stdout.write
    err = sys.stderr.write
else:
    import ctypes
    OutputDebugString = ctypes.windll.kernel32.OutputDebugStringW
    OutputDebugString.argtypes = [ctypes.c_wchar_p]
    def out(x):
        OutputDebugString('_nsis.py: ' + x)
    def err(x):
        OutputDebugString('_nsis.py: Error: ' + x)

def mk_menus(remove=False, prefix=None):
    try:
        import menuinst
    except (ImportError, OSError):
        return
    if prefix is None:
        prefix = sys.prefix
    menu_dir = join(prefix, 'Menu')
    if not os.path.isdir(menu_dir):
        return
    pkg_names = [s.strip() for s in sys.argv[2:]]
    for fn in os.listdir(menu_dir):
        if not fn.endswith('.json'):
            continue
        if pkg_names and fn[:-5] not in pkg_names:
            continue
        shortcut = join(menu_dir, fn)
        try:
            menuinst.install(shortcut, remove, prefix=prefix)
        except Exception as e:
            out("Failed to process %s...\n" % shortcut)
            err("Error: %s\n" % str(e))
            err("Traceback:\n%s\n" % traceback.format_exc(20))
        else:
            out("Processed %s successfully.\n" % shortcut)

def mk_dirs():
    envs_dir = join(ROOT_PREFIX, 'envs')
    if not exists(envs_dir):
        os.mkdir(envs_dir)

def get_conda_envs_from_python_api():
    try:
        from conda.cli.python_api import run_command, Commands
    except (ImportError, OSError):
        return
    from json import loads
    c_stdout, c_stderr, return_code = run_command(Commands.INFO, "--json")
    json_conda_info = loads(c_stdout)
    return json_conda_info["envs"]


get_conda_envs = get_conda_envs_from_python_api


def rm_menus():
    mk_menus(remove=True)
    try:
        import menuinst
        menuinst
    except (ImportError, OSError):
        return
    try:
        envs = get_conda_envs()
        envs = list(envs)  # make sure `envs` is iterable
    except Exception as e:
        out("Failed to get conda environments list\n")
        err("Error: %s\n" % str(e))
        err("Traceback:\n%s\n" % traceback.format_exc(20))
        return
    for env in envs:
        env = str(env)  # force `str` so that `os.path.join` doesn't fail
        mk_menus(remove=True, prefix=env)


def run_post_install():
    """
    call the post install script, if the file exists
    """
    path = join(ROOT_PREFIX, 'pkgs', 'post_install.bat')
    if not isfile(path):
        return
    env = os.environ
    env['PREFIX'] = str(ROOT_PREFIX)
    try:
        args = [env['COMSPEC'], '/c', path]
    except KeyError:
        err("Error: COMSPEC undefined\n")
        return
    import subprocess
    try:
        subprocess.check_call(args, env=env)
    except subprocess.CalledProcessError:
        err("Error: running %s failed\n" % path)


allusers = (not exists(join(ROOT_PREFIX, '.nonadmin')))
out('allusers is %s\n' % allusers)

# This must be the same as conda's binpath_from_arg() in conda/cli/activate.py
PATH_SUFFIXES = ('',
                 os.path.join('Library', 'mingw-w64', 'bin'),
                 os.path.join('Library', 'usr', 'bin'),
                 os.path.join('Library', 'bin'),
                 'Scripts')


def remove_from_path(root_prefix=None):
    from _system_path import (remove_from_system_path,
                              broadcast_environment_settings_change)

    if root_prefix is None:
        root_prefix = ROOT_PREFIX
    for path in [os.path.normpath(os.path.join(root_prefix, path_suffix))
                 for path_suffix in PATH_SUFFIXES]:
        remove_from_system_path(path, allusers)
    broadcast_environment_settings_change()


def add_to_path(pyversion, arch):
    from _system_path import (add_to_system_path,
                              get_previous_install_prefixes,
                              broadcast_environment_settings_change)

    # If a previous Anaconda install attempt to this location left remnants,
    # remove those.
    remove_from_path(ROOT_PREFIX)

    # If a previously registered Anaconda install left remnants, remove those.
    try:
        old_prefixes = get_previous_install_prefixes(pyversion, arch, allusers)
    except IOError:
        old_prefixes = []
    for prefix in old_prefixes:
        out('Removing old installation at %s from PATH (if any entries get found)\n' % (prefix))
        remove_from_path(prefix)

    # add Anaconda to the path
    add_to_system_path([os.path.normpath(os.path.join(ROOT_PREFIX, path_suffix))
                        for path_suffix in PATH_SUFFIXES], allusers)
    broadcast_environment_settings_change()


def main():
    cmd = sys.argv[1].strip()
    if cmd == 'mkmenus':
        mk_menus(remove=False)
    elif cmd == 'post_install':
        run_post_install()
    elif cmd == 'rmmenus':
        rm_menus()
    elif cmd == 'mkdirs':
        mk_dirs()
    elif cmd == 'addpath':
        # These checks are probably overkill, but could be useful
        # if I forget to update something that uses this code.
        if len(sys.argv) > 2:
            pyver = sys.argv[2]
        else:
            pyver = '%s.%s.%s' % (sys.version_info.major,
                                  sys.version_info.minor,
                                  sys.version_info.micro)
        if len(sys.argv) > 3:
            arch = sys.argv[2]
        else:
            arch = '32-bit' if tuple.__itemsize__==4 else '64-bit'
        add_to_path(pyver, arch)
    elif cmd == 'rmpath':
        remove_from_path()
    else:
        sys.exit("ERROR: did not expect %r" % cmd)


if __name__ == '__main__':
    main()
