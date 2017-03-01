from setuptools import setup
from os.path import join, split

with open(join(split(__file__)[0], 'README.rst')) as f:
    long_description = f.read()

setup(
    name='wfastcgi',
    version='3.0.0',

    description='An IIS-Python bridge based on WSGI and FastCGI.',
    long_description=long_description,
    url='http://aka.ms/python',

    # Author details
    author='Microsoft Corporation',
    author_email='ptvshelp@microsoft.com',
    license='Apache License 2.0',

    # See https://pypi.python.org/pypi?%3Aaction=list_classifiers
    classifiers=[
        'Development Status :: 6 - Mature',
        'License :: OSI Approved :: Apache Software License',
        'Operating System :: Microsoft :: Windows',
        'Programming Language :: Python :: 2',
        'Programming Language :: Python :: 2.7',
        'Programming Language :: Python :: 3',
        'Programming Language :: Python :: 3.3',
        'Programming Language :: Python :: 3.4',
        'Programming Language :: Python :: 3.5',
        'Programming Language :: Python :: 3.6',
        'Topic :: Internet',
        'Topic :: Internet :: WWW/HTTP',
        'Topic :: Internet :: WWW/HTTP :: WSGI',
        'Topic :: Internet :: WWW/HTTP :: WSGI :: Server',
    ],

    keywords='iis fastcgi wsgi windows server mod_python',
    py_modules=['wfastcgi'],
    install_requires=[],
    entry_points={
        'console_scripts': [
            'wfastcgi = wfastcgi:main',
            'wfastcgi-enable = wfastcgi:enable',
            'wfastcgi-disable = wfastcgi:disable',
        ]
    },
)

