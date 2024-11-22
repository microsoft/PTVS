from setuptools import setup, Extension
import sys

# Determine if we are in debug mode
debug = sys.executable.endswith('_d.exe')

# Determine python library name from the version
lib_name = f'python{sys.version_info.major}{sys.version_info.minor}'
if debug:
      lib_name += '_d'

# Define the extension module
module = Extension(
    'cpp_mod',
    sources=['initmod.cpp', 'cpp_mod.cpp'],
    define_macros=[('Py_DEBUG', None)] if debug else [],
    extra_compile_args = ['/Zi', '/Fdbuild'],
    extra_link_args = ['/DEBUG'],
    libraries=[lib_name],
    library_dirs=['path_to_python_libs'],
    include_dirs=['path_to_python_include']
)

setup(name='cpp_mod_pkg',
      version='1.0',
      ext_modules = [module],
     )
