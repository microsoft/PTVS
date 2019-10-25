# (c) Anaconda, Inc. / https://anaconda.com
# All Rights Reserved
# This file is under the BSD license

# Helper script which is called from within the nsis install process
# on Windows.  The fact that we put this file into the standard library
# directory is merely a convenience.  This way, functionally can easily
# be tested in an installation.

import os
import re
import sys
import traceback
from os.path import isfile, join, exists, basename
from os import environ
from subprocess import check_output, STDOUT, CalledProcessError

try:
    import winreg
except ImportError:
    import _winreg as winreg

ROOT_PREFIX = sys.prefix

# Install an exception hook which pops up a message box.
# Ideally, exceptions will get returned to NSIS and logged there,
# etc, but this is a stopgap solution for now.
old_excepthook = sys.excepthook


# this sucks.  It is copied from _nsis.py because it can't be a relative import.
# _nsis.py must be standalone.
def ensure_comspec_set():
    if basename(environ.get("COMSPEC", "")).lower() != "cmd.exe":
        cmd_exe = join(environ.get('SystemRoot'), 'System32', 'cmd.exe')
        if not isfile(cmd_exe):
            cmd_exe = join(environ.get('windir'), 'System32', 'cmd.exe')
        if not isfile(cmd_exe):
            print("cmd.exe could not be found. "
                     "Looked in SystemRoot and windir env vars.\n")
        else:
            environ['COMSPEC'] = cmd_exe


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


class NSISReg:
    def __init__(self, reg_path):
        self.reg_path = reg_path
        if exists(join(ROOT_PREFIX, '.nonadmin')):
            self.main_key = winreg.HKEY_CURRENT_USER
        else:
            self.main_key = winreg.HKEY_LOCAL_MACHINE

    def set(self, name, value):
        try:
            winreg.CreateKey(self.main_key, self.reg_path)
            registry_key = winreg.OpenKey(self.main_key, self.reg_path, 0,
                                           winreg.KEY_WRITE)
            winreg.SetValueEx(registry_key, name, 0, winreg.REG_SZ, value)
            winreg.CloseKey(registry_key)
            return True
        except WindowsError:
            return False

    def get(self, name):
        try:
            registry_key = winreg.OpenKey(self.main_key, self.reg_path, 0,
                                           winreg.KEY_READ)
            value, regtype = winreg.QueryValueEx(registry_key, name)
            winreg.CloseKey(registry_key)
            return value
        except WindowsError:
            return None


def mk_menus(remove=False, prefix=None, pkg_names=[]):
    try:
        import menuinst
    except (ImportError, OSError):
        return
    if prefix is None:
        prefix = sys.prefix
    menu_dir = join(prefix, 'Menu')
    if not os.path.isdir(menu_dir):
        return
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
    cmd_exe = os.path.join(os.environ['SystemRoot'], 'System32', 'cmd.exe')
    if not os.path.isfile(cmd_exe):
        cmd_exe = os.path.join(os.environ['windir'], 'System32', 'cmd.exe')
    if not os.path.isfile(cmd_exe):
        err("Error: running %s failed.  cmd.exe could not be found.  "
            "Looked in SystemRoot and windir env vars.\n" % path)
    args = [cmd_exe, '/d', '/c', path]
    import subprocess
    try:
        subprocess.check_call(args, env=env)
    except subprocess.CalledProcessError:
        err("Error: running %s failed\n" % path)


allusers = (not exists(join(ROOT_PREFIX, '.nonadmin')))
# out('allusers is %s\n' % allusers)

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


def rm_regkeys():
    cmdproc_reg_entry = NSISReg('Software\Microsoft\Command Processor')
    cmdproc_autorun_val = cmdproc_reg_entry.get('AutoRun')
    conda_hook_regex_pat = r'((\s+&\s+)?\"[^\"]*?conda[-_]hook\.bat\")'
    if join(ROOT_PREFIX, 'condabin') in (cmdproc_autorun_val or ''):
        cmdproc_autorun_newval = re.sub(conda_hook_regex_pat, '',
                cmdproc_autorun_val)
        try:
            cmdproc_reg_entry.set('AutoRun', cmdproc_autorun_newval)
        except:
            # Hey, at least we made an attempt to cleanup
            pass


def win_del(dirname):
    # check_output uses comspec as the default shell when setting the parameter `shell=True`
    ensure_comspec_set()
    out = "unknown error (exception not caught)"
    # first, remove all files
    try:
        out = check_output('DEL /F/Q/S *.* > NUL', shell=True, stderr=STDOUT, cwd=dirname)
    except CalledProcessError as e:
            # error code 5 indicates a permission error.  We ignore those, but raise for anything else
            if e.returncode != 5:
                print("Removing folder {} the fast way failed. Output was: {}".format(dirname, out))
                raise
            else:
                print("removing dir contents the fast way failed. Output was: {}".format(out))
    else:
        print("Unexpected error removing dirname {}. Uninstall was probably not successful".format(dirname))
    # next, remove folder hierarchy
    try:
        out = check_output('RD /S /Q "{}" > NUL'.format(dirname), shell=True, stderr=STDOUT)
    except CalledProcessError as e:
            # error code 5 indicates a permission error.  We ignore those, but raise for anything else
            if e.returncode != 5:
                print("Removing folder {} the fast way failed. Output was: {}".format(dirname, out))
                raise
            else:
                print("removing dir folders the fast way failed. Output was: {}".format(out))
    else:
        print("Unexpected error removing dirname {}. Uninstall was probably not successful".format(dirname))


def main():
    cmd = sys.argv[1].strip()
    if cmd == 'mkmenus':
        pkg_names = [s.strip() for s in sys.argv[2:]]
        mk_menus(remove=False, pkg_names=pkg_names)
    elif cmd == 'post_install':
        run_post_install()
    elif cmd == 'rmmenus':
        rm_menus()
    elif cmd == 'rmreg':
        rm_regkeys()
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
    elif cmd == 'del':
        assert len(sys.argv) == 3
        win_del(sys.argv[2].strip())
    else:
        sys.exit("ERROR: did not expect %r" % cmd)


if __name__ == '__main__':
    main()
