# Copyright (c) 2008-2011 by Enthought, Inc.
# All rights reserved.
"""
The menu will be installed to both Gnome and KDE desktops if they are
available.
Note that the information required is sufficient to install application
menus on systems that follow the format of the Desktop Entry Specification
by freedesktop.org.  See:
    http://freedesktop.org/Standards/desktop-entry-spec
"""
import re
import os
import shutil
import sys
import time
import xml.etree.ElementTree as ET
from os.path import abspath, dirname, exists, expanduser, isdir, isfile, join

from utils import rm_rf, get_executable
from freedesktop import make_desktop_entry, make_directory_entry


# datadir: contains the desktop and directory entries
# confdir: contains the XML menu files
sys_menu_file = '/etc/xdg/menus/applications.menu'
if os.getuid() == 0:
    mode = 'system'
    datadir = '/usr/share'
    confdir = '/etc/xdg'
else:
    mode = 'user'
    datadir = os.environ.get('XDG_DATA_HOME',
                             abspath(expanduser('~/.local/share')))
    confdir = os.environ.get('XDG_CONFIG_HOME',
                             abspath(expanduser('~/.config')))

appdir = join(datadir, 'applications')
menu_file = join(confdir, 'menus/applications.menu')


def indent(elem, level=0):
    """
    adds whitespace to the tree, so that it results in a pretty printed tree
    """
    XMLindentation = "    " # 4 spaces, just like in Python!
    i = "\n" + level * XMLindentation
    if len(elem):
        if not elem.text or not elem.text.strip():
            elem.text = i + XMLindentation
        for e in elem:
            indent(e, level+1)
            if not e.tail or not e.tail.strip():
                e.tail = i + XMLindentation
        if not e.tail or not e.tail.strip():
            e.tail = i
    else:
        if level and (not elem.tail or not elem.tail.strip()):
            elem.tail = i


def add_child(parent, tag, text=None):
    """
    Add a child element of specified tag type to parent.
    The new child element is returned.
    """
    elem = ET.SubElement(parent, tag)
    if text is not None:
        elem.text = text
    return elem


def is_valid_menu_file():
    try:
        root = ET.parse(menu_file).getroot()
        assert root is not None and root.tag == 'Menu'
        return True
    except:
        return False


def write_menu_file(tree):
    indent(tree.getroot())
    fo = open(menu_file, 'w')
    fo.write("""\
<!DOCTYPE Menu PUBLIC '-//freedesktop//DTD Menu 1.0//EN'
  'http://standards.freedesktop.org/menu-spec/menu-1.0.dtd'>
""")
    tree.write(fo)
    fo.write('\n')
    fo.close()


def ensure_menu_file():
    # ensure any existing version is a file
    if exists(menu_file) and not isfile(menu_file):
        rm_rf(menu_file)

    # ensure any existing file is actually a menu file
    if isfile(menu_file):
        # make a backup of the menu file to be edited
        cur_time = time.strftime('%Y-%m-%d_%Hh%Mm%S')
        backup_menu_file = "%s.%s" % (menu_file, cur_time)
        shutil.copyfile(menu_file, backup_menu_file)

        if not is_valid_menu_file():
            os.remove(menu_file)

    # create a new menu file if one doesn't yet exist
    if not isfile(menu_file):
        fo = open(menu_file, 'w')
        if mode == 'user':
            merge = '<MergeFile type="parent">%s</MergeFile>' % sys_menu_file
        else:
            merge = ''
        fo.write("<Menu><Name>Applications</Name>%s</Menu>\n" % merge)
        fo.close()


