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
try:
    from urllib.request import urlretrieve
except ImportError:
    from urllib import urlretrieve

if sys.version_info[0:2] < (2, 5):
    print('Python versions earlier than 2.5 are not supported by PTVS.')
    sys.exit(-1)
elif sys.version_info[0:2] == (2, 5):
    SETUPTOOLS_SOURCE = 'https://go.microsoft.com/fwlink/?LinkId=317602'
    PIP_SOURCE = 'https://go.microsoft.com/fwlink/?LinkId=313647'
else:
    SETUPTOOLS_SOURCE = 'https://go.microsoft.com/fwlink/?LinkId=317603'
    PIP_SOURCE = 'https://go.microsoft.com/fwlink/?LinkId=317604'

setuptools_temp_dir = tempfile.mkdtemp('-setuptools', 'ptvs-')
pip_temp_dir = tempfile.mkdtemp('-pip', 'ptvs-')
cwd = os.getcwd()

try:
    os.chdir(setuptools_temp_dir)
    print('Downloading setuptools from ' + SETUPTOOLS_SOURCE)
    sys.stdout.flush()
    if os.path.exists('setuptools.tar.gz'):
        setuptools_package = 'setuptools.tar.gz'
    else:
        setuptools_package, _ = urlretrieve(SETUPTOOLS_SOURCE, 'setuptools.tar.gz')

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
    subprocess.check_call([sys.executable, 'setup.py', 'install'])

    os.chdir(pip_temp_dir)
    print('Downloading pip from ' + PIP_SOURCE)
    sys.stdout.flush()
    if os.path.exists('pip.tar.gz'):
        pip_package = 'pip.tar.gz'
    else:
        pip_package, _ = urlretrieve(PIP_SOURCE, 'pip.tar.gz')

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
    subprocess.check_call([sys.executable, 'setup.py', 'install'])

    print('\nInstallation Complete')
    sys.stdout.flush()
finally:
    try:
        os.chdir(cwd)
        shutil.rmtree(setuptools_temp_dir)
        shutil.rmtree(pip_temp_dir)
    except:
        # Don't report errors deleting temporary files
        pass
