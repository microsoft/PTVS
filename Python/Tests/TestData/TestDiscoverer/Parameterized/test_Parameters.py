
#http://doc.pytest.org/en/latest/example/parametrize.html

import pytest

from datetime import datetime, timedelta

testdata = [
    (datetime(2001, 12, 12), datetime(2001, 12, 11), timedelta(1)),
    (datetime(2001, 12, 11), datetime(2001, 12, 12), timedelta(-1)),
]


@pytest.mark.parametrize(",expected", testdata)
def test_timedistance_v0(a, expected):
    
    assert a == expected

@pytest.mark.parametrize("a,b,expected", testdata)
def test_timedistance_v0(a, b, expected):
    diff = a - b
    assert diff == expected


@pytest.mark.parametrize("a,b,expected", testdata, ids=["forward", "backward"])
def test_timedistance_v1(a, b, expected):
    diff = a - b
    assert diff == expected


def idfn(val):
    if isinstance(val, (datetime,)):
        # note this wouldn't show any hours/minutes/seconds
        return val.strftime("%Y%m%d")


@pytest.mark.parametrize("a,b,expected", testdata, ids=idfn)
def test_timedistance_v2(a, b, expected):
    diff = a - b
    assert diff == expected


@pytest.mark.parametrize(
    "a,b,expected",
    [
        pytest.param(
            datetime(2001, 12, 12), datetime(2001, 12, 11), timedelta(1), id="forward"
        ),
        pytest.param(
            datetime(2001, 12, 11), datetime(2001, 12, 12), timedelta(-1), id="backward"
        ),
    ],
)
def test_timedistance_v3(a, b, expected):
    diff = a - b
    assert diff == expected


@pytest.mark.parametrize(
    "test_input,expected",
    [
        ("3+5", 8),
        pytest.param("1+7", 8, marks=pytest.mark.basic),
        pytest.param("2+4", 6, marks=pytest.mark.basic, id="basic_2+4"),
    ],
)
def test_eval(test_input, expected):
    assert eval(test_input) == expected


@pytest.mark.parametrize(
    "test_input,expected",
    [
        ("::", "::"),
        ("\\n", "\\n"),
        ("\r", "\r"),
        ("\n","\n"),
        ("\\", "\\"),
        ("\n \n", "\n \n"),
        (":", ":"),
        ("3.0", "3.0"),
        (3.0, 3.0),
        (  """
           .. figure:: foo.jpg

 

              this is title
           """, """
           .. figure:: foo.jpg

 

              this is title
           """)
    ],
)
def test_pytest_finds_tests_with_special_strings(test_input, expected):
    assert test_input == expected


@pytest.mark.parametrize(
    "test_input,expected",
    [
      pytest.param("::", "::", id="colon")
    ],
)
def test_ids_found(test_input, expected):
    assert test_input == expected