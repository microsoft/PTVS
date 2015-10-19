try: raise Exception()  # does not break
except: print('handled 1')

try: raise Exception()  # does not break
except Exception: print('handled 2')

try: raise Exception()  # does not break
except Exception, e: print('handled 4')

try: raise Exception()  # does not break
except BaseException: print('handled 5')

try: raise Exception()  # does not break
except BaseException, e: print('handled 7')

try: raise Exception()  # does not break
except (Exception,): print('handled 8')

try: raise Exception()  # does not break
except (Exception,), e: print('handled 10')

try: raise Exception()  # does not break
except (Exception, ValueError): print('handled 11')

try: raise Exception()  # does not break
except (Exception, ValueError), e: print('handled 13')



try: raise ValueError()  # does not break
except: print('handled 14')

try: raise ValueError()  # does not break
except Exception: print('handled 15')

try: raise ValueError()  # does not break
except Exception, e: print('handled 17')

try: raise ValueError()  # does not break
except BaseException: print('handled 18')

try: raise ValueError()  # does not break
except BaseException, e: print('handled 20')

try: raise ValueError()  # does not break
except (Exception,): print('handled 21')

try: raise ValueError()  # does not break
except (Exception,), e: print('handled 23')

try: raise ValueError()  # does not break
except (Exception, ValueError): print('handled 24')

try: raise ValueError()  # does not break
except (Exception, ValueError), e: print('handled 26')

raise Exception()   # breaks

