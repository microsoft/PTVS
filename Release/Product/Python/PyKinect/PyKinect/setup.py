 # ############################################################################
 #
 # Copyright (c) Microsoft Corporation. 
 #
 # Available under the Microsoft PyKinect 1.0 Alpha license.  See LICENSE.txt
 # for more information.
 #
 # ###########################################################################/


from distutils.core import setup

setup(name='PyKinect',
      version='1.0',
      description='PyKinect Module for interacting with the Kinect SDK',
      author='Microsoft',
      author_email='vspython@microsoft.com',
      url='http://pytools.codeplex.com/',
      packages=['pykinect', 'speech', 'pykinect.audio', 'pykinect.nui'],
      package_data={
        'pykinect.audio': ['*.dll'], 
        'pykinect': ['LICENSE.txt'],
        'speech' : ['LICENSE.txt']
      },
     )
