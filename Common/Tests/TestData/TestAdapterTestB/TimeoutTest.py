import unittest
import time

class TimeoutTest(unittest.TestCase):
    def test_wait_10_secs(self):
        print("before sleep")
        time.sleep(10)
        #while True:
        #    pass
        print("after sleep")

if __name__ == '__main__':
    unittest.main()
