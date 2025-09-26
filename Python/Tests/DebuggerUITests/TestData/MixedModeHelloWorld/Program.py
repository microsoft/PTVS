import sys
import ctypes
import os

_here = os.path.dirname(__file__)
_dll_name = 'mixedmodenative'
if sys.platform == 'win32':
    dll_path = os.path.join(_here, f'{_dll_name}.dll')
    try:
        _native = ctypes.CDLL(dll_path)
        add_numbers = _native.add_numbers
        add_numbers.argtypes = [ctypes.c_int, ctypes.c_int]
        add_numbers.restype = ctypes.c_int
    except OSError:
        add_numbers = lambda a, b: a + b
else:
    add_numbers = lambda a, b: a + b

def main():
    # Breakpoint expected here (line 18 after edits)
    value = add_numbers(1, 2)
    print('Mixed mode hello world:', value)

if __name__ == '__main__':
    main()
