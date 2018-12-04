# coding: utf-8

from __future__ import absolute_import, print_function

"""
stuff to deal with comments and formatting on dict/list/ordereddict/set
these are not really related, formatting could be factored out as
a separate base
"""

import sys
import copy


from ruamel_yaml.compat import ordereddict, PY2, string_types
from ruamel_yaml.scalarstring import ScalarString

if PY2:
    from collections import MutableSet, Sized, Set
else:
    from collections.abc import MutableSet, Sized, Set

if False:  # MYPY
    from typing import Any, Dict, Optional, List, Union  # NOQA

__all__ = ["CommentedSeq", "CommentedKeySeq",
           "CommentedMap", "CommentedOrderedMap",
           "CommentedSet", 'comment_attrib', 'merge_attrib']

comment_attrib = '_yaml_comment'
format_attrib = '_yaml_format'
line_col_attrib = '_yaml_line_col'
anchor_attrib = '_yaml_anchor'
merge_attrib = '_yaml_merge'
tag_attrib = '_yaml_tag'


class Comment(object):
    # sys.getsize tested the Comment objects, __slots__ makes them bigger
    # and adding self.end did not matter
    __slots__ = 'comment', '_items', '_end', '_start',
    attrib = comment_attrib

    def __init__(self):
        # type: () -> None
        self.comment = None  # [post, [pre]]
        # map key (mapping/omap/dict) or index (sequence/list) to a  list of
        # dict: post_key, pre_key, post_value, pre_value
        # list: pre item, post item
        self._items = {}  # type: Dict[Any, Any]
        # self._start = [] # should not put these on first item
        self._end = []  # type: List[Any] # end of document comments

    def __str__(self):
        # type: () -> str
        if bool(self._end):
            end = ',\n  end=' + str(self._end)
        else:
            end = ''
        return "Comment(comment={0},\n  items={1}{2})".format(
            self.comment, self._items, end)

    @property
    def items(self):
        # type: () -> Any
        return self._items

    @property
    def end(self):
        # type: () -> Any
        return self._end

    @end.setter
    def end(self, value):
        # type: (Any) -> None
        self._end = value

    @property
    def start(self):
        # type: () -> Any
        return self._start

    @start.setter
    def start(self, value):
        # type: (Any) -> None
        self._start = value


# to distinguish key from None
def NoComment():
    # type: () -> None
    pass


class Format(object):
    __slots__ = '_flow_style',
    attrib = format_attrib

    def __init__(self):
        # type: () -> None
        self._flow_style = None  # type: Any

    def set_flow_style(self):
        # type: () -> None
        self._flow_style = True

    def set_block_style(self):
        # type: () -> None
        self._flow_style = False

    def flow_style(self, default=None):
        # type: (Optional[Any]) -> Any
        """if default (the flow_style) is None, the flow style tacked on to
        the object explicitly will be taken. If that is None as well the
        default flow style rules the format down the line, or the type
        of the constituent values (simple -> flow, map/list -> block)"""
        if self._flow_style is None:
            return default
        return self._flow_style


class LineCol(object):
    attrib = line_col_attrib

    def __init__(self):
        # type: () -> None
        self.line = None
        self.col = None
        self.data = None  # type: Union[None, Dict[Any, Any]]

    def add_kv_line_col(self, key, data):
        # type: (Any, Any) -> None
        if self.data is None:
            self.data = {}
        self.data[key] = data

    def key(self, k):
        # type: (Any) -> Any
        return self._kv(k, 0, 1)

    def value(self, k):
        # type: (Any) -> Any
        return self._kv(k, 2, 3)

    def _kv(self, k, x0, x1):
        # type: (Any, Any, Any) -> Any
        if self.data is None:
            return None
        data = self.data[k]
        return data[x0], data[x1]

    def item(self, idx):
        # type: (Any) -> Any
        if self.data is None:
            return None
        return self.data[idx][0], self.data[idx][1]

    def add_idx_line_col(self, key, data):
        # type: (Any, Any) -> None
        if self.data is None:
            self.data = {}  # type: Dict[Any, Any]
        self.data[key] = data


