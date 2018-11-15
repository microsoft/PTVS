# (c) 2012-2016 Anaconda, Inc. / https://anaconda.com
# All Rights Reserved
#
# conda is distributed under the terms of the BSD 3-clause license.
# Consult LICENSE.txt or http://opensource.org/licenses/BSD-3-Clause.
'''
We use the following conventions in this module:

    dist:        canonical package name, e.g. 'numpy-1.6.2-py26_0'

    ROOT_PREFIX: the prefix to the root environment, e.g. /opt/anaconda

    PKGS_DIR:    the "package cache directory", e.g. '/opt/anaconda/pkgs'
                 this is always equal to ROOT_PREFIX/pkgs

    prefix:      the prefix of a particular environment, which may also
                 be the root environment

Also, this module is directly invoked by the (self extracting) tarball
installer to create the initial environment, therefore it needs to be
standalone, i.e. not import any other parts of `conda` (only depend on
the standard library).
'''
import os
import re
import sys
import json
import shutil
import stat
from os.path import abspath, dirname, exists, isdir, isfile, islink, join
from optparse import OptionParser


on_win = bool(sys.platform == 'win32')
try:
    FORCE = bool(int(os.getenv('FORCE', 0)))
except ValueError:
    FORCE = False

LINK_HARD = 1
LINK_SOFT = 2  # never used during the install process
LINK_COPY = 3
link_name_map = {
    LINK_HARD: 'hard-link',
    LINK_SOFT: 'soft-link',
    LINK_COPY: 'copy',
}
SPECIAL_ASCII = '$!&\%^|{}[]<>~`"\':;?@*#'

