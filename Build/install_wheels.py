# Extracts all files in all wheels in a specified directory (non recursive) to a specified location.
# The wheels are all merged into the same directory, and files with the same names are overwritten.
# The dist-info directories are ignored.

import argparse
import os
from zipfile import ZipFile


def extract_wheels(wheelDir, outputDir):
    for file in os.listdir(wheelDir):

        fullPath = os.path.join(wheelDir, file)

        # ignore directories
        if os.path.isdir(fullPath):
            continue

        # ignore non wheel files
        if not fullPath.endswith(".whl"):
            continue

        with ZipFile(fullPath, "r") as wheel:
            for info in wheel.infolist():

                # Ignore dist info since we are merging multiple wheels
                if ".dist-info/" in info.filename:
                    continue

                wheel.extract(info.filename, outputDir)


def parse_args():

    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--wheelDir",
        help='Directory containing wheels to extract',
    )
    parser.add_argument(
        "--outputDir",
        help="Output directory where the wheels will be merged.",
    )
    args = parser.parse_args()
    return args


def main():

    args = parse_args()
    extract_wheels(args.wheelDir, args.outputDir)


if __name__ == "__main__":
    main()
