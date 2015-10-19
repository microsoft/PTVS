from django.conf.urls import patterns, url
from test_render.views import *

urlpatterns = patterns('',
    url(r'^render$', test_render_view),
    url(r'^render_to_response$', test_render_to_response_view),
    url(r'^RequestContext$', test_RequestContext_view),
)