class Anchor(object):
    __slots__ = 'value', 'always_dump',
    attrib = anchor_attrib

    def __init__(self):
        # type: () -> None
        self.value = None
        self.always_dump = False


class Tag(object):
    """store tag information for roundtripping"""
    __slots__ = 'value',
    attrib = tag_attrib

    def __init__(self):
        # type: () -> None
        self.value = None

    def __repr__(self):
        # type: () -> Any
        return '{0.__class__.__name__}({0.value!r})'.format(self)


class CommentedBase(object):
    @property
    def ca(self):
        # type: () -> Any
        if not hasattr(self, Comment.attrib):
            setattr(self, Comment.attrib, Comment())
        return getattr(self, Comment.attrib)

    def yaml_end_comment_extend(self, comment, clear=False):
        # type: (Any, bool) -> None
        if comment is None:
            return
        if clear or self.ca.end is None:
            self.ca.end = []
        self.ca.end.extend(comment)

    def yaml_key_comment_extend(self, key, comment, clear=False):
        # type: (Any, Any, bool) -> None
        r = self.ca._items.setdefault(key, [None, None, None, None])
        if clear or r[1] is None:
            if comment[1] is not None:
                assert isinstance(comment[1], list)
            r[1] = comment[1]
        else:
            r[1].extend(comment[0])
        r[0] = comment[0]

    def yaml_value_comment_extend(self, key, comment, clear=False):
        # type: (Any, Any, bool) -> None
        r = self.ca._items.setdefault(key, [None, None, None, None])
        if clear or r[3] is None:
            if comment[1] is not None:
                assert isinstance(comment[1], list)
            r[3] = comment[1]
        else:
            r[3].extend(comment[0])
        r[2] = comment[0]

    def yaml_set_start_comment(self, comment, indent=0):
        # type: (Any, Any) -> None
        """overwrites any preceding comment lines on an object
        expects comment to be without `#` and possible have multiple lines
        """
        from .error import CommentMark
        from .tokens import CommentToken
        pre_comments = self._yaml_get_pre_comment()
        if comment[-1] == '\n':
            comment = comment[:-1]  # strip final newline if there
        start_mark = CommentMark(indent)
        for com in comment.split('\n'):
            pre_comments.append(CommentToken('# ' + com + '\n', start_mark, None))

    def yaml_set_comment_before_after_key(self, key, before=None, indent=0,
                                          after=None, after_indent=None):
        # type: (Any, Any, Any, Any, Any) -> None
        """
        expects comment (before/after) to be without `#` and possible have multiple lines
        """
        from ruamel_yaml.error import CommentMark
        from ruamel_yaml.tokens import CommentToken

        def comment_token(s, mark):
            # type: (Any, Any) -> Any
            # handle empty lines as having no comment
            return CommentToken(('# ' if s else '') + s + '\n', mark, None)

        if after_indent is None:
            after_indent = indent + 2
        if before and (len(before) > 1) and before[-1] == '\n':
            before = before[:-1]  # strip final newline if there
        if after and after[-1] == '\n':
            after = after[:-1]  # strip final newline if there
        start_mark = CommentMark(indent)
        c = self.ca.items.setdefault(key, [None, [], None, None])
        if before == '\n':
            c[1].append(comment_token('', start_mark))
        elif before:
            for com in before.split('\n'):
                c[1].append(comment_token(com, start_mark))
        if after:
            start_mark = CommentMark(after_indent)
            if c[3] is None:
                c[3] = []
            for com in after.split('\n'):
                c[3].append(comment_token(com, start_mark))  # type: ignore

    @property
    def fa(self):
        # type: () -> Any
        """format attribute

        set_flow_style()/set_block_style()"""
        if not hasattr(self, Format.attrib):
            setattr(self, Format.attrib, Format())
        return getattr(self, Format.attrib)

    def yaml_add_eol_comment(self, comment, key=NoComment, column=None):
        # type: (Any, Optional[Any], Optional[Any]) -> None
        """
        there is a problem as eol comments should start with ' #'
        (but at the beginning of the line the space doesn't have to be before
        the #. The column index is for the # mark
        """
        from .tokens import CommentToken
        from .error import CommentMark
        if column is None:
            column = self._yaml_get_column(key)
        if comment[0] != '#':
            comment = '# ' + comment
        if column is None:
            if comment[0] == '#':
                comment = ' ' + comment
                column = 0
        start_mark = CommentMark(column)
        ct = [CommentToken(comment, start_mark, None), None]
        self._yaml_add_eol_comment(ct, key=key)

    @property
    def lc(self):
        # type: () -> Any
        if not hasattr(self, LineCol.attrib):
            setattr(self, LineCol.attrib, LineCol())
        return getattr(self, LineCol.attrib)

    def _yaml_set_line_col(self, line, col):
        # type: (Any, Any) -> None
        self.lc.line = line
        self.lc.col = col

    def _yaml_set_kv_line_col(self, key, data):
        # type: (Any, Any) -> None
        self.lc.add_kv_line_col(key, data)

    def _yaml_set_idx_line_col(self, key, data):
        # type: (Any, Any) -> None
        self.lc.add_idx_line_col(key, data)

    @property
    def anchor(self):
        # type: () -> Any
        if not hasattr(self, Anchor.attrib):
            setattr(self, Anchor.attrib, Anchor())
        return getattr(self, Anchor.attrib)

    def yaml_anchor(self):
        # type: () -> Any
        if not hasattr(self, Anchor.attrib):
            return None
        return self.anchor

    def yaml_set_anchor(self, value, always_dump=False):
        # type: (Any, bool) -> None
        self.anchor.value = value
        self.anchor.always_dump = always_dump

    @property
    def tag(self):
        # type: () -> Any
        if not hasattr(self, Tag.attrib):
            setattr(self, Tag.attrib, Tag())
        return getattr(self, Tag.attrib)

    def yaml_set_tag(self, value):
        # type: (Any) -> None
        self.tag.value = value

    def copy_attributes(self, t, deep=False):
        # type: (Any, bool) -> None
        for a in [Comment.attrib, Format.attrib, LineCol.attrib, Anchor.attrib,
                  Tag.attrib, merge_attrib]:
            if hasattr(self, a):
                if deep:
                    setattr(t, a, copy.deepcopy(getattr(self, a)))
                else:
                    setattr(t, a, getattr(self, a))

    def _yaml_add_eol_comment(self, comment, key):
        # type: (Any, Any) -> None
        raise NotImplementedError

    def _yaml_get_pre_comment(self):
        # type: () -> Any
        raise NotImplementedError

    def _yaml_get_column(self, key):
        # type: (Any) -> Any
        raise NotImplementedError


