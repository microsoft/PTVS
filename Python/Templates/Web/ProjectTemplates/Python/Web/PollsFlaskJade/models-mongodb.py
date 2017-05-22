"""
Repository of polls that stores data in a MongoDB database.
"""

from bson.objectid import ObjectId, InvalidId
from pymongo import MongoClient

from . import Poll, Choice, PollNotFound
from . import _load_samples_json

def _poll_from_doc(doc):
    """Creates a poll object from the MongoDB poll document."""
    return Poll(str(doc['_id']), doc['text'])

def _choice_from_doc(doc):
    """Creates a choice object from the MongoDB choice subdocument."""
    return Choice(str(doc['id']), doc['text'], doc['votes'])

class Repository(object):
    """MongoDB repository."""
    def __init__(self, settings):
        """Initializes the repository with the specified settings dict.
        Required settings are:
         - MONGODB_HOST
         - MONGODB_DATABASE
         - MONGODB_COLLECTION
        """
        self.name = 'MongoDB'
        self.host = settings['MONGODB_HOST']
        self.client = MongoClient(self.host)
        self.database = self.client[settings['MONGODB_DATABASE']]
        self.collection = self.database[settings['MONGODB_COLLECTION']]

    def get_polls(self):
        """Returns all the polls from the repository."""
        docs = self.collection.find()
        polls = [_poll_from_doc(doc) for doc in docs]
        return polls

    def get_poll(self, poll_key):
        """Returns a poll from the repository."""
        try:
            doc = self.collection.find_one({"_id": ObjectId(poll_key)})
            if doc is None:
                raise PollNotFound()

            poll = _poll_from_doc(doc)
            poll.choices = [_choice_from_doc(choice_doc)
                            for choice_doc in doc['choices']]
            return poll
        except InvalidId:
            raise PollNotFound()

    def increment_vote(self, poll_key, choice_key):
        """Increment the choice vote count for the specified poll."""
        try:
            self.collection.update(
                {
                    "_id": ObjectId(poll_key),
                    "choices.id": int(choice_key),
                },
                {
                    "$inc": {"choices.$.votes": 1}
                }
            )
        except(InvalidId, ValueError):
            raise PollNotFound()

    def add_sample_polls(self):
        """Adds a set of polls from data stored in a samples.json file."""
        for sample_poll in _load_samples_json():
            choices = []
            choice_id = 0
            for sample_choice in sample_poll['choices']:
                choice_doc = {
                    'id': choice_id,
                    'text': sample_choice,
                    'votes': 0,
                }
                choice_id += 1
                choices.append(choice_doc)

            poll_doc = {
                'text': sample_poll['text'],
                'choices': choices,
            }

            self.collection.insert(poll_doc)
