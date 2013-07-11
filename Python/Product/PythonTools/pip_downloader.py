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

DISTRIBUTE_SOURCE = 'https://go.microsoft.com/fwlink/?LinkID=306663'
PIP_SOURCE = 'https://go.microsoft.com/fwlink/?LinkId=313647'

distribute_temp_dir = tempfile.mkdtemp('-distribute', 'ptvs-')
pip_temp_dir = tempfile.mkdtemp('-pip', 'ptvs-')
cwd = os.getcwd()

try:
    os.chdir(distribute_temp_dir)
    print('Downloading distribute from ' + DISTRIBUTE_SOURCE)
    sys.stdout.flush()
    distribute_package, _ = urlretrieve(DISTRIBUTE_SOURCE, 'distribute.tar.gz')

    package = tarfile.open(distribute_package)
    try:
        safe_members = [m for m in package.getmembers() if not m.name.startswith(('..', '\\'))]
        package.extractall(distribute_temp_dir, members=safe_members)
    finally:
        package.close()

    extracted_dirs = [d for d in os.listdir(distribute_temp_dir) if os.path.exists(os.path.join(d, 'setup.py'))]
    if not extracted_dirs:
        raise OSError("Failed to find distribute's setup.py")
    extracted_dir = extracted_dirs[0]

    print('\nInstalling from ' + extracted_dir)
    sys.stdout.flush()
    os.chdir(extracted_dir)
    subprocess.check_call([sys.executable, 'setup.py', 'install'])

    os.chdir(pip_temp_dir)
    print('Downloading pip from ' + PIP_SOURCE)
    sys.stdout.flush()
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
        shutil.rmtree(distribute_temp_dir)
        shutil.rmtree(pip_temp_dir)
    except:
        # Don't report errors deleting temporary files
        pass
