import os
import sys

def sync_tfs():
    curr_dir = os.getcwd()
    
    os.chdir('\\ptvs')
    print(r'tf get . /r > syncresults.txt')
    os.system(r'tf get . /r > syncresults.txt')
    with open('syncresults.txt') as f:
        if f.readline().startswith('All files are up to date'):
            os.chdir(curr_dir)
            return False
    os.chdir(curr_dir)
    return True

