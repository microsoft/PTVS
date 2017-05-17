"""
Repository of polls that stores data in Azure Table Storage.
"""

from azure.common import AzureMissingResourceHttpError
from azure.storage.table import TableService

from . import Poll, Choice, PollNotFound
from . import _load_samples_json

def _partition_and_row_to_key(partition, row):
    """Builds a poll/choice key out of azure table partition and row keys."""
    return partition + '_' + row

def _key_to_partition_and_row(key):
    """Parses the azure table partition and row keys from the poll/choice
    key."""
    partition, _, row = key.partition('_')
    return partition, row

def _poll_from_entity(entity):
    """Creates a poll object from the azure table poll entity."""
    return Poll(
        _partition_and_row_to_key(entity.PartitionKey, entity.RowKey),
        entity.Text
    )

def _choice_from_entity(entity):
    """Creates a choice object from the azure table choice entity."""
    return Choice(
        _partition_and_row_to_key(entity.PartitionKey, entity.RowKey),
        entity.Text,
        entity.Votes
    )

class Repository(object):
    """Azure Table Storage repository."""
    def __init__(self, settings):
        """Initializes the repository with the specified settings dict.
        Required settings are:
         - STORAGE_NAME
         - STORAGE_KEY
         - STORAGE_TABLE_POLL
         - STORAGE_TABLE_CHOICE
        """
        self.name = 'Azure Table Storage'
        self.storage_name = settings['STORAGE_NAME']
        self.storage_key = settings['STORAGE_KEY']
        self.poll_table = settings['STORAGE_TABLE_POLL']
        self.choice_table = settings['STORAGE_TABLE_CHOICE']

        self.svc = TableService(self.storage_name, self.storage_key)
        self.svc.create_table(self.poll_table)
        self.svc.create_table(self.choice_table)

    def get_polls(self):
        """Returns all the polls from the repository."""
        poll_entities = self.svc.query_entities(self.poll_table)
        polls = [_poll_from_entity(entity) for entity in poll_entities]
        return polls

    def get_poll(self, poll_key):
        """Returns a poll from the repository."""
        try:
            partition, row = _key_to_partition_and_row(poll_key)
            poll_entity = self.svc.get_entity(self.poll_table, partition, row)
            choice_entities = self.svc.query_entities(
                self.choice_table,
                "PollPartitionKey eq '{0}' and PollRowKey eq '{1}'" \
                    .format(partition, row)
            )

            poll = _poll_from_entity(poll_entity)
            poll.choices = [_choice_from_entity(choice_entity)
                            for choice_entity in choice_entities]
            return poll
        except AzureMissingResourceHttpError:
            raise PollNotFound()

    def increment_vote(self, poll_key, choice_key):
        """Increment the choice vote count for the specified poll."""
        try:
            partition, row = _key_to_partition_and_row(choice_key)
            entity = self.svc.get_entity(self.choice_table, partition, row)
            entity.Votes += 1
            self.svc.update_entity(self.choice_table, entity)
        except AzureMissingResourceHttpError:
            raise PollNotFound()

    def add_sample_polls(self):
        """Adds a set of polls from data stored in a samples.json file."""
        poll_partition = '2014'
        poll_row = 0
        choice_partition = '2014'
        choice_row = 0

        for sample_poll in _load_samples_json():
            poll_entity = {
                'PartitionKey': poll_partition,
                'RowKey': str(poll_row),
                'Text': sample_poll['text'],
            }
            self.svc.insert_entity(self.poll_table, poll_entity)

            for sample_choice in sample_poll['choices']:
                choice_entity = {
                    'PartitionKey': choice_partition,
                    'RowKey': str(choice_row),
                    'Text': sample_choice,
                    'Votes': 0,
                    'PollPartitionKey': poll_partition,
                    'PollRowKey': str(poll_row),
                }
                self.svc.insert_entity(self.choice_table, choice_entity)
                choice_row += 1

            poll_row += 1
