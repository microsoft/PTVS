# (c) Anaconda, Inc. / https://anaconda.com
# All Rights Reserved
# This file is under the BSD license
#
# Helper script for adding and removing entries in the
# Windows system path from the NSIS installer.

__all__ = ['remove_from_system_path', 'add_to_system_path', 'broadcast_environment_settings_change', 'get_previous_install_locations']

import sys
import os, ctypes
import re
from os import path
from ctypes import wintypes
if sys.version_info[0] >= 3:
    import winreg as reg
else:
    import _winreg as reg

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

HWND_BROADCAST = 0xffff
WM_SETTINGCHANGE = 0x001A
SMTO_ABORTIFHUNG = 0x0002
SendMessageTimeout = ctypes.windll.user32.SendMessageTimeoutW
SendMessageTimeout.restype = None #wintypes.LRESULT
SendMessageTimeout.argtypes = [wintypes.HWND, wintypes.UINT, wintypes.WPARAM,
            wintypes.LPCWSTR, wintypes.UINT, wintypes.UINT, ctypes.POINTER(wintypes.DWORD)]

def sz_expand(value, value_type):
    if value_type == reg.REG_EXPAND_SZ:
        return reg.ExpandEnvironmentStrings(value)
    else:
        return value


def remove_from_system_path(pathname, allusers=True, path_env_var='PATH'):
    """Removes all entries from the path which match the value in 'pathname'

       You must call broadcast_environment_settings_change() after you are finished
       manipulating the environment with this and other functions.

       For example,
         # Remove Anaconda from PATH
         remove_from_system_path(r'C:\Anaconda')
         broadcast_environment_settings_change()
    """
    pathname = path.normcase(path.normpath(pathname))

    envkeys = [(reg.HKEY_CURRENT_USER, r'Environment')]
    if allusers:
        envkeys.append((reg.HKEY_LOCAL_MACHINE,
            r'SYSTEM\CurrentControlSet\Control\Session Manager\Environment'))
    for root, keyname in envkeys:
        key = reg.OpenKey(root, keyname, 0,
                reg.KEY_QUERY_VALUE|reg.KEY_SET_VALUE)
        reg_value = None
        try:
            reg_value = reg.QueryValueEx(key, path_env_var)
        except WindowsError:
            # This will happen if we're a non-admin install and the user has
            # no PATH variable.
            reg.CloseKey(key)
            continue

        try:
            any_change = False
            results = []
            for v in reg_value[0].split(os.pathsep):
                vexp = sz_expand(v, reg_value[1])
                # Check if the expanded path matches the
                # requested path in a normalized way
                if path.normcase(path.normpath(vexp)) == pathname:
                    any_change = True
                else:
                    # Append the original unexpanded version to the results
                    results.append(v)

            modified_path = os.pathsep.join(results)
            if any_change:
                reg.SetValueEx(key, path_env_var, 0, reg_value[1], modified_path)
        except:
            # If there's an error (e.g. when there is no PATH for the current
            # user), continue on to try the next root/keyname pair
            reg.CloseKey(key)


def add_to_system_path(paths, allusers=True, path_env_var='PATH'):
    """Adds the requested paths to the system PATH variable.

       You must call broadcast_environment_settings_change() after you are finished
       manipulating the environment with this and other functions.

    """
    # Make sure it's a list
    if not issubclass(type(paths), list):
        paths = [paths]

    # Ensure all the paths are valid before we start messing with the
    # registry.
    new_paths = None
    for p in paths:
        p = path.abspath(p)
        if new_paths:
            new_paths = new_paths + os.pathsep + p
        else:
            new_paths = p

    if allusers:
        # All Users
        root, keyname = (reg.HKEY_LOCAL_MACHINE,
            r'SYSTEM\CurrentControlSet\Control\Session Manager\Environment')
    else:
        # Just Me
        root, keyname = (reg.HKEY_CURRENT_USER, r'Environment')

    key = reg.OpenKey(root, keyname, 0,
            reg.KEY_QUERY_VALUE|reg.KEY_SET_VALUE)

    reg_type = None
    reg_value = None
    try:
        try:
            reg_value = reg.QueryValueEx(key, path_env_var)
        except WindowsError:
            # This will happen if we're a non-admin install and the user has
            # no PATH variable; in which case, we can write our new paths
            # directly.
            reg_type = reg.REG_EXPAND_SZ
            final_value = new_paths
        else:
            # Put to the front of PATH irrespective of allusers. The old
            # behaviour was asking for trouble and did not, contrary to what
            # this comment used to say, mirror what happens on *nix.
            reg_type = reg_value[1]
            final_value = new_paths + os.pathsep + reg_value[0]
        # Replace coincident ';' with a single ';'
        final_value = re.sub(r'([\;])+', r'\1', final_value)
        # Remove trailing ';'
        final_value = re.sub(r'\;$', '', final_value)
        # Remove any '"', they are not needed and break conda.
        final_value = final_value.replace('"', '')
        # Warn about directories that do not exist.
        directories = final_value.split(';')
        for directory in directories:
            if not '%' in directory and not os.path.exists(directory):
                out("WARNING: Old PATH entry '%s' does not exist\n" % (directory))
        reg.SetValueEx(key, path_env_var, 0, reg_type, final_value)

    finally:
        reg.CloseKey(key)


def _reg_query_sub_keys(handle, key, keylist = []):
    reghandle = reg.OpenKey(handle, key, 0, reg.KEY_READ)
    try:
        i = 0
        while True:
           subkey = reg.EnumKey(reghandle, i)
           i += 1
           _reg_query_sub_keys(handle, key + subkey + "\\", keylist)
    except WindowsError as ex:
           if ex.winerror == 259:
               keylist.append(key)
    finally:
        reg.CloseKey(reghandle)


def get_previous_install_prefixes(pyversion, arch, allusers=True):
    """Returns a list of prefixes for all old installations of this arch so that
       they can be removed from PATH if present. Note, it would be preferable to
       uninstall them properly instead.
    """
    if allusers:
        # All Users
        key, subkey = (reg.HKEY_LOCAL_MACHINE, r'SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\')
    else:
        # Just Me
        key, subkey = (reg.HKEY_CURRENT_USER, r'SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\')

    keylist = []
    # We ignore pyversion and instead look for any *conda installations.
    regex = re.compile('Python \S+ \(\S+conda[0-9]+ \S+ '+arch+'\)')
    _reg_query_sub_keys(key, subkey, keylist)
    results = []
    for uninstsubkey in keylist:
        final_part = os.path.basename(uninstsubkey.rstrip('\\'))
        if regex.match(final_part):
            try:
                with reg.OpenKeyEx(key, uninstsubkey, 0,
                                 reg.KEY_QUERY_VALUE) as keyhandle:
                    reg_value = reg.QueryValueEx(keyhandle, 'UninstallString')
                    results.append(os.path.dirname(re.sub(r'^"|"$', '', reg_value[0])))
            except:
                pass
    return results


def broadcast_environment_settings_change():
    """Broadcasts to the system indicating that master environment variables have changed.

    This must be called after using the other functions in this module to
    manipulate environment variables.
    """
    SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, 0, u'Environment',
                SMTO_ABORTIFHUNG, 5000, ctypes.pointer(wintypes.DWORD()))
