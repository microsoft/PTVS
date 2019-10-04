from __future__ import division, print_function, unicode_literals

from contextlib import contextmanager
from ctypes import cast, c_void_p, POINTER, create_string_buffer
from os import fstat, stat

from . import ffi
from .ffi import (ARCHIVE_EOF, OPEN_CALLBACK, READ_CALLBACK, CLOSE_CALLBACK,
                  VOID_CB, page_size)
from .entry import ArchiveEntry, new_archive_entry


class ArchiveRead(object):

    def __init__(self, archive_p):
        self._pointer = archive_p

    def __iter__(self):
        """Iterates through an archive's entries.
        """
        archive_p = self._pointer
        read_next_header2 = ffi.read_next_header2
        with new_archive_entry() as entry_p:
            entry = ArchiveEntry(archive_p, entry_p)
            while 1:
                r = read_next_header2(archive_p, entry_p)
                if r == ARCHIVE_EOF:
                    return
                yield entry


@contextmanager
def new_archive_read(format_name='all', filter_name='all'):
    """Creates an archive struct suitable for reading from an archive.

    Returns a pointer if successful. Raises ArchiveError on error.
    """
    archive_p = ffi.read_new()
    getattr(ffi, 'read_support_filter_'+filter_name)(archive_p)
    getattr(ffi, 'read_support_format_'+format_name)(archive_p)
    try:
        yield archive_p
    finally:
        ffi.read_free(archive_p)


@contextmanager
def custom_reader(
        read_func, format_name='all', filter_name='all',
        open_func=VOID_CB, close_func=VOID_CB, block_size=page_size,
        archive_read_class=ArchiveRead
):
    """Read an archive using a custom function.
    """
    open_cb = OPEN_CALLBACK(open_func)
    read_cb = READ_CALLBACK(read_func)
    close_cb = CLOSE_CALLBACK(close_func)
    with new_archive_read(format_name, filter_name) as archive_p:
        ffi.read_open(archive_p, None, open_cb, read_cb, close_cb)
        yield archive_read_class(archive_p)


@contextmanager
def fd_reader(fd, format_name='all', filter_name='all', block_size=4096):
    """Read an archive from a file descriptor.
    """
    with new_archive_read(format_name, filter_name) as archive_p:
        try:
            block_size = fstat(fd).st_blksize
        except (OSError, AttributeError):  # pragma: no cover
            pass
        ffi.read_open_fd(archive_p, fd, block_size)
        yield ArchiveRead(archive_p)


@contextmanager
def file_reader(path, format_name='all', filter_name='all', block_size=4096):
    """Read an archive from a file.
    """
    with new_archive_read(format_name, filter_name) as archive_p:
        try:
            block_size = stat(path).st_blksize
        except (OSError, AttributeError):  # pragma: no cover
            pass
        ffi.read_open_filename_w(archive_p, path, block_size)
        yield ArchiveRead(archive_p)


@contextmanager
def memory_reader(buf, format_name='all', filter_name='all'):
    """Read an archive from memory.
    """
    with new_archive_read(format_name, filter_name) as archive_p:
        ffi.read_open_memory(archive_p, cast(buf, c_void_p), len(buf))
        yield ArchiveRead(archive_p)


@contextmanager
def stream_reader(stream, format_name='all', filter_name='all',
                  block_size=page_size):
    """Read an archive from a stream.

    The `stream` object must support the standard `readinto` method.
    """
    buf = create_string_buffer(block_size)
    buf_p = cast(buf, c_void_p)

    def read_func(archive_p, context, ptrptr):
        # readinto the buffer, returns number of bytes read
        length = stream.readinto(buf)
        # write the address of the buffer into the pointer
        ptrptr = cast(ptrptr, POINTER(c_void_p))
        ptrptr[0] = buf_p
        # tell libarchive how much data was written into the buffer
        return length

    open_cb = OPEN_CALLBACK(VOID_CB)
    read_cb = READ_CALLBACK(read_func)
    close_cb = CLOSE_CALLBACK(VOID_CB)
    with new_archive_read(format_name, filter_name) as archive_p:
        ffi.read_open(archive_p, None, open_cb, read_cb, close_cb)
        yield ArchiveRead(archive_p)
