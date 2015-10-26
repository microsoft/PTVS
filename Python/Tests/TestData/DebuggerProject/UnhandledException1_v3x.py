try: raise Exception()  # does not break
except: print('handled 1')

try: raise Exception()  # does not break
except Exception: print('handled 2')

try: raise Exception()  # does not break
except Exception as e: print('handled 3')

try: raise Exception()  # does not break
except BaseException: print('handled 5')

try: raise Exception()  # does not break
except BaseException as e: print('handled 6')

try: raise Exception()  # does not break
except (Exception,): print('handled 8')

try: raise Exception()  # does not break
except (Exception,) as e: print('handled 9')

try: raise Exception()  # does not break
except (Exception, ValueError): print('handled 11')

try: raise Exception()  # does not break
except (Exception, ValueError) as e: print('handled 12')


try: raise ValueError()  # does not break
except: print('handled 14')

try: raise ValueError()  # does not break
except Exception: print('handled 15')

try: raise ValueError()  # does not break
except Exception as e: print('handled 16')

try: raise ValueError()  # does not break
except BaseException: print('handled 18')

try: raise ValueError()  # does not break
except BaseException as e: print('handled 19')

try: raise ValueError()  # does not break
except (Exception,): print('handled 21')

try: raise ValueError()  # does not break
except (Exception,) as e: print('handled 22')

try: raise ValueError()  # does not break
except (Exception, ValueError): print('handled 24')

try: raise ValueError()  # does not break
except (Exception, ValueError) as e: print('handled 25')

raise Exception()   # breaks