class CommentedSeq(list, CommentedBase):
    __slots__ = Comment.attrib,

    def _yaml_add_comment(self, comment, key=NoComment):
        # type: (Any, Optional[Any]) -> None
        if key is not NoComment:
            self.yaml_key_comment_extend(key, comment)
        else:
            self.ca.comment = comment

    def _yaml_add_eol_comment(self, comment, key):
        # type: (Any, Any) -> None
        self._yaml_add_comment(comment, key=key)

    def _yaml_get_columnX(self, key):
        # type: (Any) -> Any
        return self.ca.items[key][0].start_mark.column

    def insert(self, idx, val):
        # type: (Any, Any) -> None
        """the comments after the insertion have to move forward"""
        list.insert(self, idx, val)
        for list_index in sorted(self.ca.items, reverse=True):
            if list_index < idx:
                break
            self.ca.items[list_index + 1] = self.ca.items.pop(list_index)

    def pop(self, idx=None):
        # type: (Any) -> Any
        res = list.pop(self, idx)  # type: ignore
        self.ca.items.pop(idx, None)  # might not be there -> default value
        for list_index in sorted(self.ca.items):
            if list_index < idx:
                continue
            self.ca.items[list_index - 1] = self.ca.items.pop(list_index)
        return res

    def _yaml_get_column(self, key):
        # type: (Any) -> Any
        column = None
        sel_idx = None
        pre, post = key - 1, key + 1
        if pre in self.ca.items:
            sel_idx = pre
        elif post in self.ca.items:
            sel_idx = post
        else:
            # self.ca.items is not ordered
            for row_idx, k1 in enumerate(self):
                if row_idx >= key:
                    break
                if row_idx not in self.ca.items:
                    continue
                sel_idx = row_idx
        if sel_idx is not None:
            column = self._yaml_get_columnX(sel_idx)
        return column

    def _yaml_get_pre_comment(self):
        # type: () -> Any
        pre_comments = []  # type: List[Any]
        if self.ca.comment is None:
            self.ca.comment = [None, pre_comments]
        else:
            self.ca.comment[1] = pre_comments
        return pre_comments

    def __deepcopy__(self, memo):
        # type: (Any) -> Any
        res = self.__class__()
        memo[id(self)] = res
        for k in self:
            res.append(copy.deepcopy(k))
            self.copy_attributes(res, deep=True)
        return res

    def __setitem__(self, idx, value):
        # type: (Any, Any) -> None
        # try to preserve the scalarstring type if setting an existing key to a new value
        if idx < len(self):
            if isinstance(value, string_types) and \
               not isinstance(value, ScalarString) and \
               isinstance(self[idx], ScalarString):
                value = type(self[idx])(value)
        list.__setitem__(self, idx, value)


