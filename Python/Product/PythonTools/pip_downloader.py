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

def install_from_source_file(name, tar_file, temp_dir):
        package = tarfile.open(tar_file)
        try:
            safe_members = [m for m in package.getmembers() if not m.name.startswith(('..', '\\'))]
            package.extractall(temp_dir, members=safe_members)
        finally:
            package.close()

        extracted_dirs = [d for d in os.listdir(temp_dir) if os.path.isfile(os.path.join(temp_dir, d, 'setup.py'))]
        if not extracted_dirs:
            raise OSError("Failed to find " + name + "'s setup.py")
        extracted_dir = extracted_dirs[0]

        print('\nInstalling from ' + extracted_dir)
        sys.stdout.flush()
        cwd = os.getcwd()
        try:
            os.chdir(os.path.join(temp_dir, extracted_dir))
            subprocess.check_call(
                EXECUTABLE + ['setup.py', 'install', '--single-version-externally-managed', '--record', name + '.txt']
            )
        finally:
            os.chdir(cwd)

def install_from_local_source():
    cwd = os.getcwd()
    setuptools_package = os.path.join(cwd, 'setuptools.tar.gz')
    pip_package = os.path.join(cwd, 'pip.tar.gz')
    if not os.path.isfile(setuptools_package):
        print('Did not find ' + setuptools_package)
        raise ValueError('setuptools.tar.gz not found')
    if not os.path.isfile(pip_package):
        print('Did not find ' + pip_package)
        raise ValueError('pip.tar.gz not found')
    
    setuptools_temp_dir = tempfile.mkdtemp('-setuptools', 'ptvs-')
    pip_temp_dir = tempfile.mkdtemp('-pip', 'ptvs-')

    try:
        install_from_source_file('setuptools', setuptools_package, setuptools_temp_dir)
        install_from_source_file('pip', pip_package, pip_temp_dir)

        print('\nInstallation Complete')
        sys.stdout.flush()
    finally:
        os.chdir(cwd)
        shutil.rmtree(setuptools_temp_dir, ignore_errors=True)
        shutil.rmtree(pip_temp_dir, ignore_errors=True)

def install_from_source(setuptools_source, pip_source):
    setuptools_temp_dir = tempfile.mkdtemp('-setuptools', 'ptvs-')
    pip_temp_dir = tempfile.mkdtemp('-pip', 'ptvs-')
    cwd = os.getcwd()

    try:
        os.chdir(setuptools_temp_dir)
        print('Downloading setuptools from ' + setuptools_source)
        sys.stdout.flush()
        setuptools_package, _ = urlretrieve(setuptools_source, 'setuptools.tar.gz')

        install_from_source_file('setuptools', setuptools_package, setuptools_temp_dir)

        os.chdir(pip_temp_dir)
        print('Downloading pip from ' + pip_source)
        sys.stdout.flush()
        pip_package, _ = urlretrieve(pip_source, 'pip.tar.gz')

        install_from_source_file('pip', pip_package, pip_temp_dir)

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

    # Latest version doesn't work on IronPython, so don't upgrade
    if sys.platform != 'cli':
        subprocess.check_call(
            EXECUTABLE + ["-m", "pip", "install", "-U", "pip", "setuptools", "wheel"]
        )

    print('\nInstallation Complete')
    sys.stdout.flush()

def main():
    try:
        install_from_local_source()
    except Exception:
        pass
    
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
            'https://go.microsoft.com/fwlink/?LinkId=317602',
            'https://go.microsoft.com/fwlink/?LinkId=313647',
        )
        return

    if MAJOR_VERSION == (2, 6):
        install_from_pip('https://go.microsoft.com/fwlink/?linkid=874551')
        return

    if MAJOR_VERSION == (3, 1):
        install_from_source(
            'https://go.microsoft.com/fwlink/?LinkId=616616',
            'https://go.microsoft.com/fwlink/?LinkID=616614',
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
        'https://go.microsoft.com/fwlink/?LinkId=317603',
        'https://go.microsoft.com/fwlink/?LinkId=317604',
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
