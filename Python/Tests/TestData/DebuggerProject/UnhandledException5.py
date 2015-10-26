def A():
    return ValueError

try: raise ValueError() # breaks
except A(): pass
