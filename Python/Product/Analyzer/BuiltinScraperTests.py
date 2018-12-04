# Python Tools for Visual Studio
# Copyright(c) Microsoft Corporation
# All rights reserved.
# 
# Licensed under the Apache License, Version 2.0 (the License); you may not use
# this file except in compliance with the License. You may obtain a copy of the
# License at http://www.apache.org/licenses/LICENSE-2.0
# 
# THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
# OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
# IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
# MERCHANTABILITY OR NON-INFRINGEMENT.
# 
# See the Apache Version 2.0 License for specific language governing
# permissions and limitations under the License.

__author__ = "Microsoft Corporation <ptvshelp@microsoft.com>"
__version__ = "3.0.0.0"

import re
import unittest
from pprint import pformat
from BuiltinScraper import parse_doc_str, BUILTIN, __builtins__, get_overloads_from_doc_string, TOKENS_REGEX

try:
    unicode
except NameError:
    from BuiltinScraper import unicode
import sys

class Test_BuiltinScraperTests(unittest.TestCase):
    def check_doc_str(self, doc, module_name, func_name, expected, mod=None, extra_args=[], obj_class=None):
        r = parse_doc_str(doc, module_name, mod, func_name, extra_args, obj_class)
        
        # Quick pass if everything matches
        if r == expected:
            return

        msg = 'Expected:\n%s\nActual\n%s' % (pformat(expected), pformat(r))

        self.assertEqual(len(r), len(expected), msg)

        def check_dict(e, a, indent):
            if e == a:
                return
            missing_keys = set(e.keys()) - set(a.keys())
            extra_keys = set(a.keys()) - set(e.keys())
            mismatched_keys = [k for k in set(a.keys()) & set(e.keys()) if a[k] != e[k]]
            if missing_keys:
                print('%sDid not received following keys: %s' % (indent, ', '.join(missing_keys)))
            if extra_keys:
                print('%sDid not expect following keys: %s' % (indent, ', '.join(extra_keys)))
            for k in mismatched_keys:
                if isinstance(e[k], dict) and isinstance(a[k], dict):
                    check_dict(e[k], a[k], indent + ' ')
                elif (isinstance(e[k], tuple) and isinstance(a[k], tuple) or isinstance(e[k], list) and isinstance(a[k], list)):
                    check_seq(e[k], a[k], indent + ' ')
                else:
                    print('%sExpected "%s": "%s"' % (indent, k, e[k]))
                    print('%sActual   "%s": "%s"' % (indent, k, a[k]))
                    print('')

        def check_seq(e, a, indent):
            if e == a:
                return
            for i, (e2, a2) in enumerate(zip(e, a)):
                if isinstance(e2, dict) and isinstance(a2, dict):
                    check_dict(e2, a2, indent + ' ')
                elif (isinstance(e2, tuple) and isinstance(a2, tuple) or isinstance(e2, list) and isinstance(a2, list)):
                    check_seq(e2, a2, indent + ' ')
                elif e1 != a1:
                    print('%sExpected "%s"' % (indent, e2))
                    print('%sActual   "%s"' % (indent, a2))
                    print('')

        for e1, a1 in zip(expected, r):
            check_dict(e1, a1, '')
        self.fail(msg)

    def test_regex(self):
        self.assertSequenceEqual(
            [i.strip() for i in re.split(TOKENS_REGEX, 'f(\'\', \'a\', \'a\\\'b\', "", "a", "a\\\"b")') if i.strip()],
            ['f', '(', "''", ',', "'a'", ',', "'a\\'b'", ',', '""', ',', '"a"', ',', '"a\\"b"', ')']
        )
        self.assertSequenceEqual(
            [i.strip() for i in re.split(TOKENS_REGEX, 'f(1, 1., -1, -1.)') if i.strip()],
            ['f', '(', '1', ',', '1.', ',', '-1', ',', '-1.', ')']
        )
        self.assertSequenceEqual(
            [i.strip() for i in re.split(TOKENS_REGEX, 'f(a, *a, **a, ...)') if i.strip()],
            ['f', '(', 'a', ',', '*', 'a', ',', '**', 'a', ',', '...', ')']
        )
        self.assertSequenceEqual(
            [i.strip() for i in re.split(TOKENS_REGEX, 'f(a:123, a=123) --> => ->') if i.strip()],
            ['f', '(', 'a', ':', '123', ',', 'a', '=', '123', ')', '-->', '=>', '->']
        )


    def test_numpy_1(self):
        self.check_doc_str(
            """arange([start,] stop[, step,], dtype=None)

    Returns
    -------
    out : ndarray""",
            'numpy',
            'arange',
            [{
                'doc': 'Returns\n    -------\n    out : ndarray',
                'ret_type': [('', 'ndarray')],
                'args': (
                    {'name': 'start', 'default_value':'None'}, 
                    {'name': 'stop'}, 
                    {'name': 'step', 'default_value': 'None'},
                    {'name': 'dtype', 'default_value':'None'}, 
                )
            }]
        )

    def test_numpy_2(self):
        self.check_doc_str(
            """arange([start,] stop[, step,], dtype=None)

    Return - out : ndarray""",
        'numpy',
        'arange',
            [{
                'doc': 'Return - out : ndarray',
                'ret_type': [('', 'ndarray')],
                'args': (
                    {'name': 'start', 'default_value':'None'}, 
                    {'name': 'stop'}, 
                    {'name': 'step', 'default_value': 'None'},
                    {'name': 'dtype', 'default_value':'None'}, 
                )
            }]
        )

    def test_reduce(self):
        self.check_doc_str(
            'reduce(function, sequence[, initial]) -> value',
            BUILTIN,
            'reduce',
            mod=__builtins__,
            expected = [{
                'args': (
                    {'name': 'function'},
                    {'name': 'sequence'},
                    {'default_value': 'None', 'name': 'initial'}
                ), 
                'doc': '', 
                'ret_type': [('', 'value')]
            }]
        )

    def test_pygame_draw_arc(self):
        self.check_doc_str(
            'pygame.draw.arc(Surface, color, Rect, start_angle, stop_angle, width=1): return Rect', 
            'draw',
            'arc',
            [{
                'args': (
                    {'name': 'Surface'},
                    {'name': 'color'},
                    {'name': 'Rect'},
                    {'name': 'start_angle'},
                    {'name': 'stop_angle'},
                    {'default_value': '1', 'name': 'width'}
                ),
                'doc': '',
                'ret_type': [('', 'Rect')]
            }]
        )

    def test_isdigit(self):
        self.check_doc_str(
            '''B.isdigit() -> bool

Return True if all characters in B are digits
and there is at least one character in B, False otherwise.''',
            'bytes',
            'isdigit',
            [{
                'args': (),
                'doc': 'Return True if all characters in B are digits\nand there is at least one character in B, False otherwise.',
                'ret_type': [(BUILTIN, 'bool')]
            }]
        )

    def test_init(self):
        self.check_doc_str(
            'x.__init__(...) initializes x; see help(type(x)) for signature',
            'str',
            '__init__',
            [{'args': ({'arg_format': '*', 'name': 'args'},), 'doc': 'initializes x; see help(type(x)) for signature'}]
        )

    def test_find(self):
        self.check_doc_str(
            'S.find(sub [,start [,end]]) -> int',
            'str',
            'find',
            [{
                'args': (
                    {'name': 'sub'},
                    {'default_value': 'None', 'name': 'start'},
                    {'default_value': 'None', 'name': 'end'}
                ),
                'doc': '',
                'ret_type': [(BUILTIN, 'int')]
            }]
        )

    def test_format(self):
        self.check_doc_str(
            'S.format(*args, **kwargs) -> unicode',
            'str',
            'format',
            [{
                'args': (
                    {'arg_format': '*', 'name': 'args'},
                    {'arg_format': '**', 'name': 'kwargs'}
                ),
                'doc': '',
                'ret_type': [(BUILTIN, unicode.__name__)]
            }]
        )
    
    def test_ascii(self):
        self.check_doc_str(
            "'ascii(object) -> string\n\nReturn the same as repr().  In Python 3.x, the repr() result will\\ncontain printable characters unescaped, while the ascii() result\\nwill have such characters backslash-escaped.'",
            'future_builtins',
            'ascii',
            [{
                'args': ({'name': 'object'},),
                'doc': "Return the same as repr().  In Python 3.x, the repr() result will\\ncontain printable characters unescaped, while the ascii() result\\nwill have such characters backslash-escaped.'",
                'ret_type': [(BUILTIN, 'str')]
            }]
        )

    def test_preannotation(self):
        self.check_doc_str(
            'f(INT class_code) => SpaceID',
            'fob',
            'f',
            [{
                'args': ({'name': 'class_code', 'type': [(BUILTIN, 'int')]},),
                'doc': '',
                'ret_type': [('', 'SpaceID')]
            }])

    def test_compress(self):
        self.check_doc_str(
            'compress(data, selectors) --> iterator over selected data\n\nReturn data elements',
            'itertools',
            'compress',
            [{
                'args': ({'name': 'data'}, {'name': 'selectors'}),
                'doc': 'Return data elements',
                'ret_type': [('', 'iterator')]
            }]
        )

    def test_isinstance(self):
        self.check_doc_str(
            'isinstance(object, class-or-type-or-tuple) -> bool\n\nReturn whether an object is an '
            'instance of a class or of a subclass thereof.\nWith a type as second argument, '
            'return whether that is the object\'s type.\nThe form using a tuple, isinstance(x, (A, B, ...)),'
            ' is a shortcut for\nisinstance(x, A) or isinstance(x, B) or ... (etc.).',
            BUILTIN,
            'isinstance',
            [{
                'args': ({'name': 'object'}, {'name': 'class-or-type-or-tuple'}),
                'doc': "Return whether an object is an instance of a class or of a subclass thereof.\n"
                       "With a type as second argument, return whether that is the object's type.\n"
                       "The form using a tuple, isinstance(x, (A, B, ...)), is a shortcut for\n"
                       "isinstance(x, A) or isinstance(x, B) or ... (etc.).",
                'ret_type': [(BUILTIN, 'bool')]
            }]
        )

    def test_tuple_parameters(self):
        self.check_doc_str(
            'pygame.Rect(left, top, width, height): return Rect\n'
            'pygame.Rect((left, top), (width, height)): return Rect\n'
            'pygame.Rect(object): return Rect\n'
            'pygame object for storing rectangular coordinates',
            'pygame',
            'Rect',
            [{
                'args': ({'name': 'left'}, {'name': 'top'}, {'name': 'width'}, {'name': 'height'}),
                'doc': 'pygame object for storing rectangular coordinates',
                'ret_type': [('', 'Rect')]
            },
            {
                'args': ({'name': 'left, top'}, {'name': 'width, height'}),
                'doc': 'pygame object for storing rectangular coordinates',
                'ret_type': [('', 'Rect')]
            },
            {
                'args': ({'name': 'object'},),
                'doc': 'pygame object for storing rectangular coordinates',
                'ret_type': [('', 'Rect')]
            }]
        )

    def test_read(self):
        self.check_doc_str(
            'read([size]) -> read at most size bytes, returned as a string.\n\n'
            'If the size argument is negative or omitted, read until EOF is reached.\n'
            'Notice that when in non-blocking mode, less data than what was requested\n'
            'may be returned, even if no size parameter was given.',
            BUILTIN,
            'read',
            mod=__builtins__,
            expected=[{
                'args': ({'default_value': 'None', 'name': 'size'},),
                'doc': 'read at most size bytes, returned as a string.\n\nIf the size argument is negative or omitted, read until EOF is reached.\nNotice that when in non-blocking mode, less data than what was requested\nmay be returned, even if no size parameter was given.',
                'ret_type': [('', '')]
            }]
        )


        r = get_overloads_from_doc_string(
            'read([size]) -> read at most size bytes, returned as a string.\n\n'
            'If the size argument is negative or omitted, read until EOF is reached.\n'
            'Notice that when in non-blocking mode, less data than what was requested\n'
            'may be returned, even if no size parameter was given.',
            __builtins__,
            None,
            'read'
         )

        self.assertEqual(
            r,
            [{
                'args': ({'default_value': 'None', 'name': 'size'},),
                'doc': 'read at most size bytes, returned as a string.\n\nIf the size argument is negative or omitted, read until EOF is reached.\nNotice that when in non-blocking mode, less data than what was requested\nmay be returned, even if no size parameter was given.',
                'ret_type': [('', '')]
            }],
            repr(r)
        )

    def test_new(self):
        self.check_doc_str(
            'T.__new__(S, ...) -> a new object with type S, a subtype of T',
            'struct',
            '__new__',
            [{
                'ret_type': [('', '')],
                'doc': 'a new object with type S, a subtype of T',
                'args': ({'name': 'S'}, {'arg_format': '*', 'name': 'args'})
            }]
        )

    def test_C_prototype(self):
        self.check_doc_str(
            'GetDriverByName(char const * name) -> Driver',
            '',
            'GetDriverByName',
            [{
                'ret_type': [('', 'Driver')],
                'doc': '',
                'args': ({'name': 'name', 'type': [(BUILTIN, 'str')]},),
            }]
        )

    def test_chmod(self):
        self.check_doc_str(
            'chmod(path, mode, *, dir_fd=None, follow_symlinks=True)',
            'nt',
            'chmod',
            [{
                'doc': '',
                'args': (
                    {'name': 'path'},
                    {'name': 'mode'},
                    {'name': 'args', 'arg_format': '*'},
                    {'name': 'dir_fd', 'default_value': 'None'},
                    {'name': 'follow_symlinks', 'default_value': 'True'}
                )
            }]
        )

    def test_open(self):
        if sys.version_info[0] >= 3:
            expect_ret_type = ('_io', '_IOBase')
        else:
            expect_ret_type = (BUILTIN, 'file')
        
        self.check_doc_str(
            'open(file, mode=\'r\', buffering=-1, encoding=None,\n' +
            '     errors=None, newline=None, closefd=True, opener=None)' +
            ' -> file object\n\nOpen file',
            BUILTIN,
            'open',
            [{
                'doc': 'Open file',
                'ret_type': [expect_ret_type],
                'args': (
                    {'name': 'file'},
                    {'name': 'mode', 'default_value': "'r'"},
                    {'name': 'buffering', 'default_value': '-1'},
                    {'name': 'encoding', 'default_value': 'None'},
                    {'name': 'errors', 'default_value': 'None'},
                    {'name': 'newline', 'default_value': 'None'},
                    {'name': 'closefd', 'default_value': 'True'},
                    {'name': 'opener', 'default_value': 'None'},
                )
            }]
        )

    def test_optional_with_default(self):
        self.check_doc_str(
            'max(iterable[, key=func]) -> value',
            BUILTIN,
            'max',
            [{
                'doc': '',
                'ret_type': [('', 'value')],
                'args': (
                    {'name': 'iterable'},
                    {'name': 'key', 'default_value': 'func'}
                )
            }]
        )

    def test_pyplot_figure(self):
        pyplot_doc = """
    Creates a new figure.

    Parameters
    ----------

    num : integer or string, optional, default: none
        If not provided, a new figure will be created, and a the figure number
        will be increamted. The figure objects holds this number in a `number`
        attribute.
        If num is provided, and a figure with this id already exists, make
        it active, and returns a reference to it. If this figure does not
        exists, create it and returns it.
        If num is a string, the window title will be set to this figure's
        `num`.

    figsize : tuple of integers, optional, default : None
        width, height in inches. If not provided, defaults to rc
        figure.figsize.

    dpi : integer, optional, default ; None
        resolution of the figure. If not provided, defaults to rc figure.dpi.

    facecolor :
        the background color; If not provided, defaults to rc figure.facecolor

    edgecolor :
        the border color. If not provided, defaults to rc figure.edgecolor

    Returns
    -------
    figure : Figure
        The Figure instance returned will also be passed to new_figure_manager
        in the backends, which allows to hook custom Figure classes into the
        pylab interface. Additional kwargs will be passed to the figure init
        function.

    Note
    ----
    If you are creating many figures, make sure you explicitly call "close"
    on the figures you are not using, because this will enable pylab
    to properly clean up the memory.

    rcParams defines the default values, which can be modified in the
    matplotlibrc file

    """
        self.check_doc_str(
            pyplot_doc,
            'matplotlib.pyplot',
            'figure',
            [{
                'doc': pyplot_doc,
                'ret_type': [('', 'Figure')],
                'args': (
                    {'name': 'args', 'arg_format': '*'},
                    {'name': 'kwargs', 'arg_format': '**'}
                )
            }]
        )

if __name__ == '__main__':
    unittest.main()