# these may be changed in main()
ROOT_PREFIX = sys.prefix
PKGS_DIR = join(ROOT_PREFIX, 'pkgs')
SKIP_SCRIPTS = False
IDISTS = {
  "asn1crypto-0.24.0-py37_0": {
    "md5": "3a4916054f7d730b17e5d8a4343d9b59",
    "url": "https://repo.anaconda.com/pkgs/main/win-64/asn1crypto-0.24.0-py37_0.tar.bz2"
  },
  "ca-certificates-2018.03.07-0": {
    "md5": "df2f346c2e8351372e6935ae6654da75",
    "url": "https://repo.anaconda.com/pkgs/main/win-64/ca-certificates-2018.03.07-0.tar.bz2"
  },
  "certifi-2018.8.24-py37_1": {
    "md5": "062f4f3edee9699b2e610d1365dab1ee",
    "url": "https://repo.anaconda.com/pkgs/main/win-64/certifi-2018.8.24-py37_1.tar.bz2"
  },
  "cffi-1.11.5-py37h74b6da3_1": {
    "md5": "4ce1b5165b847a849c6c1331c589b29d",
    "url": "https://repo.anaconda.com/pkgs/main/win-64/cffi-1.11.5-py37h74b6da3_1.tar.bz2"
  },
  "chardet-3.0.4-py37_1": {
    "md5": "fa614ba863adbffcc5d5d449a8a19add",
    "url": "https://repo.anaconda.com/pkgs/main/win-64/chardet-3.0.4-py37_1.tar.bz2"
  },
  "conda-4.5.11-py37_0": {
    "md5": "3fdae9626af9eba05c5f2ad26678fcfc",
    "url": "https://repo.anaconda.com/pkgs/main/win-64/conda-4.5.11-py37_0.tar.bz2"
  },
  "conda-env-2.6.0-1": {
    "md5": "0f615892c0a17b46e5619deeb0103b5b",
    "url": "https://repo.anaconda.com/pkgs/main/win-64/conda-env-2.6.0-1.tar.bz2"
  },
  "console_shortcut-0.1.1-3": {
    "md5": "aaad91749d7bb128bd51c0df273285e6",
    "url": "https://repo.anaconda.com/pkgs/main/win-64/console_shortcut-0.1.1-3.tar.bz2"
  },
  "cryptography-2.3.1-py37h74b6da3_0": {
    "md5": "ee9f68380a21d76c3727768ecbdd6f8c",
    "url": "https://repo.anaconda.com/pkgs/main/win-64/cryptography-2.3.1-py37h74b6da3_0.tar.bz2"
  },
  "idna-2.7-py37_0": {
    "md5": "081c44c8c49ff7b0600712ce3ff61213",
    "url": "https://repo.anaconda.com/pkgs/main/win-64/idna-2.7-py37_0.tar.bz2"
  },
  "menuinst-1.4.14-py37hfa6e2cd_0": {
    "md5": "afab8d32432bfb633348196c7ded8a9d",
    "url": "https://repo.anaconda.com/pkgs/main/win-64/menuinst-1.4.14-py37hfa6e2cd_0.tar.bz2"
  },
  "openssl-1.0.2p-hfa6e2cd_0": {
    "md5": "dd8f54d03bbe425eca55eca0bc0a174c",
    "url": "https://repo.anaconda.com/pkgs/main/win-64/openssl-1.0.2p-hfa6e2cd_0.tar.bz2"
  },
  "pip-10.0.1-py37_0": {
    "md5": "4c8224736f0830493d1ce89f9dc0a986",
    "url": "https://repo.anaconda.com/pkgs/main/win-64/pip-10.0.1-py37_0.tar.bz2"
  },
  "pycosat-0.6.3-py37hfa6e2cd_0": {
    "md5": "0286cada8369b363e53969d840b58f27",
    "url": "https://repo.anaconda.com/pkgs/main/win-64/pycosat-0.6.3-py37hfa6e2cd_0.tar.bz2"
  },
  "pycparser-2.18-py37_1": {
    "md5": "f3299371d54441828998620b9692598f",
    "url": "https://repo.anaconda.com/pkgs/main/win-64/pycparser-2.18-py37_1.tar.bz2"
  },
  "pyopenssl-18.0.0-py37_0": {
    "md5": "39d9e77bee35dcbf59e9a97cd9d1e393",
    "url": "https://repo.anaconda.com/pkgs/main/win-64/pyopenssl-18.0.0-py37_0.tar.bz2"
  },
  "pysocks-1.6.8-py37_0": {
    "md5": "dc3eb2cfe5f40ca5984fe7164c8e312b",
    "url": "https://repo.anaconda.com/pkgs/main/win-64/pysocks-1.6.8-py37_0.tar.bz2"
  },
  "python-3.7.0-hea74fb7_0": {
    "md5": "0d32017fe32d896f49629ff1ca68e399",
    "url": "https://repo.anaconda.com/pkgs/main/win-64/python-3.7.0-hea74fb7_0.tar.bz2"
  },
  "pywin32-223-py37hfa6e2cd_1": {
    "md5": "f109e28513833e67c4e0ef4a5cdb9cae",
    "url": "https://repo.anaconda.com/pkgs/main/win-64/pywin32-223-py37hfa6e2cd_1.tar.bz2"
  },
  "requests-2.19.1-py37_0": {
    "md5": "519225535d52bb7910d0e49fde6a11de",
    "url": "https://repo.anaconda.com/pkgs/main/win-64/requests-2.19.1-py37_0.tar.bz2"
  },
  "ruamel_yaml-0.15.46-py37hfa6e2cd_0": {
    "md5": "163e6e1230812be0d46c0b8a0e1734ff",
    "url": "https://repo.anaconda.com/pkgs/main/win-64/ruamel_yaml-0.15.46-py37hfa6e2cd_0.tar.bz2"
  },
  "setuptools-40.2.0-py37_0": {
    "md5": "e8a2d1732e2144f3e15824e8fbd1e8bf",
    "url": "https://repo.anaconda.com/pkgs/main/win-64/setuptools-40.2.0-py37_0.tar.bz2"
  },
  "six-1.11.0-py37_1": {
    "md5": "435aef93d101e41414f86ec745eb1ac9",
    "url": "https://repo.anaconda.com/pkgs/main/win-64/six-1.11.0-py37_1.tar.bz2"
  },
  "urllib3-1.23-py37_0": {
    "md5": "d46f406c8e0c408488bb76bc04edf289",
    "url": "https://repo.anaconda.com/pkgs/main/win-64/urllib3-1.23-py37_0.tar.bz2"
  },
  "vc-14-h0510ff6_3": {
    "md5": "e9af5df0ce656124ba7bbf42ec403113",
    "url": "https://repo.anaconda.com/pkgs/main/win-64/vc-14-h0510ff6_3.tar.bz2"
  },
  "vs2015_runtime-14.0.25123-3": {
    "md5": "8fe8b5873f5cc872cfc58e782ae1a008",
    "url": "https://repo.anaconda.com/pkgs/main/win-64/vs2015_runtime-14.0.25123-3.tar.bz2"
  },
  "wheel-0.31.1-py37_0": {
    "md5": "61b4226dfffc3902caee1d549810af7b",
    "url": "https://repo.anaconda.com/pkgs/main/win-64/wheel-0.31.1-py37_0.tar.bz2"
  },
  "win_inet_pton-1.0.1-py37_1": {
    "md5": "d97eeb1bdc401c552a00e37fb065bdc7",
    "url": "https://repo.anaconda.com/pkgs/main/win-64/win_inet_pton-1.0.1-py37_1.tar.bz2"
  },
  "wincertstore-0.2-py37_0": {
    "md5": "0418cf653523e6ef0174c259efaa78b4",
    "url": "https://repo.anaconda.com/pkgs/main/win-64/wincertstore-0.2-py37_0.tar.bz2"
  },
  "yaml-0.1.7-hc54c509_2": {
    "md5": "eaeee418b8ac33d392ea03bb9b6f389d",
    "url": "https://repo.anaconda.com/pkgs/main/win-64/yaml-0.1.7-hc54c509_2.tar.bz2"
  }
}
C_ENVS = {
  "root": [
    "python-3.7.0-hea74fb7_0",
    "ca-certificates-2018.03.07-0",
    "conda-env-2.6.0-1",
    "vs2015_runtime-14.0.25123-3",
    "vc-14-h0510ff6_3",
    "openssl-1.0.2p-hfa6e2cd_0",
    "yaml-0.1.7-hc54c509_2",
    "asn1crypto-0.24.0-py37_0",
    "certifi-2018.8.24-py37_1",
    "chardet-3.0.4-py37_1",
    "console_shortcut-0.1.1-3",
    "idna-2.7-py37_0",
    "pycosat-0.6.3-py37hfa6e2cd_0",
    "pycparser-2.18-py37_1",
    "pywin32-223-py37hfa6e2cd_1",
    "ruamel_yaml-0.15.46-py37hfa6e2cd_0",
    "six-1.11.0-py37_1",
    "win_inet_pton-1.0.1-py37_1",
    "wincertstore-0.2-py37_0",
    "cffi-1.11.5-py37h74b6da3_1",
    "menuinst-1.4.14-py37hfa6e2cd_0",
    "pysocks-1.6.8-py37_0",
    "setuptools-40.2.0-py37_0",
    "cryptography-2.3.1-py37h74b6da3_0",
    "wheel-0.31.1-py37_0",
    "pip-10.0.1-py37_0",
    "pyopenssl-18.0.0-py37_0",
    "urllib3-1.23-py37_0",
    "requests-2.19.1-py37_0",
    "conda-4.5.11-py37_0"
  ]
}



