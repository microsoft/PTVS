 # ############################################################################
 #
 # Copyright (c) Microsoft Corporation.
 #
 # This source code is subject to terms and conditions of the Apache License, Version 2.0. A
 # copy of the license can be found in the License.html file at the root of this distribution. If
 # you cannot locate the Apache License, Version 2.0, please send an email to
 # vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound
 # by the terms of the Apache License, Version 2.0.
 #
 # You must not remove this notice, or any other, from this software.
 #
 # ###########################################################################

import os.path
import subprocess
import shutil
import sys
import tarfile
import tempfile

MAJOR_VERSION = sys.version_info[:2]
EXECUTABLE = [sys.executable]
if sys.platform == 'cli' and hasattr(sys, '_getframe'):
    EXECUTABLE.append('-X:Frames')

if MAJOR_VERSION == (3, 1):
    from urllib.request import urlopen
    def urlretrieve(url, filename):
        fobj = None
        uobj = urlopen(url)
        try:
            fobj = open(filename, 'wb')
            fobj.write(uobj.readall())
        finally:
            uobj.close()
            if fobj:
                fobj.close()
        return filename, None
else:
    try:
        from urllib.request import urlretrieve
    except ImportError:
        from urllib import urlretrieve

def install_from_source(setuptools_source, pip_source):
    setuptools_temp_dir = tempfile.mkdtemp('-setuptools', 'ptvs-')
    pip_temp_dir = tempfile.mkdtemp('-pip', 'ptvs-')
    cwd = os.getcwd()

    try:
        os.chdir(setuptools_temp_dir)
        print('Downloading setuptools from ' + setuptools_source)
        sys.stdout.flush()
        setuptools_package, _ = urlretrieve(setuptools_source, 'setuptools.tar.gz')

        package = tarfile.open(setuptools_package)
        try:
            safe_members = [m for m in package.getmembers() if not m.name.startswith(('..', '\\'))]
            package.extractall(setuptools_temp_dir, members=safe_members)
        finally:
            package.close()

        extracted_dirs = [d for d in os.listdir(setuptools_temp_dir) if os.path.exists(os.path.join(d, 'setup.py'))]
        if not extracted_dirs:
            raise OSError("Failed to find setuptools's setup.py")
        extracted_dir = extracted_dirs[0]

        print('\nInstalling from ' + extracted_dir)
        sys.stdout.flush()
        os.chdir(extracted_dir)
        subprocess.check_call(
            EXECUTABLE + ['setup.py', 'install', '--single-version-externally-managed', '--record', 'setuptools.txt']
        )

        os.chdir(pip_temp_dir)
        print('Downloading pip from ' + pip_source)
        sys.stdout.flush()
        pip_package, _ = urlretrieve(pip_source, 'pip.tar.gz')

        package = tarfile.open(pip_package)
        try:
            safe_members = [m for m in package.getmembers() if not m.name.startswith(('..', '\\'))]
            package.extractall(pip_temp_dir, members=safe_members)
        finally:
            package.close()

        extracted_dirs = [d for d in os.listdir(pip_temp_dir) if os.path.exists(os.path.join(d, 'setup.py'))]
        if not extracted_dirs:
            raise OSError("Failed to find pip's setup.py")
        extracted_dir = extracted_dirs[0]

        print('\nInstalling from ' + extracted_dir)
        sys.stdout.flush()
        os.chdir(extracted_dir)
        subprocess.check_call(
            EXECUTABLE + ['setup.py', 'install', '--single-version-externally-managed', '--record', 'pip.txt']
        )

        print('\nInstallation Complete')
        sys.stdout.flush()
    finally:
        os.chdir(cwd)
        shutil.rmtree(setuptools_temp_dir, ignore_errors=True)
        shutil.rmtree(pip_temp_dir, ignore_errors=True)

def install_from_pip(getpip_url):
    pip_temp_dir = tempfile.mkdtemp('-pip', 'ptvs-')

    try:
        print('Downloading pip from ' + getpip_url)
        sys.stdout.flush()
        pip_script, _ = urlretrieve(getpip_url, os.path.join(pip_temp_dir, 'get-pip.py'))

        print('\nInstalling from ' + pip_script)
        sys.stdout.flush()

        subprocess.check_call(EXECUTABLE + [pip_script])

        print('\nInstallation Complete')
        sys.stdout.flush()
    finally:
        shutil.rmtree(pip_temp_dir, ignore_errors=True)

def install_from_ensurepip(ensurepip):
    print('Installing with ensurepip')
    sys.stdout.flush()

    # We can bootstrap with ensurepip, but then have to upgrade to the latest
    ensurepip.bootstrap(upgrade=True, default_pip=True)

    subprocess.check_call(
        EXECUTABLE + ["-m", "pip", "install", "-U", "pip", "setuptools", "wheel"]
    )

    print('\nInstallation Complete')
    sys.stdout.flush()

def main():
    try:
        import ensurepip
    except ImportError:
        pass
    else:
        try:
            install_from_ensurepip(ensurepip)
            return
        except Exception:
            if sys.platform == 'cli':
                print('\nFailed to upgrade pip, which is probably because of IronPython. Leaving the earlier version.')
                return
            print("\nFailed to upgrade pip, which probably indicates that it isn't installed properly.")

    if MAJOR_VERSION < (2, 5):
        print('Python versions earlier than 2.5 are not supported by PTVS.')
        return -1

    if MAJOR_VERSION == (3, 0):
        print('Python 3.0 is not supported by pip and setuptools')
        return -2

    if MAJOR_VERSION == (2, 5):
        install_from_source(
            'http://go.microsoft.com/fwlink/?LinkId=317602',
            'http://go.microsoft.com/fwlink/?LinkId=313647',
        )
        return

    if MAJOR_VERSION == (3, 1):
        install_from_source(
            'http://go.microsoft.com/fwlink/?LinkId=616616',
            'http://go.microsoft.com/fwlink/?LinkID=616614',
        )
        return

    try:
        install_from_pip('https://go.microsoft.com/fwlink/?LinkId=616663')
    except Exception:
        pass
    else:
        return

    print('\nFailed to install. Attempting direct download.')
    install_from_source(
        'http://go.microsoft.com/fwlink/?LinkId=317603',
        'http://go.microsoft.com/fwlink/?LinkId=317604',
    )

def _restart_with_x_frames():
    if '--no-ipy-restart' in sys.argv:
        print('-X:Frames failed to add _getframe method. Aborting')
        return -3
    print('Restarting IronPython with -X:Frames')
    sys.stdout.flush()
    return subprocess.call([sys.executable, '-X:Frames', __file__, '--no-ipy-restart'])

if __name__ == '__main__':
    if sys.platform == 'cli' and not hasattr(sys, '_getframe'):
        sys.exit(_restart_with_x_frames())

    try:
        import pip
    except ImportError:
        pass
    else:
        print('pip is already available.')
        sys.exit(0)

    sys.exit(int(main() or 0))
