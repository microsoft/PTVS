import os
import unittest

class EnvironmentVariableTests(unittest.TestCase):
    def test_variable(self):
        assert os.environ['USER_ENV_VAR'] == '123'