def _link(src, dst, linktype=LINK_HARD):
    if linktype == LINK_HARD:
        if on_win:
            from ctypes import windll, wintypes
            CreateHardLink = windll.kernel32.CreateHardLinkW
            CreateHardLink.restype = wintypes.BOOL
            CreateHardLink.argtypes = [wintypes.LPCWSTR, wintypes.LPCWSTR,
                                       wintypes.LPVOID]
            if not CreateHardLink(dst, src, None):
                raise OSError('win32 hard link failed')
        else:
            os.link(src, dst)
    elif linktype == LINK_COPY:
        # copy relative symlinks as symlinks
        if islink(src) and not os.readlink(src).startswith(os.path.sep):
            os.symlink(os.readlink(src), dst)
        else:
            shutil.copy2(src, dst)
    else:
        raise Exception("Did not expect linktype=%r" % linktype)


def rm_rf(path):
    """
    try to delete path, but never fail
    """
    try:
        if islink(path) or isfile(path):
            # Note that we have to check if the destination is a link because
            # exists('/path/to/dead-link') will return False, although
            # islink('/path/to/dead-link') is True.
            os.unlink(path)
        elif isdir(path):
            shutil.rmtree(path)
    except (OSError, IOError):
        pass


def yield_lines(path):
    for line in open(path):
        line = line.strip()
        if not line or line.startswith('#'):
            continue
        yield line


