from django.db import models
import datetime

# Create your models here.
from django.db import models

class Poll(models.Model):
    question = models.CharField(max_length=200)
    pub_date = models.DateTimeField('date published')

    def was_published_today(self):
        return self.pub_date.date() == datetime.date.today()

    was_published_today.short_description = 'Published today?'

class Choice(models.Model):
    poll = models.ForeignKey(Poll, on_delete = models.CASCADE)
    choice = models.CharField(max_length=200)
    votes = models.IntegerField()


