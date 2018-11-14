# Copyright (c) 2008-2011 by Enthought, Inc.
# Copyright (c) 2013-2015 Continuum Analytics, Inc.
# All rights reserved.

from __future__ import absolute_import
import logging
import sys
import json
from os.path import abspath, basename, exists, join

from ._version import get_versions
__version__ = get_versions()['version']
del get_versions


if sys.platform.startswith('linux'):
    from .linux import Menu, ShortCut

elif sys.platform == 'darwin':
    from .darwin import Menu, ShortCut

elif sys.platform == 'win32':
    from .win32 import Menu, ShortCut
    from .win_elevate import isUserAdmin, runAsAdmin


def _install(path, remove=False, prefix=sys.prefix, mode=None):
    if abspath(prefix) == abspath(sys.prefix):
        env_name = None
    else:
        env_name = basename(prefix)

    data = json.load(open(path))
    try:
        menu_name = data['menu_name']
    except KeyError:
        menu_name = 'Python-%d.%d' % sys.version_info[:2]

    shortcuts = data['menu_items']
    m = Menu(menu_name, prefix=prefix, env_name=env_name, mode=mode)
    if remove:
        for sc in shortcuts:
            ShortCut(m, sc).remove()
        m.remove()
    else:
        m.create()
        for sc in shortcuts:
            ShortCut(m, sc).create()


def install(path, remove=False, prefix=sys.prefix, recursing=False):
    """
    install Menu and shortcuts
    """
    # this sys.prefix is intentional.  We want to reflect the state of the root installation.
    if sys.platform == 'win32' and not exists(join(sys.prefix, '.nonadmin')):
        if isUserAdmin():
            _install(path, remove, prefix, mode='system')
        else:
            from pywintypes import error
            try:
                if not recursing:
                    retcode = runAsAdmin([join(sys.prefix, 'python'), '-c',
                                          "import menuinst; menuinst.install(%r, %r, %r, %r)" % (
                                              path, bool(remove), prefix, True)])
                else:
                    retcode = 1
            except error:
                retcode = 1

            if retcode != 0:
                logging.warn("Insufficient permissions to write menu folder.  "
                             "Falling back to user location")
                _install(path, remove, prefix, mode='user')
    else:
        _install(path, remove, prefix, mode='user')