prefix_placeholder = ('/opt/anaconda1anaconda2'
                      # this is intentionally split into parts,
                      # such that running this program on itself
                      # will leave it unchanged
                      'anaconda3')

def read_has_prefix(path):
    """
    reads `has_prefix` file and return dict mapping filenames to
    tuples(placeholder, mode)
    """
    import shlex

    res = {}
    try:
        for line in yield_lines(path):
            try:
                parts = [x.strip('"\'') for x in shlex.split(line, posix=False)]
                # assumption: placeholder and mode will never have a space
                placeholder, mode, f = parts[0], parts[1], ' '.join(parts[2:])
                res[f] = (placeholder, mode)
            except (ValueError, IndexError):
                res[line] = (prefix_placeholder, 'text')
    except IOError:
        pass
    return res


def exp_backoff_fn(fn, *args):
    """
    for retrying file operations that fail on Windows due to virus scanners
    """
    if not on_win:
        return fn(*args)

    import time
    import errno
    max_tries = 6  # max total time = 6.4 sec
    for n in range(max_tries):
        try:
            result = fn(*args)
        except (OSError, IOError) as e:
            if e.errno in (errno.EPERM, errno.EACCES):
                if n == max_tries - 1:
                    raise Exception("max_tries=%d reached" % max_tries)
                time.sleep(0.1 * (2 ** n))
            else:
                raise e
        else:
            return result


class PaddingError(Exception):
    pass


def binary_replace(data, a, b):
    """
    Perform a binary replacement of `data`, where the placeholder `a` is
    replaced with `b` and the remaining string is padded with null characters.
    All input arguments are expected to be bytes objects.
    """
    def replace(match):
        occurances = match.group().count(a)
        padding = (len(a) - len(b)) * occurances
        if padding < 0:
            raise PaddingError(a, b, padding)
        return match.group().replace(a, b) + b'\0' * padding

    pat = re.compile(re.escape(a) + b'([^\0]*?)\0')
    res = pat.sub(replace, data)
    assert len(res) == len(data)
    return res


def update_prefix(path, new_prefix, placeholder, mode):
    if on_win:
        # force all prefix replacements to forward slashes to simplify need
        # to escape backslashes - replace with unix-style path separators
        new_prefix = new_prefix.replace('\\', '/')

    path = os.path.realpath(path)
    with open(path, 'rb') as fi:
        data = fi.read()
    if mode == 'text':
        new_data = data.replace(placeholder.encode('utf-8'),
                                new_prefix.encode('utf-8'))
    elif mode == 'binary':
        if on_win:
            # anaconda-verify will not allow binary placeholder on Windows.
            # However, since some packages might be created wrong (and a
            # binary placeholder would break the package, we just skip here.
            return
        new_data = binary_replace(data, placeholder.encode('utf-8'),
                                  new_prefix.encode('utf-8'))
    else:
        sys.exit("Invalid mode:" % mode)

    if new_data == data:
        return
    st = os.lstat(path)
    # unlink in case the file is memory mapped
    exp_backoff_fn(os.unlink, path)
    with open(path, 'wb') as fo:
        fo.write(new_data)
    os.chmod(path, stat.S_IMODE(st.st_mode))


