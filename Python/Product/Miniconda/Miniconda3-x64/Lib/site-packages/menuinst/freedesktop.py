# Copyright (c) 2008-2011 by Enthought, Inc.
# All rights reserved.


def make_desktop_entry(d):
    """
    Create a desktop entry that conforms to the format of the Desktop Entry
    Specification by freedesktop.org.  See:
            http://freedesktop.org/Standards/desktop-entry-spec
    These should work for both KDE and Gnome2

    An entry is a .desktop file that includes the application's type,
    executable, name, etc.   It will be placed in the location specified within
    the passed dict.
    """
    assert d['path'].endswith('.desktop')

    # default values
    d.setdefault('comment', '')
    d.setdefault('icon', '')

    # Format the command to a single string.
    if isinstance(d['cmd'], list):
        d['cmd'] = ' '.join(d['cmd'])

    assert isinstance(d['terminal'], bool)
    d['terminal'] = {False: 'false', True: 'true'}[d['terminal']]

    fo = open(d['path'], "w")
    fo.write("""\
[Desktop Entry]
Type=Application
Encoding=UTF-8
Name=%(name)s
Comment=%(comment)s
Exec=%(cmd)s
Terminal=%(terminal)s
Icon=%(icon)s
Categories=%(categories)s
""" % d)

    if d['tp'] == 'kde':
        fo.write('OnlyShowIn=KDE\n')
    else:
        fo.write('NotShowIn=KDE\n')

    fo.close()


def make_directory_entry(d):
    """
    Create a directory entry that conforms to the format of the Desktop Entry
    Specification by freedesktop.org.  See:
            http://freedesktop.org/Standards/desktop-entry-spec
    These should work for both KDE and Gnome2

    An entry is a .directory file that includes the display name, icon, etc.
    It will be placed in the location specified within the passed dict.  The
    filename can be explicitly specified, but if not provided, will default to
    an escaped version of the name.
    """
    assert d['path'].endswith('.directory')

    # default values
    d.setdefault('comment', '')
    d.setdefault('icon', '')

    fo = open(d['path'], "w")
    fo.write("""\
[Desktop Entry]
Type=Directory
Encoding=UTF-8
Name=%(name)s
Comment=%(comment)s
Icon=%(icon)s
""" % d)
    fo.close()
