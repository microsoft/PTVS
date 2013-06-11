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
PIP_SOURCE = 'https://go.microsoft.com/fwlink/?LinkID=306664'

temp_dir = tempfile.mkdtemp('-pip_downloader', 'ptvs-')
cwd = os.getcwd()
os.chdir(temp_dir)

try:
    print('Downloading distribute from ' + DISTRIBUTE_SOURCE)
    distribute_package, _ = urlretrieve(DISTRIBUTE_SOURCE, 'distribute.tar.gz')

    package = tarfile.open(distribute_package)
    try:
        safe_members = [m for m in package.getmembers() if not m.name.startswith(('..', '\\'))]
        package.extractall(temp_dir, members=safe_members)
    finally:
        package.close()

    extracted_dir = os.listdir(temp_dir)[0]
    print('\nInstalling from ' + extracted_dir)
    os.chdir(extracted_dir)
    subprocess.check_call([sys.executable, 'setup.py', 'install'])
    os.chdir(temp_dir)

    print('\nDownloading get-pip.py from ' + PIP_SOURCE)
    get_pip_path, _ = urlretrieve(PIP_SOURCE, 'get_pip.py')

    print('\nInstalling from ' + get_pip_path)
    subprocess.check_call([sys.executable, get_pip_path])

    print('\nInstallation Complete')
finally:
    try:
        os.chdir(cwd)
        shutil.rmtree(temp_dir)
    except:
        # Don't report errors deleting temporary files
        pass
