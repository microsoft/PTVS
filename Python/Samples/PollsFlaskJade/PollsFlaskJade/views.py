"""
Routes and views for the flask application.
"""

from datetime import datetime

from flask import render_template, redirect, request

from $safeprojectname$ import app
from $safeprojectname$.models import PollNotFound
from $safeprojectname$.models.factory import create_repository
from $safeprojectname$.settings import REPOSITORY_NAME, REPOSITORY_SETTINGS

repository = create_repository(REPOSITORY_NAME, REPOSITORY_SETTINGS)

@app.route('/')
@app.route('/home')
def home():
    """Renders the home page, with a list of all polls."""
    return render_template(
        'index.jade',
        title='Polls',
        year=datetime.now().year,
        polls=repository.get_polls(),
    )

@app.route('/contact')
def contact():
    """Renders the contact page."""
    return render_template(
        'contact.jade',
        title='Contact',
        year=datetime.now().year,
    )

@app.route('/about')
def about():
    """Renders the about page."""
    return render_template(
        'about.jade',
        title='About',
        year=datetime.now().year,
        repository_name=repository.name,
    )

@app.route('/seed', methods=['POST'])
def seed():
    """Seeds the database with sample polls."""
    repository.add_sample_polls()
    return redirect('/')

@app.route('/results/<key>')
def results(key):
    """Renders the results page."""
    poll = repository.get_poll(key)
    poll.calculate_stats()
    return render_template(
        'results.jade',
        title='Results',
        year=datetime.now().year,
        poll=poll,
    )

@app.route('/poll/<key>', methods=['GET', 'POST'])
def details(key):
    """Renders the poll details page."""
    error_message = ''
    if request.method == 'POST':
        try:
            choice_key = request.form['choice']
            repository.increment_vote(key, choice_key)
            return redirect('/results/{0}'.format(key))
        except KeyError:
            error_message = 'Please make a selection.'

    return render_template(
        'details.jade',
        title='Poll',
        year=datetime.now().year,
        poll=repository.get_poll(key),
        error_message=error_message,
    )

@app.errorhandler(PollNotFound)
def page_not_found(error):
    """Renders error page."""
    return 'Poll does not exist.', 404
