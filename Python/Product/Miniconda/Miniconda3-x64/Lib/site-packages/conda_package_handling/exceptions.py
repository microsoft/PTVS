from errno import ENOENT


class InvalidArchiveError(Exception):
    """Raised when libarchive can't open a file"""
    def __init__(self, fn, msg, *args, **kw):
        msg = ("Error with archive %s.  You probably need to delete and re-download "
               "or re-create this file.  Message from libarchive was:\n\n%s" % (fn, msg))
        self.errno = ENOENT
        super(InvalidArchiveError, self).__init__(msg)
