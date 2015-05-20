from django.template import RequestContext
from django.shortcuts import render, render_to_response
from django.template.defaulttags import register

def test_render_view(request):
    return render(request, 'test_render.html', {'content': 'data'})

def test_render_to_response_view(request):
    return render_to_response('test_render_to_response.html', {'content': 'data'})

def test_RequestContext_view(request):
    return render(request, 'test_RequestContext.html', context_instance=RequestContext(request, {'content': 'data'}))

def test_RequestContext2_view(request):
    return render(request, 'test_RequestContext2.html', RequestContext(request, {'content': 'data'}))

@register.filter
def test_filter(f):
    """this is my filter"""
    pass

@register.tag
def test_tag(*a, **kw):
    pass

@register.filter('test_filter_2')
def test_filter_function(f):
    """this is my filter"""
    pass

@register.tag('test_tag_2')
def test_tag_function(*a, **kw):
    pass

@register.assignment_tag('test_assignment_tag')
def test_assignment_tag(*a, **kw):
    pass

@register.simple_tag('test_simple_tag')
def test_simple_tag(*a, **kw):
    pass
