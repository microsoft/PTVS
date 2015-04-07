import os
import unittest

class EnvironmentTests(unittest.TestCase):
    def test_environ(self):
        assert os.environ['USER_ENV_VAR'] == '123'