class CommentedKeySeq(tuple, CommentedBase):
    """This primarily exists to be able to roundtrip keys that are sequences"""
    def _yaml_add_comment(self, comment, key=NoComment):
        # type: (Any, Optional[Any]) -> None
        if key is not NoComment:
            self.yaml_key_comment_extend(key, comment)
        else:
            self.ca.comment = comment

    def _yaml_add_eol_comment(self, comment, key):
        # type: (Any, Any) -> None
        self._yaml_add_comment(comment, key=key)

    def _yaml_get_columnX(self, key):
        # type: (Any) -> Any
        return self.ca.items[key][0].start_mark.column

    def _yaml_get_column(self, key):
        # type: (Any) -> Any
        column = None
        sel_idx = None
        pre, post = key - 1, key + 1
        if pre in self.ca.items:
            sel_idx = pre
        elif post in self.ca.items:
            sel_idx = post
        else:
            # self.ca.items is not ordered
            for row_idx, k1 in enumerate(self):
                if row_idx >= key:
                    break
                if row_idx not in self.ca.items:
                    continue
                sel_idx = row_idx
        if sel_idx is not None:
            column = self._yaml_get_columnX(sel_idx)
        return column

    def _yaml_get_pre_comment(self):
        # type: () -> Any
        pre_comments = []  # type: List[Any]
        if self.ca.comment is None:
            self.ca.comment = [None, pre_comments]
        else:
            self.ca.comment[1] = pre_comments
        return pre_comments


class CommentedMapView(Sized):
    __slots__ = '_mapping',

    def __init__(self, mapping):
        # type: (Any) -> None
        self._mapping = mapping

    def __len__(self):
        # type: () -> int
        count = len(self._mapping)
        done = []  # type: List[Any] # list of processed merge items, kept for masking
        for merged in getattr(self._mapping, merge_attrib, []):
            for x in merged[1]:
                if self._mapping._unmerged_contains(x):
                    continue
                for y in done:
                    if x in y:
                        break
                else:
                    count += 1
            done.append(merged[1])
        return count

    def __repr__(self):
        # type: () -> str
        return '{0.__class__.__name__}({0._mapping!r})'.format(self)


