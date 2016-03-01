"""
Definition of urls for $safeprojectname$.
"""

from django.conf.urls import include, url

import $safeprojectname$.views

# Uncomment the next two lines to enable the admin:
# from django.contrib import admin
# admin.autodiscover()

urlpatterns = [
    # Examples:
    # url(r'^$', $safeprojectname$.views.home, name='home'),
    # url(r'^$safeprojectname$/', include('$safeprojectname$.$safeprojectname$.urls')),

    # Uncomment the admin/doc line below to enable admin documentation:
    # url(r'^admin/doc/', include('django.contrib.admindocs.urls')),

    # Uncomment the next line to enable the admin:
    # url(r'^admin/', include(admin.site.urls)),
]
