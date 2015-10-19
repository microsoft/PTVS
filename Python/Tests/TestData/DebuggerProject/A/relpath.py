import sys
def do_something():
    # Make sure that the file is actually loaded with relative path, otherwise
    # we are not testing the scenario properly.
    if sys._getframe().f_code.co_filename == 'A/relpath.py':
        print('ok')
