"""
The flask application package.
"""

from flask import Flask
app = Flask(__name__)
app.jinja_env.add_extension('pyjade.ext.jinja.PyJadeExtension')

import $safeprojectname$.views