def name_dist(dist):
    if hasattr(dist, 'name'):
        return dist.name
    else:
        return dist.rsplit('-', 2)[0]


def create_meta(prefix, dist, info_dir, extra_info):
    """
    Create the conda metadata, in a given prefix, for a given package.
    """
    # read info/index.json first
    with open(join(info_dir, 'index.json')) as fi:
        meta = json.load(fi)
    # add extra info
    meta.update(extra_info)
    # write into <prefix>/conda-meta/<dist>.json
    meta_dir = join(prefix, 'conda-meta')
    if not isdir(meta_dir):
        os.makedirs(meta_dir)
    with open(join(meta_dir, dist + '.json'), 'w') as fo:
        json.dump(meta, fo, indent=2, sort_keys=True)


def run_script(prefix, dist, action='post-link'):
    """
    call the post-link (or pre-unlink) script, and return True on success,
    False on failure
    """
    path = join(prefix, 'Scripts' if on_win else 'bin', '.%s-%s.%s' % (
            name_dist(dist),
            action,
            'bat' if on_win else 'sh'))
    if not isfile(path):
        return True
    if SKIP_SCRIPTS:
        print("WARNING: skipping %s script by user request" % action)
        return True

    if on_win:
        try:
            args = [os.environ['COMSPEC'], '/c', path]
        except KeyError:
            return False
    else:
        shell_path = '/bin/sh' if 'bsd' in sys.platform else '/bin/bash'
        args = [shell_path, path]

    env = os.environ
    env['PREFIX'] = prefix

    import subprocess
    try:
        subprocess.check_call(args, env=env)
    except subprocess.CalledProcessError:
        return False
    return True


url_pat = re.compile(r'''
(?P<baseurl>\S+/)                 # base URL
(?P<fn>[^\s#/]+)                  # filename
([#](?P<md5>[0-9a-f]{32}))?       # optional MD5
$                                 # EOL
''', re.VERBOSE)

def read_urls(dist):
    try:
        data = open(join(PKGS_DIR, 'urls')).read()
        for line in data.split()[::-1]:
            m = url_pat.match(line)
            if m is None:
                continue
            if m.group('fn') == '%s.tar.bz2' % dist:
                return {'url': m.group('baseurl') + m.group('fn'),
                        'md5': m.group('md5')}
    except IOError:
        pass
    return {}


def read_no_link(info_dir):
    res = set()
    for fn in 'no_link', 'no_softlink':
        try:
            res.update(set(yield_lines(join(info_dir, fn))))
        except IOError:
            pass
    return res


def linked(prefix):
    """
    Return the (set of canonical names) of linked packages in prefix.
    """
    meta_dir = join(prefix, 'conda-meta')
    if not isdir(meta_dir):
        return set()
    return set(fn[:-5] for fn in os.listdir(meta_dir) if fn.endswith('.json'))


def link(prefix, dist, linktype=LINK_HARD, info_dir=None):
    '''
    Link a package in a specified prefix.  We assume that the packacge has
    been extra_info in either
      - <PKGS_DIR>/dist
      - <ROOT_PREFIX>/ (when the linktype is None)
    '''
    if linktype:
        source_dir = join(PKGS_DIR, dist)
        info_dir = join(source_dir, 'info')
        no_link = read_no_link(info_dir)
    else:
        info_dir = info_dir or join(prefix, 'info')

    files = list(yield_lines(join(info_dir, 'files')))
    # TODO: Use paths.json, if available or fall back to this method
    has_prefix_files = read_has_prefix(join(info_dir, 'has_prefix'))

    if linktype:
        for f in files:
            src = join(source_dir, f)
            dst = join(prefix, f)
            dst_dir = dirname(dst)
            if not isdir(dst_dir):
                os.makedirs(dst_dir)
            if exists(dst):
                if FORCE:
                    rm_rf(dst)
                else:
                    raise Exception("dst exists: %r" % dst)
            lt = linktype
            if f in has_prefix_files or f in no_link or islink(src):
                lt = LINK_COPY
            try:
                _link(src, dst, lt)
            except OSError:
                pass

    for f in sorted(has_prefix_files):
        placeholder, mode = has_prefix_files[f]
        try:
            update_prefix(join(prefix, f), prefix, placeholder, mode)
        except PaddingError:
            sys.exit("ERROR: placeholder '%s' too short in: %s\n" %
                     (placeholder, dist))

    if not run_script(prefix, dist, 'post-link'):
        sys.exit("Error: post-link failed for: %s" % dist)

    meta = {
        'files': files,
        'link': ({'source': source_dir,
                  'type': link_name_map.get(linktype)}
                 if linktype else None),
    }
    try:    # add URL and MD5
        meta.update(IDISTS[dist])
    except KeyError:
        meta.update(read_urls(dist))
    meta['installed_by'] = 'Miniconda3-4.5.11-Windows-x86_64.exe'
    create_meta(prefix, dist, info_dir, meta)


