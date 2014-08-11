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

import os
from warnings import warn
try:
    from setuptools import setup, Extension
    from setuptools.command.build_ext import build_ext
except ImportError:
    from distutils.core import setup, Extension
    from distutils.command.build_ext import build_ext

import distutils.msvc9compiler
from distutils.sysconfig import get_config_var
from distutils.util import get_platform

long_description = 'The pykinect package provides access to the Kinect device. The pykinect package includes both the "nui" and "audio" subpackages. The nui package provides interactions with the Kinect cameras including skeleton tracking, video camera, as well as the depth camera. The audio subpackage provides access to the Kinect devices microphones.'

classifiers = [
    'Development Status :: 5 - Production/Stable',
    'Environment :: Win32 (MS Windows)',
    'License :: OSI Approved :: Apache Software License',
    'Natural Language :: English',
    'Operating System :: Microsoft :: Windows',
    'Operating System :: Microsoft :: Windows :: Windows 7',
    'Programming Language :: C++',
    'Programming Language :: Python',
    'Programming Language :: Python :: 2.7',
    'Programming Language :: Python :: 2 :: Only',
    'Topic :: Games/Entertainment',
    'Topic :: Multimedia :: Graphics',
    'Topic :: Multimedia :: Graphics :: Capture',
    'Topic :: Multimedia :: Sound/Audio',
]


def _find_vcvarsall(version):
    Reg = distutils.msvc9compiler.Reg
    try:
        from winreg import HKEY_LOCAL_MACHINE
    except ImportError:
        from _winreg import HKEY_LOCAL_MACHINE
    for sxs_key in [
        Reg.read_values(HKEY_LOCAL_MACHINE, r'SOFTWARE\Wow6432Node\Microsoft\VisualStudio\SxS\VC7'),
        Reg.read_values(HKEY_LOCAL_MACHINE, r'SOFTWARE\Microsoft\VisualStudio\SxS\VC7')
    ]:
        for vcpath in (sxs_key[v] for v in ['12.0', '11.0', '10.0'] if v in sxs_key):
            if os.path.exists(os.path.join(vcpath, 'vcvarsall.bat')):
                return os.path.join(vcpath, 'vcvarsall.bat')
    return None

# Replace the original find_vcvarsall with our version, which will find newer
# versions of MSVC.
distutils.msvc9compiler.find_vcvarsall = _find_vcvarsall

class pykinect_build_ext(build_ext):
    def initialize_options(self):
        build_ext.initialize_options(self)
        self.inplace = 1
    
    def get_ext_filename(self, ext_name):
        return build_ext.get_ext_filename(self, ext_name).replace(get_config_var('SO'), '.dll')

    def get_export_symbols(self, ext):
        return ext.export_symbols

    def get_source_files(self):
        filenames = build_ext.get_source_files(self)
        
        for ext in self.extensions:
            filenames.extend(getattr(ext, 'headers', []))
        
        return filenames

kinectsdk_dir = os.environ.get('KINECTSDK10_DIR', '')
if kinectsdk_dir:
    kinectsdk_inc = os.path.join(kinectsdk_dir, 'inc')
    kinectsdk_lib = os.path.join(kinectsdk_dir, 'lib', distutils.msvc9compiler.PLAT_TO_VCVARS[get_platform()])
else:
    warn("Cannot find KINECTSDK10_DIR environment variable. You will need to install the Kinect for Windows SDK if building.")

pykinectaudio_ext = Extension(
    'pykinect.audio.PyKinectAudio',
    include_dirs=filter(None, ['src', kinectsdk_inc]),
    libraries=['Msdmo', 'dmoguids', 'mf', 'mfuuid', 'mfplat', 'avrt', 'Kinect10'],
    library_dirs=filter(None, [kinectsdk_lib]),
    sources=[
        'src\\stdafx.cpp',
        'src\\PyKinectAudio.cpp',
        'src\\AudioStream.cpp',
        'src\\MediaBuffer.cpp',
    ],
)

pykinectaudio_ext.headers=[
    'src\\AudioStream.h',
    'src\\MediaBuffer.h',
    'src\\PyKinectAudio.h',
    'src\\stdafx.h',
    'src\\targetver.h',
]


setup_cfg = dict(
    name='pykinect',
    version='2.1',
    description='PyKinect Module for interacting with the Kinect SDK',
    long_description=long_description,
    author='Microsoft Corporation',
    author_email='ptvshelp@microsoft.com',
    url='http://pytools.codeplex.com/',
    zip_safe=False,
    packages=['pykinect', 'winspeech', 'pykinect.audio', 'pykinect.nui'],
    platforms=["win32"],
    classifiers=classifiers,
    package_data={
        '': ['*.txt'],
    },
    cmdclass={'build_ext': pykinect_build_ext},
    ext_modules=[pykinectaudio_ext]
)

setup(**setup_cfg)
