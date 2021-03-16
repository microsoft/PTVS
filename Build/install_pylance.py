import argparse
import tarfile

# This code is passed a tarball to extract with pylance in it.
def main(tarball):
    tar = tarfile.open(tarball, "r:gz")
    tar.extractall("../Pylance");
    tar.close()

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument('--pylanceTgz', help='Tarball with pylance in it.', required=True)
    args = parser.parse_args()
    main(args.pylanceTgz)
