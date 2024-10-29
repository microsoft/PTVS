
import ctypes 

# Load the DLL
mylib = ctypes.CDLL('..\\x64\\Debug\\CppDll.dll')

# Call the add function from the DLL result
result = mylib.add(5, 10)

print(f"Result from C++ DLL: {result}")