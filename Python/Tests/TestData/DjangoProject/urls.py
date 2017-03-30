from django.conf.urls import include, url
import Oar.views

# Uncomment the next two lines to enable the admin:
from django.contrib import admin
admin.autodiscover()

urlpatterns = [
    # Examples:
    # url(r'^$', 'DjangoApplication1.views.home', name='home'),
    # url(r'^DjangoApplication1/', include('DjangoApplication1.fob.urls')),

    # Uncomment the admin/doc line below to enable admin documentation:
    # url(r'^admin/doc/', include('django.contrib.admindocs.urls')),

    # Uncomment the next line to enable the admin:
    url(r'^admin/', admin.site.urls),
    url(r'^Oar/$', Oar.views.index),
    url(r'^$', Oar.views.main),
    url(r'^loop_nobom/$', Oar.views.loop_nobom),
    url(r'^loop/$', Oar.views.loop),
    url(r'^loop2/$', Oar.views.loop2),
]
