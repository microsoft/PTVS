from __future__ import division, print_function, unicode_literals

from contextlib import contextmanager
from ctypes import c_char_p, create_string_buffer

from . import ffi


@contextmanager
def new_archive_entry():
    entry_p = ffi.entry_new()
    try:
        yield entry_p
    finally:
        ffi.entry_free(entry_p)


def format_time(seconds, nanos):
    """ return float of seconds.nanos when nanos set, or seconds when not """
    if nanos:
        return float(seconds) + float(nanos) / 1000000000.0
    return int(seconds)


class ArchiveEntry(object):

    def __init__(self, archive_p, entry_p):
        self._archive_p = archive_p
        self._entry_p = entry_p

    def __str__(self):
        return self.pathname

    @property
    def filetype(self):
        return ffi.entry_filetype(self._entry_p)

    @property
    def uid(self):
        return ffi.entry_uid(self._entry_p)

    @property
    def gid(self):
        return ffi.entry_gid(self._entry_p)

    def get_blocks(self, block_size=ffi.page_size):
        archive_p = self._archive_p
        buf = create_string_buffer(block_size)
        read = ffi.read_data
        while 1:
            r = read(archive_p, buf, block_size)
            if r == 0:
                break
            yield buf.raw[0:r]

    @property
    def isblk(self):
        return self.filetype & 0o170000 == 0o060000

    @property
    def ischr(self):
        return self.filetype & 0o170000 == 0o020000

    @property
    def isdir(self):
        return self.filetype & 0o170000 == 0o040000

    @property
    def isfifo(self):
        return self.filetype & 0o170000 == 0o010000

    @property
    def islnk(self):
        return bool(ffi.entry_hardlink_w(self._entry_p) or
                    ffi.entry_hardlink(self._entry_p))

    @property
    def issym(self):
        return self.filetype & 0o170000 == 0o120000

    def _linkpath(self):
        return (ffi.entry_symlink_w(self._entry_p) or
                ffi.entry_hardlink_w(self._entry_p) or
                ffi.entry_symlink(self._entry_p) or
                ffi.entry_hardlink(self._entry_p))

    # aliases to get the same api as tarfile
    linkpath = property(_linkpath)
    linkname = property(_linkpath)

    @property
    def isreg(self):
        return self.filetype & 0o170000 == 0o100000

    @property
    def isfile(self):
        return self.isreg

    @property
    def issock(self):
        return self.filetype & 0o170000 == 0o140000

    @property
    def isdev(self):
        return self.ischr or self.isblk or self.isfifo or self.issock

    @property
    def atime(self):
        sec_val = ffi.entry_atime(self._entry_p)
        nsec_val = ffi.entry_atime_nsec(self._entry_p)
        return format_time(sec_val, nsec_val)

    def set_atime(self, timestamp_sec, timestamp_nsec):
        return ffi.entry_set_atime(self._entry_p,
                                   timestamp_sec, timestamp_nsec)

    @property
    def mtime(self):
        sec_val = ffi.entry_mtime(self._entry_p)
        nsec_val = ffi.entry_mtime_nsec(self._entry_p)
        return format_time(sec_val, nsec_val)

    def set_mtime(self, timestamp_sec, timestamp_nsec):
        return ffi.entry_set_mtime(self._entry_p,
                                   timestamp_sec, timestamp_nsec)

    @property
    def ctime(self):
        sec_val = ffi.entry_ctime(self._entry_p)
        nsec_val = ffi.entry_ctime_nsec(self._entry_p)
        return format_time(sec_val, nsec_val)

    def set_ctime(self, timestamp_sec, timestamp_nsec):
        return ffi.entry_set_ctime(self._entry_p,
                                   timestamp_sec, timestamp_nsec)

    @property
    def birthtime(self):
        sec_val = ffi.entry_birthtime(self._entry_p)
        nsec_val = ffi.entry_birthtime_nsec(self._entry_p)
        return format_time(sec_val, nsec_val)

    def set_birthtime(self, timestamp_sec, timestamp_nsec):
        return ffi.entry_set_birthtime(self._entry_p,
                                       timestamp_sec, timestamp_nsec)

    def _getpathname(self):
        return (ffi.entry_pathname_w(self._entry_p) or
                ffi.entry_pathname(self._entry_p))

    def _setpathname(self, value):
        if not isinstance(value, bytes):
            value = value.encode('utf8')
        ffi.entry_update_pathname_utf8(self._entry_p, c_char_p(value))

    pathname = property(_getpathname, _setpathname)
    # aliases to get the same api as tarfile
    path = property(_getpathname, _setpathname)
    name = property(_getpathname, _setpathname)

    @property
    def size(self):
        if ffi.entry_size_is_set(self._entry_p):
            return ffi.entry_size(self._entry_p)

    @property
    def mode(self):
        return ffi.entry_mode(self._entry_p)

    @property
    def strmode(self):
        # note we strip the mode because archive_entry_strmode
        # returns a trailing space: strcpy(bp, "?rwxrwxrwx ");
        return ffi.entry_strmode(self._entry_p).strip()

    @property
    def rdevmajor(self):
        return ffi.entry_rdevmajor(self._entry_p)

    @property
    def rdevminor(self):
        return ffi.entry_rdevminor(self._entry_p)
