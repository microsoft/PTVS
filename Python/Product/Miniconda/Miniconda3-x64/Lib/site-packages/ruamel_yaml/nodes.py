# coding: utf-8

from __future__ import print_function

from .compat import string_types

if False:  # MYPY
    from typing import Dict, Any, Text   # NOQA


class Node(object):
    __slots__ = 'tag', 'value', 'start_mark', 'end_mark', 'comment', 'anchor',

    def __init__(self, tag, value, start_mark, end_mark, comment=None):
        # type: (Any, Any, Any, Any, Any) -> None
        self.tag = tag
        self.value = value
        self.start_mark = start_mark
        self.end_mark = end_mark
        self.comment = comment
        self.anchor = None

    def __repr__(self):
        # type: () -> str
        value = self.value
        # if isinstance(value, list):
        #     if len(value) == 0:
        #         value = '<empty>'
        #     elif len(value) == 1:
        #         value = '<1 item>'
        #     else:
        #         value = '<%d items>' % len(value)
        # else:
        #     if len(value) > 75:
        #         value = repr(value[:70]+u' ... ')
        #     else:
        #         value = repr(value)
        value = repr(value)
        return '%s(tag=%r, value=%s)' % (self.__class__.__name__,
                                         self.tag, value)

    def dump(self, indent=0):
        # type: (int) -> None
        if isinstance(self.value, string_types):
            print('{}{}(tag={!r}, value={!r})'.format(
                '  ' * indent, self.__class__.__name__, self.tag, self.value))
            if self.comment:
                print('    {}comment: {})'.format(
                    '  ' * indent, self.comment))
            return
        print('{}{}(tag={!r})'.format(
            '  ' * indent, self.__class__.__name__, self.tag))
        if self.comment:
            print('    {}comment: {})'.format(
                '  ' * indent, self.comment))
        for v in self.value:
            if isinstance(v, tuple):
                for v1 in v:
                    v1.dump(indent + 1)
            elif isinstance(v, Node):
                v.dump(indent + 1)
            else:
                print('Node value type?', type(v))


class ScalarNode(Node):
    """
    styles:
      ? -> set() ? key, no value
      " -> double quoted
      ' -> single quoted
      | -> literal style
      > -> folding style
    """
    __slots__ = 'style',
    id = 'scalar'

    def __init__(self, tag, value, start_mark=None, end_mark=None, style=None,
                 comment=None):
        # type: (Any, Any, Any, Any, Any, Any) -> None
        Node.__init__(self, tag, value, start_mark, end_mark, comment=comment)
        self.style = style


class CollectionNode(Node):
    __slots__ = 'flow_style', 'anchor',

    def __init__(self, tag, value, start_mark=None, end_mark=None,
                 flow_style=None, comment=None, anchor=None):
        # type: (Any, Any, Any, Any, Any, Any, Any) -> None
        Node.__init__(self, tag, value, start_mark, end_mark, comment=comment)
        self.flow_style = flow_style
        self.anchor = anchor


class SequenceNode(CollectionNode):
    __slots__ = ()
    id = 'sequence'


class MappingNode(CollectionNode):
    __slots__ = ('merge', )
    id = 'mapping'

    def __init__(self, tag, value, start_mark=None, end_mark=None,
                 flow_style=None, comment=None, anchor=None):
        # type: (Any, Any, Any, Any, Any, Any, Any) -> None
        CollectionNode.__init__(self, tag, value, start_mark, end_mark,
                                flow_style, comment, anchor)
        self.merge = None
