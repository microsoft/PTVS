from setuptools import setup, Extension

setup(name='cpp_mod_pkg',
      version='1.0',
      ext_modules = [Extension('cpp_mod',
                               ['initmod.cpp', 'cpp_mod.cpp'],
                               extra_compile_args = ['/Zi', '/Fdbuild'],
                               extra_link_args = ['/DEBUG'])],
     )
