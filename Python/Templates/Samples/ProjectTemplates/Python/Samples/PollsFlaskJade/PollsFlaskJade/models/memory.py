"""
Repository of polls that uses in-memory objects, no serialization.
Used for testing only.
"""

from . import Poll, Choice, PollNotFound
from . import _load_samples_json

class Repository(object):
    """In-Memory repository."""
    def __init__(self, settings):
        """Initializes the repository. Note that settings are not used."""
        self.name = 'In-Memory'
        self.index = {}

    def get_polls(self):
        """Returns all the polls from the repository."""
        return self.index.values()

    def get_poll(self, poll_key):
        """Returns a poll from the repository."""
        poll = self.index.get(poll_key)
        if poll is None:
            raise PollNotFound()
        return poll

    def increment_vote(self, poll_key, choice_key):
        """Increment the choice vote count for the specified poll."""
        poll = self.get_poll(poll_key)
        for choice in poll.choices:
            if choice.key == choice_key:
                choice.votes += 1
                break

    def add_sample_polls(self):
        """Adds a set of polls from data stored in a samples.json file."""
        poll_key = 0
        choice_key = 0

        for sample_poll in _load_samples_json():
            poll = Poll(str(poll_key), sample_poll['text'])
            for sample_choice in sample_poll['choices']:
                poll.choices.append(Choice(str(choice_key), sample_choice))
                choice_key += 1

            self.index[str(poll_key)] = poll
            poll_key += 1
