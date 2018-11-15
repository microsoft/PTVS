import sys
from os.path import join

import menuinst


def main():
    from optparse import OptionParser

    p = OptionParser(
        usage="usage: %prog [options] MENU_FILE",
        description="install a menu item")

    p.add_option('-p', '--prefix',
                 action="store",
                 default=sys.prefix)

    p.add_option('--remove',
                 action="store_true")

    p.add_option('--version',
                 action="store_true")

    opts, args = p.parse_args()

    if opts.version:
        sys.stdout.write("menuinst: %s\n" % menuinst.__version__)
        return

    for arg in args:
        menuinst.install(join(opts.prefix, arg), opts.remove, opts.prefix)


if __name__ == '__main__':
    main()