class CommentedMapKeysView(CommentedMapView, Set):
    __slots__ = ()

    @classmethod
    def _from_iterable(self, it):
        # type: (Any) -> Any
        return set(it)

    def __contains__(self, key):
        # type: (Any) -> Any
        return key in self._mapping

    def __iter__(self):
        # type: () -> Any  # yield from self._mapping  # not in py27, pypy
        for x in self._mapping:
            yield x


class CommentedMapItemsView(CommentedMapView, Set):
    __slots__ = ()

    @classmethod
    def _from_iterable(self, it):
        # type: (Any) -> Any
        return set(it)

    def __contains__(self, item):
        # type: (Any) -> Any
        key, value = item
        try:
            v = self._mapping[key]
        except KeyError:
            return False
        else:
            return v == value

    def __iter__(self):
        # type: () -> Any
        for key in self._mapping._keys():
            yield (key, self._mapping[key])


class CommentedMapValuesView(CommentedMapView):
    __slots__ = ()

    def __contains__(self, value):
        # type: (Any) -> Any
        for key in self._mapping:
            if value == self._mapping[key]:
                return True
        return False

    def __iter__(self):
        # type: () -> Any
        for key in self._mapping:
            yield self._mapping[key]


class CommentedMap(ordereddict, CommentedBase):
    __slots__ = Comment.attrib,

    def _yaml_add_comment(self, comment, key=NoComment, value=NoComment):
        # type: (Any, Optional[Any], Optional[Any]) -> None
        """values is set to key to indicate a value attachment of comment"""
        if key is not NoComment:
            self.yaml_key_comment_extend(key, comment)
            return
        if value is not NoComment:
            self.yaml_value_comment_extend(value, comment)
        else:
            self.ca.comment = comment

    def _yaml_add_eol_comment(self, comment, key):
        # type: (Any, Any) -> None
        """add on the value line, with value specified by the key"""
        self._yaml_add_comment(comment, value=key)

    def _yaml_get_columnX(self, key):
        # type: (Any) -> Any
        return self.ca.items[key][2].start_mark.column

    def _yaml_get_column(self, key):
        # type: (Any) -> Any
        column = None
        sel_idx = None
        pre, post, last = None, None, None
        for x in self:
            if pre is not None and x != key:
                post = x
                break
            if x == key:
                pre = last
            last = x
        if pre in self.ca.items:
            sel_idx = pre
        elif post in self.ca.items:
            sel_idx = post
        else:
            # self.ca.items is not ordered
            for row_idx, k1 in enumerate(self):
                if k1 >= key:
                    break
                if k1 not in self.ca.items:
                    continue
                sel_idx = k1
        if sel_idx is not None:
            column = self._yaml_get_columnX(sel_idx)
        return column

    def _yaml_get_pre_comment(self):
        # type: () -> Any
        pre_comments = []  # type: List[Any]
        if self.ca.comment is None:
            self.ca.comment = [None, pre_comments]
        else:
            self.ca.comment[1] = pre_comments
        return pre_comments

    def update(self, vals):
        # type: (Any) -> None
        try:
            ordereddict.update(self, vals)
        except TypeError:
            # probably a dict that is used
            for x in vals:
                self[x] = vals[x]

    def insert(self, pos, key, value, comment=None):
        # type: (Any, Any, Any, Optional[Any]) -> None
        """insert key value into given position
        attach comment if provided
        """
        ordereddict.insert(self, pos, key, value)
        if comment is not None:
            self.yaml_add_eol_comment(comment, key=key)

    def mlget(self, key, default=None, list_ok=False):
        # type: (Any, Any, Any) -> Any
        """multi-level get that expects dicts within dicts"""
        if not isinstance(key, list):
            return self.get(key, default)
        # assume that the key is a list of recursively accessible dicts

        def get_one_level(key_list, level, d):
            # type: (Any, Any, Any) -> Any
            if not list_ok:
                assert isinstance(d, dict)
            if level >= len(key_list):
                if level > len(key_list):
                    raise IndexError
                return d[key_list[level - 1]]
            return get_one_level(key_list, level + 1, d[key_list[level - 1]])

        try:
            return get_one_level(key, 1, self)
        except KeyError:
            return default
        except (TypeError, IndexError):
            if not list_ok:
                raise
            return default

    def __getitem__(self, key):
        # type: (Any) -> Any
        try:
            return ordereddict.__getitem__(self, key)
        except KeyError:
            for merged in getattr(self, merge_attrib, []):
                if key in merged[1]:
                    return merged[1][key]
            raise

    def __setitem__(self, key, value):
        # type: (Any, Any) -> None
        # try to preserve the scalarstring type if setting an existing key to a new value
        if key in self:
            if isinstance(value, string_types) and \
               not isinstance(value, ScalarString) and \
               isinstance(self[key], ScalarString):
                value = type(self[key])(value)
        ordereddict.__setitem__(self, key, value)

    def _unmerged_contains(self, key):
        # type: (Any) -> Any
        if ordereddict.__contains__(self, key):
            return True
        return None

    def __contains__(self, key):
        # type: (Any) -> bool
        if ordereddict.__contains__(self, key):
            return True
        # this will only work once the mapping/dict is built to completion
        for merged in getattr(self, merge_attrib, []):
            if key in merged[1]:
                return True
        return False

    def get(self, key, default=None):
        # type: (Any, Any) -> Any
        try:
            return self.__getitem__(key)
        except:  # NOQA
            return default

    def __repr__(self):
        # type: () -> Any
        if not hasattr(self, merge_attrib):
            return ordereddict.__repr__(self)
        return 'ordereddict(' + repr(list(self._items())) + ')'

    def non_merged_items(self):
        # type: () -> Any
        for x in ordereddict.__iter__(self):
            yield x, ordereddict.__getitem__(self, x)

    def __delitem__(self, key):
        # type: (Any) -> None
        found = True
        for merged in getattr(self, merge_attrib, []):
            try:
                del merged[1][key]
                found = True
            except KeyError:
                pass
        try:
            ordereddict.__delitem__(self, key)
        except KeyError:
            if not found:
                raise

    def __iter__(self):
        # type: () -> Any
        for x in ordereddict.__iter__(self):
            yield x
        done = []  # type: List[Any]  # list of processed merge items, kept for masking
        for merged in getattr(self, merge_attrib, []):
            for x in merged[1]:
                if ordereddict.__contains__(self, x):
                    continue
                for y in done:
                    if x in y:
                        break
                else:
                    yield x
            done.append(merged[1])

    def _keys(self):
        # type: () -> Any
        for x in ordereddict.__iter__(self):
            yield x
        done = []  # type: List[Any]  # list of processed merge items, kept for masking
        for merged in getattr(self, merge_attrib, []):
            for x in merged[1]:
                if ordereddict.__contains__(self, x):
                    continue
                for y in done:
                    if x in y:
                        break
                else:
                    yield x
            done.append(merged[1])

    if PY2:
        def keys(self):
            # type: () -> Any
            return list(self._keys())

        def iterkeys(self):
            # type: () -> Any
            return self._keys()

        def viewkeys(self):
            # type: () -> Any
            return CommentedMapKeysView(self)
    else:
        def keys(self):
            # type: () -> Any
            return CommentedMapKeysView(self)

    def _values(self):
        # type: () -> Any
        for x in ordereddict.__iter__(self):
            yield ordereddict.__getitem__(self, x)
        done = []  # type: List[Any]  # list of processed merge items, kept for masking
        for merged in getattr(self, merge_attrib, []):
            for x in merged[1]:
                if ordereddict.__contains__(self, x):
                    continue
                for y in done:
                    if x in y:
                        break
                else:
                    yield ordereddict.__getitem__(merged[1], x)
            done.append(merged[1])

    if PY2:
        def values(self):
            # type: () -> Any
            return list(self._values())

        def itervalues(self):
            # type: () -> Any
            return self._values()

        def viewvalues(self):
            # type: () -> Any
            return CommentedMapValuesView(self)
    else:
        def values(self):
            # type: () -> Any
            return CommentedMapValuesView(self)

    def _items(self):
        # type: () -> Any
        for x in ordereddict.__iter__(self):
            yield x, ordereddict.__getitem__(self, x)
        done = []  # type: List[Any]  # list of processed merge items, kept for masking
        for merged in getattr(self, merge_attrib, []):
            for x, v in merged[1].items():
                if ordereddict.__contains__(self, x):
                    continue
                for y in done:
                    if x in y:
                        break
                else:
                    yield x, v  # ordereddict.__getitem__(merged[1], x)
            done.append(merged[1])

    if PY2:
        def items(self):
            # type: () -> Any
            return list(self._items())

        def iteritems(self):
            # type: () -> Any
            return self._items()

        def viewitems(self):
            # type: () -> Any
            return CommentedMapItemsView(self)
    else:
        def items(self):
            # type: () -> Any
            return CommentedMapItemsView(self)

    @property
    def merge(self):
        # type: () -> Any
        if not hasattr(self, merge_attrib):
            setattr(self, merge_attrib, [])
        return getattr(self, merge_attrib)

    def add_yaml_merge(self, value):
        # type: (Any) -> None
        self.merge.extend(value)

    def __deepcopy__(self, memo):
        # type: (Any) -> Any
        res = self.__class__()
        memo[id(self)] = res
        for k in self:
            res[k] = copy.deepcopy(self[k])
            self.copy_attributes(res, deep=True)
        return res


