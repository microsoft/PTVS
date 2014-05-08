from datetime import datetime
from flask import render_template
from app import app

@app.route('/')
@app.route('/home')
def home():
    return render_template(
        'index.html',
        title = 'Home Page',
        year = datetime.now().year,
        content = 'hello template'
    )

@app.route('/contact')
def contact():
    return render_template(
        'contact.html',
        title = 'Contact',
        year = datetime.now().year,
        message = 'Your contact page.'
    )

@app.route('/about')
def about():
    return render_template(
        'about.html',
        title = 'About',
        year = datetime.now().year,
        message = 'Your application description page.'
    )

