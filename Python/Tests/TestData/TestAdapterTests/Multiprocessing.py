import unittest

def square(x):
    return x*x

class Multiprocessing(unittest.TestCase):
    def test_pool(self):
        from multiprocessing import Pool

        pool = Pool()
        
        asyncresults = [pool.apply_async(square, [t]) for t in range(2)]
    
        results = [r.get() for r in asyncresults]
        expected = [t*t for t in range(2)]
        pool.close()

        self.assertListEqual(expected, results)

if __name__ == '__main__':
    unittest.main()
