# Create your views here.
from django.template.base import Template
from django.template.loader import render_to_string
from django.http import HttpResponse
def home(request):
    return Template('<html><body>{{ foo }}</body></html>').render({'foo' : 42})


def home2(request):
    return HttpResponse(render_to_string('page.html.djt', {'content': 42}))

def home3(request):
    return HttpResponse(render_to_string('page2.html.djt', {'content': 42}))

def home4(request):
    return HttpResponse(render_to_string('page3.html.djt', {'content': 'foo'}))


