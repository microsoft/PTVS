'''Removes filename fields from all IDB files in a given directory.

Because filenames are now validated on load, we want to omit them from
any test databases to avoid having them be removed during the run.

cd to the directory containing the files and then run this script to
strip the filename fields.
'''

import os
import pickle

for fname in os.listdir('.'):
    if not fname.endswith('.idb'):
        continue

    with open(fname, 'rb') as f:
        d = pickle.load(f)
    if 'filename' in d:
        del d['filename']
        with open(fname, 'wb') as f:
            pickle.dump(d, f, 1)
        print('Processed ' + fname)
