from datetime import datetime
from django.conf.urls import patterns, include, url
from djangoapp.forms import BootstrapAuthenticationForm

# Uncomment the next two lines to enable the admin:
# from django.contrib import admin
# admin.autodiscover()

urlpatterns = patterns('',
    # Examples:
    url(r'^$', 'djangoapp.views.home', name='home'),
    url(r'^contact$', 'djangoapp.views.contact', name='contact'),
    url(r'^about', 'djangoapp.views.about', name='about'),
    url(r'^login/$',
        'django.contrib.auth.views.login',
        {
            'template_name': 'djangoapp/login.html',
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

    # Uncomment the admin/doc line below to enable admin documentation:
    # url(r'^admin/doc/', include('django.contrib.admindocs.urls')),

    # Uncomment the next line to enable the admin:
    # url(r'^admin/', include(admin.site.urls)),
)
