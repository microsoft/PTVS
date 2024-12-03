# This is a slightly modified version of https://github.com/microsoft/vscode-python/blob/main/pythonFiles/install_debugpy.py

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

DEBUGGER_PYTHON_VERSIONS = ("cp38", "cp39", "cp310", "cp311", "cp312", "cp313")
DEBUGGER_EXCLUDED_PLATFORMS = ("manylinux", "macosx")


def _contains(s, parts=()):
    return any(p for p in parts if p in s)


# Get json containing all debugpy package metadata and all releases
def _get_package_data():
    json_uri = "https://pypi.org/pypi/debugpy/json"
    # Response format: https://warehouse.readthedocs.io/api-reference/json/#project
    # Release metadata format: https://github.com/pypa/interoperability-peps/blob/master/pep-0426-core-metadata.rst
    context = ssl.create_default_context(cafile=certifi.where())
    with url_lib.urlopen(json_uri, context=context) as response:
        return json.loads(response.read())


# Get the wheel url for a specific release
def _get_debugger_wheel_urls(data, version):
    return list(
        r["url"]
        for r in data["releases"][version]
        if _contains(r["url"], DEBUGGER_PYTHON_VERSIONS)
        and not _contains(r["url"], DEBUGGER_EXCLUDED_PLATFORMS)
    )


# Download the wheel from the url and extract the files in the appropriate layout
def _download_and_extract(root, url, version):
    root = os.getcwd() if root is None or root == "." else root
    # print(url)
    context = ssl.create_default_context(cafile=certifi.where())
    with url_lib.urlopen(url, context=context) as response:
        data = response.read()
        with zipfile.ZipFile(io.BytesIO(data), "r") as wheel:
            for zip_info in wheel.infolist():
                # Ignore dist info since we are merging multiple wheels
                if ".dist-info/" in zip_info.filename:
                    continue
                # print("\t" + zip_info.filename)
                wheel.extract(zip_info.filename, root)


def main(root, ver):
    data = _get_package_data()

    # We don't yank individual files in our releases; either the whole release
    # is yanked or it isn't. So go through all releases remove all yanked ones.
    for release in list(data["releases"].keys()):
        if any(r["yanked"] for r in data["releases"][release]):
            del data["releases"][release]

    # if version is "latest", use the max version from the data
    if ver == "latest":
        ver = max(data["releases"].keys(), key=version_parser)

    for url in _get_debugger_wheel_urls(data, ver):
        _download_and_extract(root, url, ver)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "version",
        help='Version of debugpy package. If "latest" is specified, the latest release will be installed',
    )
    parser.add_argument(
        "outputdir",
        help="Output directory under which debugpy directory will be created.",
    )
    args = parser.parse_args()
    main(args.outputdir, args.version)
