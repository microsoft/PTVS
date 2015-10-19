# Create your views here.
from django.template.base import Template
from django.template.loader import render_to_string
from django.http import HttpResponse
from django.shortcuts import render

def home(request):
    return Template('<html><body>{{ foo }}</body></html>').render({'foo' : 42})


def home2(request):
    return HttpResponse(render_to_string('page.html.djt', {'content': 42}))

def home3(request):
    return HttpResponse(render_to_string('page2.html.djt', {'content': 42}))

def home4(request):
    return HttpResponse(render_to_string('page3.html.djt', {'content': 'fob'}))


def home5(request):
    return HttpResponse(render_to_string('page4.html.djt', {'content': 'fob'}))


def home6(request):
    return render(request, 'page5.html.djt', {'content': 'fob'})


