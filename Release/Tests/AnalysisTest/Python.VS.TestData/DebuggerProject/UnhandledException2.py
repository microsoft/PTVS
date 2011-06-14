try: raise ValueError()
except: pass

try: raise ValueError()
except BaseException: pass

try: raise ValueError()
except Exception: pass

try: raise ValueError()
except StandardError: pass

try: raise ValueError()
except ValueError: pass

try: raise Exception()  # breaks
except ValueError: pass
