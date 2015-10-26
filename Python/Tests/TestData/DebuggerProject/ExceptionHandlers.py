try:
    pass
except:
    pass

try: pass
except: pass

try:
    pass
    pass
    pass
except:
    pass
    pass
    pass

try: pass
except ArithmeticError: pass
except AssertionError: pass
except AttributeError: pass
except BaseException: pass
except BufferError: pass
except BytesWarning: pass
except DeprecationWarning: pass
except EOFError: pass
except EnvironmentError: pass
except Exception: pass
except FloatingPointError: pass
except FutureWarning: pass
except GeneratorExit: pass
except IOError: pass
except ImportError: pass
except ImportWarning: pass
except IndentationError: pass
except IndexError: pass
except KeyError: pass
except KeyboardInterrupt: pass
except LookupError: pass
except MemoryError: pass
except NameError: pass
except NotImplementedError: pass
except OSError: pass
except OverflowError: pass
except PendingDeprecationWarning: pass
except ReferenceError: pass
except RuntimeError: pass
except RuntimeWarning: pass
except StandardError: pass
except StopIteration: pass
except SyntaxError: pass
except SyntaxWarning: pass
except SystemError: pass
except SystemExit: pass
except TabError: pass
except TypeError: pass
except UnboundLocalError: pass
except UnicodeDecodeError: pass
except UnicodeEncodeError: pass
except UnicodeError: pass
except UnicodeTranslateError: pass
except UnicodeWarning: pass
except UserWarning: pass
except ValueError: pass
except Warning: pass
except WindowsError: pass
except ZeroDivisionError: pass

try: pass
except (ArithmeticError, AssertionError, AttributeError, BaseException, BufferError, BytesWarning, DeprecationWarning, EOFError, EnvironmentError, Exception, FloatingPointError, FutureWarning, GeneratorExit, IOError, ImportError, ImportWarning, IndentationError, IndexError, KeyError, KeyboardInterrupt, LookupError, MemoryError, NameError, NotImplementedError, OSError, OverflowError, PendingDeprecationWarning, ReferenceError, RuntimeError, RuntimeWarning, StandardError, StopIteration, SyntaxError, SyntaxWarning, SystemError, SystemExit, TabError, TypeError, UnboundLocalError, UnicodeDecodeError, UnicodeEncodeError, UnicodeError, UnicodeTranslateError, UnicodeWarning, UserWarning, ValueError, Warning, WindowsError, ZeroDivisionError): pass

try: pass
except ArithmeticError: pass
except AssertionError: pass
except AttributeError: pass
except BaseException: pass
except BufferError: pass
except BytesWarning: pass
except DeprecationWarning: pass
except EOFError: pass
except EnvironmentError: pass
except Exception: pass
except FloatingPointError: pass
except FutureWarning: pass
except GeneratorExit: pass
except IOError: pass
except ImportError: pass
except ImportWarning: pass
except IndentationError: pass
except IndexError: pass
except KeyError: pass
except KeyboardInterrupt: pass
except LookupError: pass
except MemoryError: pass
except NameError: pass
except NotImplementedError: pass
except OSError: pass
except OverflowError: pass
except PendingDeprecationWarning: pass
except ReferenceError: pass
except RuntimeError: pass
except RuntimeWarning: pass
except StandardError: pass
except StopIteration: pass
except SyntaxError: pass
except SyntaxWarning: pass
except SystemError: pass
except SystemExit: pass
except TabError: pass
except TypeError: pass
except UnboundLocalError: pass
except UnicodeDecodeError: pass
except UnicodeEncodeError: pass
except UnicodeError: pass
except UnicodeTranslateError: pass
except UnicodeWarning: pass
except UserWarning: pass
except ValueError: pass
except Warning: pass
except WindowsError: pass
except ZeroDivisionError: pass
except: pass

import struct, socket, os
try: pass
except struct.error: pass
except socket.error: pass
except os.error: pass

try: pass
except (struct.error, socket.error, os.error): pass

try:
    pass
    try:
        pass
        try:
            pass
        except ValueError:
            pass
    except TypeError:
        pass
except ValueError:
    pass

try:
    pass
except ValueError:
    pass
    try:
        pass
        try:
            pass
        except ValueError:
            pass
    except TypeError:
        pass

try: pass
except Exception as ex: pass

try: pass
except Exception, ex: pass

try: pass
except (ValueError, TypeError) as ex: pass

try: pass
except (ValueError, TypeError), ex: pass

try: pass
except not_included(): pass
except is_included: pass
except also.included: pass
except this.one.too.despite.having.lots.of.dots: pass
except but().not_me: pass
