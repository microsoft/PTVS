from setuptools import setup, find_packages

try:
    from pathlib import Path
    here = Path(__file__).parent
except ImportError:
    from os.path import split, join
    from codecs import open
    here = split(__file__)[0]
    Path = None


# Get the long description from the relevant file
if Path:
    f = (here /'README.rst').open(encoding='utf-8')
else:
    f = open(join(here, 'README.rst'), encoding='utf-8')

with f:
    long_description = f.read()

setup(
    name='wfastcgi',
    version='2.2',

    description='A sample Python project',
    long_description=long_description,
    url='https://github.com/Microsoft/PTVS',

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
        'Topic :: Internet',
        'Topic :: Internet :: WWW/HTTP',
        'Topic :: Internet :: WWW/HTTP :: WSGI',
        'Topic :: Internet :: WWW/HTTP :: WSGI :: Server',
    ],

    keywords='iis fastcgi',
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

