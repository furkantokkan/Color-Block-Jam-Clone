"""Checks every native library in an APK or AAB for 16 KB page-size compatibility.

Android 15+ devices can run with 16 KB memory pages, and Google Play requires 16 KB support for apps targeting
Android 15 or newer. A library whose PT_LOAD segments are aligned to less than 16384 bytes makes Android 16 show the
"Android App Compatibility" dialog at launch (seen on 2026-09-29 with Nice Vibrations' liblofelt_sdk.so).
Stored (uncompressed) libraries must also start on a 16 KB boundary inside the zip.

Usage: python check_16kb.py <file.apk|file.aab>
Exit code 1 when any library fails.
"""
import struct
import sys
import zipfile


def load_alignments(data):
    if data[:4] != b"\x7fELF":
        return None
    if data[4] != 2:  # 32-bit ELF (armeabi-v7a): read the 32-bit program headers
        phoff = struct.unpack_from("<I", data, 0x1C)[0]
        phentsize, phnum = struct.unpack_from("<HH", data, 0x2A)
        return [struct.unpack_from("<I", data, phoff + i * phentsize + 0x1C)[0]
                for i in range(phnum) if struct.unpack_from("<I", data, phoff + i * phentsize)[0] == 1]
    phoff = struct.unpack_from("<Q", data, 0x20)[0]
    phentsize, phnum = struct.unpack_from("<HH", data, 0x36)
    return [struct.unpack_from("<Q", data, phoff + i * phentsize + 0x30)[0]
            for i in range(phnum) if struct.unpack_from("<I", data, phoff + i * phentsize)[0] == 1]


def main():
    if len(sys.argv) != 2:
        raise SystemExit(__doc__)
    failed = False
    with zipfile.ZipFile(sys.argv[1]) as archive:
        for info in archive.infolist():
            if not info.filename.endswith(".so"):
                continue
            aligns = load_alignments(archive.read(info.filename))
            if aligns is None:
                print(f"SKIP not ELF  {info.filename}")
                continue
            elf_ok = min(aligns) >= 16384 if aligns else False
            zip_ok = True
            if info.compress_type == zipfile.ZIP_STORED:
                data_offset = info.header_offset + 30 + len(info.filename.encode()) + len(info.extra)
                zip_ok = data_offset % 16384 == 0
            ok = elf_ok and zip_ok
            failed |= not ok
            storage = "stored" if info.compress_type == zipfile.ZIP_STORED else "compressed"
            print(f"{'OK  ' if ok else 'FAIL'} load-align={min(aligns) if aligns else 0:>6} {storage:10} "
                  f"zip-align={'ok' if zip_ok else 'BAD'} {info.file_size / 1024:9.0f} KB  {info.filename}")
    sys.exit(1 if failed else 0)


if __name__ == "__main__":
    main()
