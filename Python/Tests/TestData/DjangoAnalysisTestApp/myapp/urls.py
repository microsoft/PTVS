from django.conf.urls import patterns, url
from django.views.generic import DetailView, ListView
from myapp.models import *

urlpatterns = patterns('',
    url(r'^$',
        ListView.as_view(
            queryset=MyModel.objects.order_by('-pub_date')[:5],
            context_object_name='latest_poll_list',
            template_name='myapp/index.html'
        ),
        name='index'),
    url(r'^(?P<pk>\d+)/$',
        DetailView.as_view(
            model=MyModel,
            template_name='myapp/details.html'
        ),
        name='detail'),
    url(r'^(?P<pk>\d+)/$',
        DetailView.as_view(model=MyModel2),
        name='detail'),
)
