import argparse
import io
import json
import os
import urllib.request as url_lib
import zipfile

DEBUGGER_PACKAGE = "debugpy"
DEBUGGER_PYTHON_VERSIONS = ("cp35", "cp36", "cp37", "cp38")
DEBUGGER_EXCLUDED_PLATFORMS = ("manylinux", "macosx")


def _contains(s, parts=()):
    return any(p for p in parts if p in s)


def _get_package_data():
    json_uri = "https://pypi.org/pypi/{0}/json".format(DEBUGGER_PACKAGE)
    # Response format: https://warehouse.readthedocs.io/api-reference/json/#project
    # Release metadata format: https://github.com/pypa/interoperability-peps/blob/master/pep-0426-core-metadata.rst
    with url_lib.urlopen(json_uri) as response:
        return json.loads(response.read())


def _get_debugger_wheel_urls(data, version):
    return list(
        r["url"]
        for r in data["releases"][version]
        if _contains(r["url"], DEBUGGER_PYTHON_VERSIONS) and not _contains(r["url"], DEBUGGER_EXCLUDED_PLATFORMS)
    )


def _download_and_extract(root, url, version):
    root = os.getcwd() if root is None or root == "." else root
    prefix = os.path.join("debugpy-{0}.data".format(version), "purelib")
    with url_lib.urlopen(url) as response:
        # Extract only the contents of the purelib subfolder (parent folder of debugpy),
        # since debugpy files rely on the presence of a 'debugpy' folder.
        with zipfile.ZipFile(io.BytesIO(response.read()), "r") as wheel:
            for zip_info in wheel.infolist():
                # Ignore dist info since we are merging multiple wheels
                if ".dist-info" in zip_info.filename:
                    continue
                # Normalize path for Windows, the wheel folder structure
                # uses forward slashes.
                normalized = os.path.normpath(zip_info.filename)
                # Flatten the folder structure.
                zip_info.filename = normalized.split(prefix)[-1]
                wheel.extract(zip_info, root)


def main(root, ver):
    data = _get_package_data()

    for url in _get_debugger_wheel_urls(data, ver):
        _download_and_extract(root, url, ver)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument('version', help='Version of debugpy package.')
    parser.add_argument('outputdir', help='Output directory under which debugpy directory will be created.')
    args = parser.parse_args()
    main(args.outputdir, args.version)
