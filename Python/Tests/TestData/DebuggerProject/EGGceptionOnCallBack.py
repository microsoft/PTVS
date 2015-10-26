import os, sys
sys.path.append(os.path.abspath('EGG.egg'))

import EGG.callback_exception

def value_error():
    raise ValueError

def type_error():
    raise TypeError

EGG.callback_exception.f(value_error)
EGG.callback_exception.f(type_error)
