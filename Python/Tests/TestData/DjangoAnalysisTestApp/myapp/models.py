from django.db import models

# Create your models here.
from django.db import models

class MyModel(models.Model):
    my_model = 'abc'
    model_data = models.CharField(max_length=200)
    pub_date = models.DateTimeField('date published')


class MyModel2(models.Model):
    my_model2 = 'abc'
    model_data = models.CharField(max_length=200)
    pub_date = models.DateTimeField('date published')

