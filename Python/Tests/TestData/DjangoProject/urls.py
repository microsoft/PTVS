from django.conf.urls.defaults import patterns, include, url

# Uncomment the next two lines to enable the admin:
from django.contrib import admin
admin.autodiscover()

urlpatterns = patterns('',
    # Examples:
    # url(r'^$', 'DjangoApplication1.views.home', name='home'),
    # url(r'^DjangoApplication1/', include('DjangoApplication1.fob.urls')),

    # Uncomment the admin/doc line below to enable admin documentation:
    # url(r'^admin/doc/', include('django.contrib.admindocs.urls')),

    # Uncomment the next line to enable the admin:
    url(r'^admin/', include(admin.site.urls)),
    url(r'^Bar/$', 'Bar.views.index'),
    url(r'^/$', 'Bar.views.main'),
    url(r'^loop_nobom/$', 'Bar.views.loop_nobom'),
    url(r'^loop/$', 'Bar.views.loop'),
    url(r'^loop2/$', 'Bar.views.loop2'),
)