def duplicates_to_remove(linked_dists, keep_dists):
    """
    Returns the (sorted) list of distributions to be removed, such that
    only one distribution (for each name) remains.  `keep_dists` is an
    interable of distributions (which are not allowed to be removed).
    """
    from collections import defaultdict

    keep_dists = set(keep_dists)
    ldists = defaultdict(set) # map names to set of distributions
    for dist in linked_dists:
        name = name_dist(dist)
        ldists[name].add(dist)

    res = set()
    for dists in ldists.values():
        # `dists` is the group of packages with the same name
        if len(dists) == 1:
            # if there is only one package, nothing has to be removed
            continue
        if dists & keep_dists:
            # if the group has packages which are have to be kept, we just
            # take the set of packages which are in group but not in the
            # ones which have to be kept
            res.update(dists - keep_dists)
        else:
            # otherwise, we take lowest (n-1) (sorted) packages
            res.update(sorted(dists)[:-1])
    return sorted(res)


def yield_idists():
    for line in open(join(PKGS_DIR, 'urls')):
        m = url_pat.match(line)
        if m:
            fn = m.group('fn')
            yield fn[:-8]


def remove_duplicates():
    idists = list(yield_idists())
    keep_files = set()
    for dist in idists:
        with open(join(ROOT_PREFIX, 'conda-meta', dist + '.json')) as fi:
            meta = json.load(fi)
        keep_files.update(meta['files'])

    for dist in duplicates_to_remove(linked(ROOT_PREFIX), idists):
        print("unlinking: %s" % dist)
        meta_path = join(ROOT_PREFIX, 'conda-meta', dist + '.json')
        with open(meta_path) as fi:
            meta = json.load(fi)
        for f in meta['files']:
            if f not in keep_files:
                rm_rf(join(ROOT_PREFIX, f))
        rm_rf(meta_path)


def determine_link_type_capability():
    src = join(PKGS_DIR, 'urls')
    dst = join(ROOT_PREFIX, '.hard-link')
    assert isfile(src), src
    assert not isfile(dst), dst
    try:
        _link(src, dst, LINK_HARD)
        linktype = LINK_HARD
    except OSError:
        linktype = LINK_COPY
    finally:
        rm_rf(dst)
    return linktype


def link_dist(dist, linktype=None):
    if not linktype:
        linktype = determine_link_type_capability()
    prefix = prefix_env('root')
    link(prefix, dist, linktype)


def link_idists():
    linktype = determine_link_type_capability()
    for env_name in sorted(C_ENVS):
        dists = C_ENVS[env_name]
        assert isinstance(dists, list)
        if len(dists) == 0:
            continue

        prefix = prefix_env(env_name)
        for dist in dists:
            assert dist in IDISTS
            link_dist(dist, linktype)

        for dist in duplicates_to_remove(linked(prefix), dists):
            meta_path = join(prefix, 'conda-meta', dist + '.json')
            print("WARNING: unlinking: %s" % meta_path)
            try:
                os.rename(meta_path, meta_path + '.bak')
            except OSError:
                rm_rf(meta_path)


