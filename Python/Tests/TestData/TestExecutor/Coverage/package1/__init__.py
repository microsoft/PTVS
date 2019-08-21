def partial_coverage(val):
    if val < 0:
        return 'negative'
    elif val > 0:
        return 'positive'
    else:
        return 'zero'

def full_coverage():
    return True

def no_coverage():
    return True
