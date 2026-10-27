import argparse
import os
import shutil
import tempfile
import zipfile
from pathlib import Path, PurePosixPath


def _is_reparse_point(path: Path) -> bool:
    if path.is_symlink():
        return True

    is_junction = getattr(path, "is_junction", None)
    return bool(is_junction and is_junction())


def _validate_archive_member(name: str) -> None:
    member = PurePosixPath(name.replace("\\", "/"))
    if member.is_absolute() or ".." in member.parts:
        raise ValueError(f"Unsafe archive member: {name}")

    if member.parts and ":" in member.parts[0]:
        raise ValueError(f"Unsafe archive member: {name}")


def create_archive(source: Path, archive: Path) -> None:
    source = source.resolve(strict=True)
    archive = archive.resolve()
    if not source.is_dir():
        raise NotADirectoryError(source)
    if archive.is_relative_to(source):
        raise ValueError(f"Archive must be outside the source directory: {archive}")

    archive.parent.mkdir(parents=True, exist_ok=True)
    temporary_archive = archive.with_name(f"{archive.name}.tmp")
    temporary_archive.unlink(missing_ok=True)

    file_count = 0
    source_size = 0

    try:
        with zipfile.ZipFile(
            temporary_archive,
            mode="w",
            compression=zipfile.ZIP_DEFLATED,
            compresslevel=1,
            allowZip64=True,
            strict_timestamps=False,
        ) as output:
            for root, directories, files in os.walk(source, followlinks=False):
                root_path = Path(root)
                directories.sort()
                files.sort()

                for directory in directories:
                    path = root_path / directory
                    if _is_reparse_point(path):
                        raise ValueError(f"Cannot cache directory reparse point: {path}")

                if not directories and not files:
                    relative = root_path.relative_to(source).as_posix()
                    if relative != ".":
                        output.writestr(f"{relative}/", b"")

                for file_name in files:
                    path = root_path / file_name
                    if _is_reparse_point(path):
                        raise ValueError(f"Cannot cache file reparse point: {path}")

                    relative = path.relative_to(source).as_posix()
                    size = path.stat().st_size
                    output.write(path, relative)
                    file_count += 1
                    source_size += size

                    if file_count % 500 == 0:
                        print(
                            f"Archived {file_count} files "
                            f"({source_size} source bytes)...",
                            flush=True,
                        )

        os.replace(temporary_archive, archive)
    except BaseException:
        temporary_archive.unlink(missing_ok=True)
        raise

    print(
        f"Created Glass cache archive {archive}: "
        f"{file_count} files, {source_size} source bytes, "
        f"{archive.stat().st_size} archive bytes",
        flush=True,
    )


def extract_archive(archive: Path, destination: Path) -> None:
    archive = archive.resolve(strict=True)
    destination = destination.resolve()
    temporary_destination = destination.with_name(f"{destination.name}.extracting")

    with zipfile.ZipFile(archive, mode="r", allowZip64=True) as source:
        for member in source.infolist():
            _validate_archive_member(member.filename)

        if temporary_destination.exists():
            shutil.rmtree(temporary_destination)
        temporary_destination.mkdir(parents=True)

        try:
            source.extractall(temporary_destination)
            file_count = sum(not member.is_dir() for member in source.infolist())

            if destination.exists():
                shutil.rmtree(destination)
            os.replace(temporary_destination, destination)
        except BaseException:
            shutil.rmtree(temporary_destination, ignore_errors=True)
            raise

    print(
        f"Extracted {file_count} files from {archive} to {destination}",
        flush=True,
    )


def self_test() -> None:
    with tempfile.TemporaryDirectory() as temporary_directory:
        root = Path(temporary_directory)
        source = root / "source"
        archive = root / "cache.zip"
        destination = root / "destination"

        (source / "nested").mkdir(parents=True)
        (source / "empty").mkdir()
        (source / "root.txt").write_bytes(b"root")
        (source / "nested" / "data.bin").write_bytes(bytes(range(256)) * 16)

        create_archive(source, archive)
        extract_archive(archive, destination)

        if (destination / "root.txt").read_bytes() != b"root":
            raise AssertionError("Root file did not round-trip")
        if (destination / "nested" / "data.bin").read_bytes() != bytes(range(256)) * 16:
            raise AssertionError("Nested file did not round-trip")
        if not (destination / "empty").is_dir():
            raise AssertionError("Empty directory did not round-trip")

        unsafe_archive = root / "unsafe.zip"
        with zipfile.ZipFile(unsafe_archive, mode="w") as output:
            output.writestr("../outside.txt", b"unsafe")

        try:
            extract_archive(unsafe_archive, root / "unsafe-destination")
        except ValueError:
            pass
        else:
            raise AssertionError("Unsafe archive member was accepted")

    print("Glass cache self-test passed", flush=True)


def main() -> None:
    parser = argparse.ArgumentParser(description="Create or extract the Glass runtime cache.")
    subparsers = parser.add_subparsers(dest="command", required=True)

    create_parser = subparsers.add_parser("create")
    create_parser.add_argument("--source", type=Path, required=True)
    create_parser.add_argument("--archive", type=Path, required=True)

    extract_parser = subparsers.add_parser("extract")
    extract_parser.add_argument("--archive", type=Path, required=True)
    extract_parser.add_argument("--destination", type=Path, required=True)

    subparsers.add_parser("self-test")
    args = parser.parse_args()

    if args.command == "create":
        create_archive(args.source, args.archive)
    elif args.command == "extract":
        extract_archive(args.archive, args.destination)
    else:
        self_test()


if __name__ == "__main__":
    main()
