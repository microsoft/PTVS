try:
    try:
        raise ValueError()  # does not break
    except AttributeError:
        assert False, "Should not ever run this"
    
    assert False, "Should not ever run this"
except ValueError:
    pass

try:
    raise ValueError()  # breaks
    try: raise ValueError()
    except: assert False, "Should not ever run this"
except AttributeError:
    pass
