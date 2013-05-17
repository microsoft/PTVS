.. _intro:

Introduction
============

Pyvot connects familiar data-exploration and visualization tools in Excel with the powerful data analysis and transformation capabilities of Python, with an emphasis on tabular data. It provides a minimal and Pythonic interface to Excel, smoothing over the pain points in using the existing Excel object model as exposed via COM.

.. _install:

Installation
------------

Pyvot requires :program:`CPython` version `2.7 <http://python.org/download/releases/2.7.2/>`_ or `3.2 <http://www.python.org/download/releases/3.2.2/>`_ with the `Python for Windows extensions (pywin32) <http://sourceforge.net/projects/pywin32/>`_ installed, and Office 2010. 

If a clean Python session can import the win32com module, Pyvot is ready to be installed::

	PS C:\> python
	Python 2.7 (r27:82525, Jul  4 2010, 09:01:59) [MSC v.1500 32 bit (Intel)] on win32
	Type "help", "copyright", "credits" or "license" for more information.
	>>> import win32com
	>>>

Installing with :mod:`setuptools` / :mod:`distribute`
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^

If you have `setuptools <http://pypi.python.org/pypi/setuptools>`_ installed, you can install a source or binary distribution of Pyvot with :program:`easy_install`. :program:`easy_install` is usually in :file:`<Python directory, ex. C:\\Python27>\\Scripts`

* To install the latest version from PyPI::

	easy_install Pyvot

* To install an already-downloaded source (.zip) or binary (.egg) distribution::

	easy_install path\to\file

Installing manually
^^^^^^^^^^^^^^^^^^^

If you do not have setuptools or distribute installed (the default, if you used the official Python installer), Pyvot can also be installed manually.

To do so, extract the source .zip for Pyvot, and run the following in the extracted directory::

	<Python directory, ex. C:\Python27>\python.exe setup.py install

