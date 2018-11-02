"""
Definition of urls for polls viewing and voting.
"""

from django.urls import path
from app.models import Poll
import app.views

urlpatterns = [
    path('',
        app.views.PollListView.as_view(
            queryset=Poll.objects.order_by('-pub_date')[:5],
            context_object_name='latest_poll_list',
            template_name='app/index.html',),
        name='home'),
    path('<int:pk>/',
        app.views.PollDetailView.as_view(
            template_name='app/details.html'),
        name='detail'),
    path('<int:pk>/results/',
        app.views.PollResultsView.as_view(
            template_name='app/results.html'),
        name='results'),
    path('<int:poll_id>/vote/', app.views.vote, name='vote'),
]
