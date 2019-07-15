import pytest

VAL = 1 

@pytest.mark.webtest
def test_webtest():
    pass

@pytest.mark.skip(reason="skip unconditionally")
def test_skip():
    pass

@pytest.mark.skipif(VAL == 1, reason="skip VAL == 1")
def test_skipif_skipped():
    pass

@pytest.mark.skipif(VAL == 0, reason="skip VAL == 0")
def test_skipif_not_skipped():
    pass
