import builtins
import collections

OrderedDict = collections.OrderedDict
__doc__ = 'High performance data structures.\n- deque:        ordered collection accessible from endpoints only\n- defaultdict:  dict subclass with a default value factory\n'
__name__ = '_collections'
__package__ = ''
def _count_elements(mapping, iterable):
    '_count_elements(mapping, iterable) -> None\n\nCount elements in the iterable, updating the mapping'
    pass

class _deque_iterator(builtins.object):
    __class__ = _deque_iterator
    def __getattribute__(self, name):
        'Return getattr(self, name).'
        pass
    
    @classmethod
    def __init_subclass__(cls):
        'This method is called when a class is subclassed.\n\nThe default implementation does nothing. It may be\noverridden to extend subclasses.\n'
        pass
    
    def __iter__(self):
        'Implement iter(self).'
        pass
    
    def __length_hint__(self):
        'Private method returning an estimate of len(list(it)).'
        pass
    
    @classmethod
    def __new__(type, *args, **kwargs):
        'Create and return a new object.  See help(type) for accurate signature.'
        pass
    
    def __next__(self):
        'Implement next(self).'
        pass
    
    def __reduce__(self):
        'Return state information for pickling.'
        pass
    
    @classmethod
    def __subclasshook__(cls, subclass):
        'Abstract classes can override this to customize issubclass().\n\nThis is invoked early on by abc.ABCMeta.__subclasscheck__().\nIt should return True, False or NotImplemented.  If it returns\nNotImplemented, the normal algorithm is used.  Otherwise, it\noverrides the normal algorithm (and the outcome is cached).\n'
        pass
    

class _deque_reverse_iterator(builtins.object):
    __class__ = _deque_reverse_iterator
    def __getattribute__(self, name):
        'Return getattr(self, name).'
        pass
    
    @classmethod
    def __init_subclass__(cls):
        'This method is called when a class is subclassed.\n\nThe default implementation does nothing. It may be\noverridden to extend subclasses.\n'
        pass
    
    def __iter__(self):
        'Implement iter(self).'
        pass
    
    def __length_hint__(self):
        'Private method returning an estimate of len(list(it)).'
        pass
    
    @classmethod
    def __new__(type, *args, **kwargs):
        'Create and return a new object.  See help(type) for accurate signature.'
        pass
    
    def __next__(self):
        'Implement next(self).'
        pass
    
    def __reduce__(self):
        'Return state information for pickling.'
        pass
    
    @classmethod
    def __subclasshook__(cls, subclass):
        'Abstract classes can override this to customize issubclass().\n\nThis is invoked early on by abc.ABCMeta.__subclasscheck__().\nIt should return True, False or NotImplemented.  If it returns\nNotImplemented, the normal algorithm is used.  Otherwise, it\noverrides the normal algorithm (and the outcome is cached).\n'
        pass
    

defaultdict = collections.defaultdict
deque = collections.deque
