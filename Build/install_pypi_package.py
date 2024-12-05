# This is a modified version of https://github.com/microsoft/vscode-python/blob/main/pythonFiles/install_debugpy.py

# It downloads and extracts a specified package version from PyPi to a specified directory.
# Doing a `pip install` will only install the package for the python version that is running the script,
# but since we don't know what version of python the user will be running, we want to download ALL the wheels and
# perform a "fat install" by merging all the wheels into the same directory.

import argparse
import io
import json
import os
import urllib.request as url_lib
import zipfile
import ssl
import certifi

# if this import fails, run PreBuild.ps1, which will pip install packaging
from packaging.version import parse as version_parser

# only install wheels for these python versions
PYTHON_VERSIONS = ("cp38", "cp39", "cp310", "cp311", "cp312", "cp313")

# don't install wheels for these platforms
EXCLUDED_PLATFORMS = ("manylinux", "macosx")


# Check if any of the parts are in the string
def contains(s, parts=()):
    return any(p for p in parts if p in s)


# Get json containing all package metadata and all releases
def get_package_data(packageName):
    json_uri = f"https://pypi.org/pypi/{packageName}/json"
    # Response format: https://warehouse.readthedocs.io/api-reference/json/#project
    # Release metadata format: https://github.com/pypa/interoperability-peps/blob/master/pep-0426-core-metadata.rst
    context = ssl.create_default_context(cafile=certifi.where())
    with url_lib.urlopen(json_uri, context=context) as response:
        return json.loads(response.read())


# Get the wheel urls for a specific release
def get_debugger_wheel_urls(data, version):
    return list(
        r["url"]
        for r in data["releases"][version]
        if contains(r["url"], PYTHON_VERSIONS)
        and not contains(r["url"], EXCLUDED_PLATFORMS)
    )


# Download the wheel from the url and extract the files in the appropriate layout
def download_and_extract(installDir, url, version):

    if (installDir is None or installDir == "."):
        installDir = os.getcwd()

    context = ssl.create_default_context(cafile=certifi.where())
    with url_lib.urlopen(url, context=context) as response:
        data = response.read()
        with zipfile.ZipFile(io.BytesIO(data), "r") as wheel:
            for zip_info in wheel.infolist():

                # Ignore dist info since we are merging multiple wheels
                if ".dist-info/" in zip_info.filename:
                    continue

                wheel.extract(zip_info.filename, installDir)


# parse the command line args and return them
def parse_args():

    parser = argparse.ArgumentParser()
    parser.add_argument(
        "name",
        help='Name of package.',
    )
    parser.add_argument(
        "version",
        help='Version of package.',
        default="latest",
    )
    parser.add_argument(
        "installDir",
        help="Output directory under which package directory will be created.",
    )
    args = parser.parse_args()
    return args


def main():

    args = parse_args()

    packageName = args.name
    packageVersion = args.version
    installDir = args.installDir

    data = get_package_data(packageName)

    # We don't yank individual files in our releases; either the whole release
    # is yanked or it isn't. So go through all releases remove all yanked ones.
    for release in list(data["releases"].keys()):
        if any(r["yanked"] for r in data["releases"][release]):
            del data["releases"][release]

    # if version is "latest", use the max version from the data
    if packageVersion == "latest":
        packageVersion = max(data["releases"].keys(), key=version_parser)

    for url in get_debugger_wheel_urls(data, packageVersion):
        download_and_extract(installDir, url, packageVersion)


if __name__ == "__main__":
    main()