class Menu(object):

    def __init__(self, name, prefix, env_name, mode=None):
        self.name = name
        self.name_ = name + '_'
        self.entry_fn = '%s.directory' % self.name
        self.entry_path = join(datadir, 'desktop-directories', self.entry_fn)
        self.prefix = prefix
        self.env_name = env_name

    def create(self):
        self._create_dirs()
        self._create_directory_entry()
        if is_valid_menu_file() and self._has_this_menu():
            return
        ensure_menu_file()
        self._add_this_menu()

    def remove(self):
        rm_rf(self.entry_path)
        for fn in os.listdir(appdir):
            if fn.startswith(self.name_):
                # found one shortcut, so don't remove the name from menu
                return
        self._remove_this_menu()

    def _remove_this_menu(self):
        tree = ET.parse(menu_file)
        root = tree.getroot()
        for elt in root.findall('Menu'):
            if elt.find('Name').text == self.name:
                root.remove(elt)
        write_menu_file(tree)

    def _has_this_menu(self):
        root = ET.parse(menu_file).getroot()
        return any(e.text == self.name for e in root.findall('Menu/Name'))

    def _add_this_menu(self):
        tree = ET.parse(menu_file)
        root = tree.getroot()
        menu_elt = add_child(root, 'Menu')
        add_child(menu_elt, 'Name', self.name)
        add_child(menu_elt, 'Directory', self.entry_fn)
        inc_elt = add_child(menu_elt, 'Include')
        add_child(inc_elt, 'Category', self.name)
        write_menu_file(tree)

    def _create_directory_entry(self):
        # Create the menu resources.  Note that the .directory files all go
        # in the same directory.
        d = dict(name=self.name, path=self.entry_path)
        try:
            import custom_tools
            icon_path = join(dirname(custom_tools.__file__), 'menu.ico')
            if isfile(icon_path):
                d['icon'] = icon_path
        except ImportError:
            pass
        make_directory_entry(d)

    def _create_dirs(self):
        # Ensure the three directories we're going to write menu and shortcut
        # resources to all exist.
        for dir_path in [dirname(menu_file),
                         dirname(self.entry_path),
                         appdir]:
            if not isdir(dir_path):
                os.makedirs(dir_path)


class ShortCut(object):

    fn_pat = re.compile(r'[\w.-]+$')

    def __init__(self, menu, shortcut, env_setup_cmd):
        # note that this is the path WITHOUT extension
        fn = menu.name_ + shortcut['id']
        assert self.fn_pat.match(fn)
        self.path = join(appdir, fn)
        shortcut['categories'] = menu.name
        self.shortcut = shortcut
        for var_name in ('name', 'cmd'):
            if var_name in shortcut:
                setattr(self, var_name, shortcut[var_name])

        self.prefix = menu.prefix if menu.prefix is not None else sys.prefix
        self.env_name = menu.env_name
        self.env_setup_cmd = env_setup_cmd


    def create(self):
        self._install_desktop_entry('gnome')
        self._install_desktop_entry('kde')

    def remove(self):
        for ext in ('.desktop', 'KDE.desktop'):
            path = self.path + ext
            rm_rf(path)

    def _install_desktop_entry(self, tp):
        # Handle the special placeholders in the specified command.  For a
        # filebrowser request, we simply used the passed filebrowser.  But
        # for a webbrowser request, we invoke the Python standard lib's
        # webbrowser script so we can force the url(s) to open in new tabs.
        spec = self.shortcut.copy()
        spec['tp'] = tp

        path = self.path
        if tp == 'gnome':
            filebrowser = 'gnome-open'
            path += '.desktop'
        elif tp == 'kde':
            filebrowser = 'kfmclient openURL'
            path += 'KDE.desktop'

        cmd = self.cmd
        if cmd[0] == '{{FILEBROWSER}}':
            cmd[0] = filebrowser
        elif cmd[0] == '{{WEBBROWSER}}':
            import webbrowser
            executable = get_executable(self.prefix)
            cmd[0:1] = [executable, webbrowser.__file__, '-t']

        spec['cmd'] = cmd
        spec['path'] = path

        # create the shortcuts
        make_desktop_entry(spec)


if __name__ == '__main__':
    rm_rf(menu_file)
    Menu('Foo').create()
    Menu('Bar').create()
    Menu('Foo').remove()
    Menu('Foo').remove()
