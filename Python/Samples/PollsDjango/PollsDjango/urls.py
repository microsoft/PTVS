"""
Definition of urls for $safeprojectname$.
"""

from datetime import datetime
from django.conf.urls import patterns, include, url
from django.contrib import admin
from app.forms import BootstrapAuthenticationForm

admin.autodiscover()

urlpatterns = patterns('',
    url(r'^', include('app.urls', namespace="app")),
    url(r'^contact$', 'app.views.contact', name='contact'),
    url(r'^about', 'app.views.about', name='about'),
    url(r'^seed', 'app.views.seed', name='seed'),
    url(r'^login/$',
        'django.contrib.auth.views.login',
        {
            'template_name': 'app/login.html',
            'authentication_form': BootstrapAuthenticationForm,
            'extra_context':
            {
                'title':'Log in',
                'year':datetime.now().year,
            }
        },
        name='login'),
    url(r'^logout$',
        'django.contrib.auth.views.logout',
        {
            'next_page': '/',
        },
        name='logout'),
    url(r'^admin/', include(admin.site.urls)),
)