def prefix_env(env_name):
    if env_name == 'root':
        return ROOT_PREFIX
    else:
        return join(ROOT_PREFIX, 'envs', env_name)


def post_extract(env_name='root'):
    """
    assuming that the package is extracted in the environment `env_name`,
    this function does everything link() does except the actual linking,
    i.e. update prefix files, run 'post-link', creates the conda metadata,
    and removed the info/ directory afterwards.
    """
    prefix = prefix_env(env_name)
    info_dir = join(prefix, 'info')
    with open(join(info_dir, 'index.json')) as fi:
        meta = json.load(fi)
    dist = '%(name)s-%(version)s-%(build)s' % meta
    if FORCE:
        run_script(prefix, dist, 'pre-unlink')
    link(prefix, dist, linktype=None)
    shutil.rmtree(info_dir)


def multi_post_extract():
    # This function is called when using the --multi option, when building
    # .pkg packages on OSX.  I tried avoiding this extra option by running
    # the post extract step on each individual package (like it is done for
    # the .sh and .exe installers), by adding a postinstall script to each
    # conda .pkg file, but this did not work as expected.  Running all the
    # post extracts at end is also faster and could be considered for the
    # other installer types as well.
    for dist in yield_idists():
        info_dir = join(ROOT_PREFIX, 'info', dist)
        with open(join(info_dir, 'index.json')) as fi:
            meta = json.load(fi)
        dist = '%(name)s-%(version)s-%(build)s' % meta
        link(ROOT_PREFIX, dist, linktype=None, info_dir=info_dir)


def main():
    global ROOT_PREFIX, PKGS_DIR

    p = OptionParser(description="conda link tool used by installers")

    p.add_option('--root-prefix',
                 action="store",
                 default=abspath(join(__file__, '..', '..')),
                 help="root prefix (defaults to %default)")

    p.add_option('--post',
                 action="store",
                 help="perform post extract (on a single package), "
                      "in environment NAME",
                 metavar='NAME')

    opts, args = p.parse_args()
    if args:
        p.error('no arguments expected')

    ROOT_PREFIX = opts.root_prefix.replace('//', '/')
    PKGS_DIR = join(ROOT_PREFIX, 'pkgs')

    if opts.post:
        post_extract(opts.post)
        return

    if FORCE:
        print("using -f (force) option")

    link_idists()


def main2():
    global SKIP_SCRIPTS, ROOT_PREFIX, PKGS_DIR

    p = OptionParser(description="conda post extract tool used by installers")

    p.add_option('--skip-scripts',
                 action="store_true",
                 help="skip running pre/post-link scripts")

    p.add_option('--rm-dup',
                 action="store_true",
                 help="remove duplicates")

    p.add_option('--multi',
                 action="store_true",
                 help="multi post extract usecase")

    p.add_option('--link-dist',
                 action="store",
                 default=None,
                 help="link dist")

    p.add_option('--root-prefix',
                 action="store",
                 default=abspath(join(__file__, '..', '..')),
                 help="root prefix (defaults to %default)")

    opts, args = p.parse_args()
    ROOT_PREFIX = opts.root_prefix.replace('//', '/')
    PKGS_DIR = join(ROOT_PREFIX, 'pkgs')

    if args:
        p.error('no arguments expected')

    if opts.skip_scripts:
        SKIP_SCRIPTS = True

    if opts.rm_dup:
        remove_duplicates()
        return

    if opts.multi:
        multi_post_extract()
        return

    if opts.link_dist:
        link_dist(opts.link_dist)
        return

    post_extract()


def warn_on_special_chrs():
    if on_win:
        return
    for c in SPECIAL_ASCII:
        if c in ROOT_PREFIX:
            print("WARNING: found '%s' in install prefix." % c)


if __name__ == '__main__':
    if IDISTS:
        main()
        warn_on_special_chrs()
    else: # common usecase
        main2()
