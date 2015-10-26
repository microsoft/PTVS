from sys import argv, exit, modules
print(argv)
if __name__ != '__main__': exit(1)
if __file__ != argv[0]: exit(2)
if modules['__main__'].__dict__ is not globals(): exit(3)
