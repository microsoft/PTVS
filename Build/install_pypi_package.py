# This is a modified version of https://github.com/microsoft/vscode-python/blob/main/pythonFiles/install_debugpy.py

# It downloads and extracts a specified package version from an authenticated Azure Artifacts feed.
# Doing a `pip install` will only install the package for the python version that is running the script,
# but since we don't know what version of python the user will be running, we want to download ALL the wheels and
# perform a "fat install" by merging all the wheels into the same directory.

import argparse
import base64
import hashlib
from html.parser import HTMLParser
import io
import os
import re
import ssl
import urllib.parse
import urllib.request as url_lib
import zipfile

import certifi

# If this import fails, run PreBuild.ps1, which installs packaging from the authenticated feed.
from packaging.utils import canonicalize_name, parse_wheel_filename
from packaging.version import parse as version_parser

# only install wheels for these python versions
PYTHON_VERSIONS = ("cp38", "cp39", "cp310", "cp311", "cp312", "cp313")

# don't install wheels for these platforms
EXCLUDED_PLATFORMS = ("manylinux", "macosx")

AZURE_ARTIFACTS_HOSTS = ("pkgs.dev.azure.com", ".pkgs.visualstudio.com")
PACKAGE_NAME_NORMALIZER = re.compile(r"[-_.]+")

# Public feeds must never be used, even indirectly through a redirect.
FORBIDDEN_HOSTS = frozenset({
    "pypi.org",
    "pypi.python.org",
    "files.pythonhosted.org",
    "upload.pypi.org",
    "pypi.nvidia.com",
})


def _is_forbidden_host(hostname):
    if not hostname:
        return False
    hostname = hostname.lower()
    return any(hostname == denied or hostname.endswith("." + denied) for denied in FORBIDDEN_HOSTS)


def _is_azure_artifacts_host(hostname):
    if not hostname:
        return False
    hostname = hostname.lower()
    for allowed in AZURE_ARTIFACTS_HOSTS:
        if allowed.startswith("."):
            if hostname.endswith(allowed):
                return True
        elif hostname == allowed:
            return True
    return False


class _SfiRedirectHandler(url_lib.HTTPRedirectHandler):
    # Azure Artifacts responds with a 303 pointing at a SAS-signed blob URL;
    # refuse to follow any redirect that leaves Microsoft-owned infrastructure.
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        new_host = urllib.parse.urlsplit(newurl).hostname
        if _is_forbidden_host(new_host):
            raise RuntimeError(f"Refusing to follow redirect to {new_host}.")
        return super().redirect_request(req, fp, code, msg, headers, newurl)


class SimpleIndexParser(HTMLParser):
    def __init__(self):
        super().__init__()
        self.links = []

    def handle_starttag(self, tag, attrs):
        if tag != "a":
            return

        attributes = dict(attrs)
        href = attributes.get("href")
        if href:
            self.links.append((href, "data-yanked" in attributes))


# Check if any of the parts are in the string
def contains(s, parts=()):
    return any(p for p in parts if p in s)


def get_feed_configuration():
    index_url = os.environ.get("PIP_INDEX_URL")
    if not index_url:
        raise RuntimeError(
            "PIP_INDEX_URL is required. Authenticate to DevDiv/Pylance_PublicPackages before running this script."
        )

    if os.environ.get("PIP_EXTRA_INDEX_URL"):
        raise RuntimeError("PIP_EXTRA_INDEX_URL is not allowed because Python packages must use a single internal feed.")

    parsed_url = urllib.parse.urlsplit(index_url)
    if parsed_url.scheme != "https" or not _is_azure_artifacts_host(parsed_url.hostname):
        raise RuntimeError(
            "PIP_INDEX_URL must use https://pkgs.dev.azure.com/ or https://<org>.pkgs.visualstudio.com/."
        )

    if not parsed_url.username or not parsed_url.password:
        raise RuntimeError("PIP_INDEX_URL must contain credentials supplied by PipAuthenticate@1.")

    port = f":{parsed_url.port}" if parsed_url.port else ""
    clean_index_url = urllib.parse.urlunsplit(
        (parsed_url.scheme, f"{parsed_url.hostname}{port}", parsed_url.path, parsed_url.query, "")
    )
    credentials = f"{urllib.parse.unquote(parsed_url.username)}:{urllib.parse.unquote(parsed_url.password)}"
    authorization = base64.b64encode(credentials.encode("utf-8")).decode("ascii")
    return clean_index_url, f"Basic {authorization}"


