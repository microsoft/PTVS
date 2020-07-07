from __future__ import division, print_function, unicode_literals

from contextlib import contextmanager
from ctypes import byref, c_longlong, c_size_t, c_void_p

from .ffi import (
    write_disk_new, write_disk_set_options, write_free, write_header,
    read_data_block, write_data_block, write_finish_entry, ARCHIVE_EOF
)
from .read import fd_reader, file_reader, memory_reader


EXTRACT_OWNER = 0x0001
EXTRACT_PERM = 0x0002
EXTRACT_TIME = 0x0004
EXTRACT_NO_OVERWRITE = 0x0008
EXTRACT_UNLINK = 0x0010
EXTRACT_ACL = 0x0020
EXTRACT_FFLAGS = 0x0040
EXTRACT_XATTR = 0x0080
EXTRACT_SECURE_SYMLINKS = 0x0100
EXTRACT_SECURE_NODOTDOT = 0x0200
EXTRACT_NO_AUTODIR = 0x0400
EXTRACT_NO_OVERWRITE_NEWER = 0x0800
EXTRACT_SPARSE = 0x1000
EXTRACT_MAC_METADATA = 0x2000
EXTRACT_NO_HFS_COMPRESSION = 0x4000
EXTRACT_HFS_COMPRESSION_FORCED = 0x8000
EXTRACT_SECURE_NOABSOLUTEPATHS = 0x10000
EXTRACT_CLEAR_NOCHANGE_FFLAGS = 0x20000


@contextmanager
def new_archive_write_disk(flags):
    archive_p = write_disk_new()
    write_disk_set_options(archive_p, flags)
    try:
        yield archive_p
    finally:
        write_free(archive_p)


def extract_entries(entries, flags=0):
    """Extracts the given archive entries into the current directory.
    """
    buff, size, offset = c_void_p(), c_size_t(), c_longlong()
    buff_p, size_p, offset_p = byref(buff), byref(size), byref(offset)
    with new_archive_write_disk(flags) as write_p:
        for entry in entries:
            write_header(write_p, entry._entry_p)
            read_p = entry._archive_p
            while 1:
                r = read_data_block(read_p, buff_p, size_p, offset_p)
                if r == ARCHIVE_EOF:
                    break
                write_data_block(write_p, buff, size, offset)
            write_finish_entry(write_p)


def extract_fd(fd, flags=0):
    """Extracts an archive from a file descriptor into the current directory.
    """
    with fd_reader(fd) as archive:
        extract_entries(archive, flags)


def extract_file(filepath, flags=0):
    """Extracts an archive from a file into the current directory."""
    with file_reader(filepath) as archive:
        extract_entries(archive, flags)


def extract_memory(buffer_, flags=0):
    """Extracts an archive from memory into the current directory."""
    with memory_reader(buffer_) as archive:
        extract_entries(archive, flags)
