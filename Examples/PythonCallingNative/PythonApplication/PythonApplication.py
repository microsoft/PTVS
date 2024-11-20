
import ctypes 

def returnATuple():
    return (4, 5)

def returnADict():
    return { "4": 5 }

# Load the DLL
mylib = ctypes.CDLL('..\\x64\\Debug\\CppDll.dll')

# Call the add function from the DLL result
result = mylib.add(5, 10)

print(f"Result from C++ DLL: {result}")

print("After result is printed")

x = returnATuple()
y = returnADict()

print("After tuple")