def read_authenticated_url(url, authorization):
    parsed_url = urllib.parse.urlsplit(url)
    if parsed_url.scheme != "https" or not _is_azure_artifacts_host(parsed_url.hostname):
        raise RuntimeError(f"Refusing to initiate a download from {parsed_url.hostname or url}.")

    request_url = urllib.parse.urlunsplit(
        (parsed_url.scheme, parsed_url.netloc, parsed_url.path, parsed_url.query, "")
    )
    request = url_lib.Request(request_url)
    # add_unredirected_header ensures the Basic credential is NOT resent when
    # Azure Artifacts redirects to its SAS-signed blob storage backend.
    request.add_unredirected_header("Authorization", authorization)
    context = ssl.create_default_context(cafile=certifi.where())
    opener = url_lib.build_opener(url_lib.HTTPSHandler(context=context), _SfiRedirectHandler())
    with opener.open(request) as response:
        return response.read()


# Get wheel metadata from the authenticated feed's PEP 503 simple index.
def get_package_data(package_name, index_url, authorization):
    normalized_name = PACKAGE_NAME_NORMALIZER.sub("-", package_name).lower()
    package_url = urllib.parse.urljoin(index_url.rstrip("/") + "/", normalized_name + "/")
    parser = SimpleIndexParser()
    parser.feed(read_authenticated_url(package_url, authorization).decode("utf-8"))

    releases = {}
    expected_name = canonicalize_name(package_name)
    for href, yanked in parser.links:
        wheel_url = urllib.parse.urljoin(package_url, href)
        filename = os.path.basename(urllib.parse.urlsplit(wheel_url).path)
        if not filename.endswith(".whl"):
            continue

        wheel_name, version, _, _ = parse_wheel_filename(filename)
        if canonicalize_name(wheel_name) != expected_name:
            continue

        release = releases.setdefault(version, {"urls": [], "yanked": False})
        release["urls"].append(wheel_url)
        release["yanked"] = release["yanked"] or yanked

    if not releases:
        raise RuntimeError(f"No wheels for {package_name} were found in PIP_INDEX_URL.")

    return releases


# Get the wheel urls for a specific release
def get_debugger_wheel_urls(releases, version):
    return list(
        url
        for url in releases[version]["urls"]
        if contains(url, PYTHON_VERSIONS)
        and not contains(url, EXCLUDED_PLATFORMS)
    )


# Download the wheel from the url and extract the files in the appropriate layout
def download_and_extract(install_dir, url, authorization):

    if install_dir is None or install_dir == ".":
        install_dir = os.getcwd()

    cache_dir = os.environ.get("PTVS_PYPI_WHEEL_CACHE_DIR")
    cache_path = None
    if cache_dir:
        os.makedirs(cache_dir, exist_ok=True)
        url_path = urllib.parse.urlparse(url).path
        filename = os.path.basename(url_path) or hashlib.sha256(url.encode("utf-8")).hexdigest()
        cache_path = os.path.join(cache_dir, filename)

    data = None
    if cache_path and os.path.exists(cache_path):
        with open(cache_path, "rb") as f:
            data = f.read()

    if data is None:
        data = read_authenticated_url(url, authorization)
        if cache_path:
            with open(cache_path, "wb") as f:
                f.write(data)

    fragment = urllib.parse.parse_qs(urllib.parse.urlsplit(url).fragment)
    expected_hashes = fragment.get("sha256")
    if expected_hashes and hashlib.sha256(data).hexdigest() != expected_hashes[0]:
        raise RuntimeError(f"SHA256 verification failed for {os.path.basename(cache_path or url)}.")

    with zipfile.ZipFile(io.BytesIO(data), "r") as wheel:
        for zip_info in wheel.infolist():

            # Ignore dist info since we are merging multiple wheels
            if ".dist-info/" in zip_info.filename:
                continue

            wheel.extract(zip_info.filename, install_dir)


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

    package_name = args.name
    package_version = args.version
    install_dir = args.installDir

    index_url, authorization = get_feed_configuration()
    releases = get_package_data(package_name, index_url, authorization)

    available_releases = [version for version, release in releases.items() if not release["yanked"]]
    if not available_releases:
        raise RuntimeError(f"No non-yanked releases of {package_name} were found in PIP_INDEX_URL.")

    if package_version == "latest":
        selected_version = max(available_releases)
    else:
        selected_version = version_parser(package_version)
        if selected_version not in available_releases:
            raise RuntimeError(f"{package_name} {package_version} was not found in PIP_INDEX_URL.")

    wheel_urls = get_debugger_wheel_urls(releases, selected_version)
    if not wheel_urls:
        raise RuntimeError(f"No supported Windows wheels were found for {package_name} {selected_version}.")

    for url in wheel_urls:
        download_and_extract(install_dir, url, authorization)


if __name__ == "__main__":
    main()
