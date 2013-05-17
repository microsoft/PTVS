 # ############################################################################
 #
 # Copyright (c) Microsoft Corporation. 
 #
 # Available under the Microsoft PyKinect 1.0 Alpha license.  See LICENSE.txt
 # for more information.
 #
 # ###########################################################################/

try:
    from setuptools import setup
except ImportError:
    from distutils.core import setup


classifiers = """\
Development Status :: 4 - Beta
Environment :: Win32 (MS Windows)
License :: Free for non-commercial use
License :: Other/Proprietary License
Natural Language :: English
Operating System :: Microsoft
Operating System :: Microsoft :: Windows :: Windows NT/2000
Programming Language :: C++
Programming Language :: Python
Programming Language :: Python :: 2.7
Programming Language :: Python :: 2 :: Only
Topic :: Games/Entertainment
Topic :: Multimedia :: Graphics
Topic :: Multimedia :: Graphics :: Capture
Topic :: Multimedia :: Sound/Audio
"""

setup(name='pykinect',
      version='1.0',
      description='PyKinect Module for interacting with the Kinect SDK',
      long_description='The pykinect package provides access to the Kinect device. The pykinect package includes both the "nui" and "audio" subpackages. The nui package provides interactions with the Kinect cameras including skeleton tracking, video camera, as well as the depth camera. The audio subpackage provides access to the Kinect devices microphones.',
      author='Microsoft',
      author_email='vspython@microsoft.com',
      url='http://pytools.codeplex.com/',
      packages=['pykinect', 'winspeech', 'pykinect.audio', 'pykinect.nui'],
      platforms=["win32"],
      classifiers = filter(None, classifiers.split("\n")),
      package_data={
        'pykinect.audio': ['*.dll'], 
        'pykinect': ['LICENSE.txt'],
        'winspeech' : ['LICENSE.txt']
      },
     )