class CommentedOrderedMap(CommentedMap):
    __slots__ = Comment.attrib,


class CommentedSet(MutableSet, CommentedMap):
    __slots__ = Comment.attrib, 'odict',

    def __init__(self, values=None):
        # type: (Any) -> None
        self.odict = ordereddict()
        MutableSet.__init__(self)
        if values is not None:
            self |= values  # type: ignore

    def add(self, value):
        # type: (Any) -> None
        """Add an element."""
        self.odict[value] = None

    def discard(self, value):
        # type: (Any) -> None
        """Remove an element.  Do not raise an exception if absent."""
        del self.odict[value]

    def __contains__(self, x):
        # type: (Any) -> Any
        return x in self.odict

    def __iter__(self):
        # type: () -> Any
        for x in self.odict:
            yield x

    def __len__(self):
        # type: () -> int
        return len(self.odict)

    def __repr__(self):
        # type: () -> str
        return 'set({0!r})'.format(self.odict.keys())


class TaggedScalar(CommentedBase):
    # the value and style attributes are set during roundtrip construction
    def __str__(self):
        return self.value


def dump_comments(d, name='', sep='.', out=sys.stdout):
    # type: (Any, str, str, Any) -> None
    """
    recursively dump comments, all but the toplevel preceded by the path
    in dotted form x.0.a
    """
    if isinstance(d, dict) and hasattr(d, 'ca'):
        if name:
            print(name)
        print(d.ca, file=out)  # type: ignore
        for k in d:
            dump_comments(d[k], name=(name + sep + k) if name else k, sep=sep, out=out)
    elif isinstance(d, list) and hasattr(d, 'ca'):
        if name:
            print(name)
        print(d.ca, file=out)  # type: ignore
        for idx, k in enumerate(d):
            dump_comments(k, name=(name + sep + str(idx)) if name else str(idx),
                          sep=sep, out=out)
