
if __name__ == '__main__':
    import sys
    with open(sys.argv[-1], 'w') as f:
        f.write(sys.argv[-2])
