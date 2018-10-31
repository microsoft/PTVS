"""
Definition of urls for $safeprojectname$.
"""

from app import forms, views
from datetime import datetime
from django.contrib import admin
from django.urls import path, include
from django.contrib.auth.views import LoginView, LogoutView

urlpatterns = [
    path('', include(('app.urls', "app"), "appurls")),
    path('contact', views.contact, name='contact'),
    path('about/', views.about, name='about'),
    path('seed/', views.seed, name='seed'),
    path('login/', 
        LoginView.as_view
        (
            template_name = 'app/login.html', 
            authentication_form = forms.BootstrapAuthenticationForm,
            extra_context =
            {
                'title': 'Log in',
                'year': datetime.now().year,
            }
         ),
        name='login'),
    path('logout/', LogoutView.as_view(next_page = '/'), name='logout'),

    path('admin/', admin.site.urls)
]