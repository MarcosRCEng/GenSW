"""Decrypt and verify one homologation set using the independent DevOPS key."""
import argparse
import datetime as dt
import fcntl
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import stat
import subprocess
import sys
import tarfile
import tempfile

ID = re.compile(r"gensw-\d{8}T\d{6}Z-[0-9a-f]{8}\Z")
SHA = re.compile(r"[0-9a-fA-F]{64}\Z")
TOP = {"database.dump", "images", "manifest.json", "capture-metadata.json", "source-snapshot-private.json", "capture-private.log"}


def digest(path):
    value = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024**2), b""):
            value.update(block)
    return value.hexdigest()


def private(path, directory=False):
    info = path.lstat()
    if info.st_uid != os.getuid() or (stat.S_IMODE(info.st_mode) != (0o700 if directory else 0o600)):
        raise RuntimeError("private owner/mode required")
    if path.is_symlink() or (directory and not path.is_dir()) or (not directory and not path.is_file()):
        raise RuntimeError("unsafe private path")


def configuration():
    home = Path.home().resolve()
    recovery = home / ".local/share/gensw-recovery-475"
    private(recovery, True)
    path = recovery / "crypto.json"
    private(path)
    cfg = json.loads(path.read_text())
    root = home / ".local/share/gensw-backup-475"
    scratch = home / ".local/share/gensw-restore-475"
    if Path(cfg["setRoot"]) != root or Path(cfg["scratchRoot"]) != scratch:
        raise RuntimeError("unexpected namespace")
    private(root, True)
    private(scratch, True)
    private(Path(cfg["identityFile"]))
    if Path(cfg["identityFile"]).parent != recovery:
        raise RuntimeError("unexpected recovery identity")
    return cfg, root, scratch


def inspect_archive(archive_path, extraction):
    with tarfile.open(archive_path, "r:") as archive:
        names, total = set(), 0
        for member in archive.getmembers():
            name = member.name
            while name.startswith("./"):
                name = name[2:]
            if name in ("", ".") and member.isdir():
                continue
            path = PurePosixPath(name)
            if (not name or "\\" in name or path.is_absolute() or ".." in path.parts or
                    path.parts[0] not in TOP or name in names or not (member.isdir() or member.isfile())):
                raise RuntimeError("unsafe archive member")
            if len(path.parts) > 1 and (path.parts[0] != "images" or len(path.parts) != 2):
                raise RuntimeError("unexpected archive hierarchy")
            names.add(name)
            total += member.size
        if total > 100 * 1024**3 or shutil.disk_usage(extraction).free - total < 2 * 1024**3:
            raise RuntimeError("insufficient private extraction capacity")
        archive.extractall(extraction, filter="data")
    metadata = json.loads((extraction / "capture-metadata.json").read_text(encoding="utf-8-sig"))
    if metadata.get("task") != 475 or metadata.get("sourceAlias") != "PILOT-472-CLEAN" or metadata.get("environment") != "Homologation":
        raise RuntimeError("unexpected source/environment")
    manifest = json.loads((extraction / "manifest.json").read_text(encoding="utf-8-sig"))
    if not isinstance(manifest, dict):
        raise RuntimeError("invalid manifest")
    images = extraction / "images"
    actual = {p.name for p in images.iterdir() if p.is_file() and not p.is_symlink()}
    if actual != set(manifest) or any(not p.is_file() or p.is_symlink() for p in images.iterdir()):
        raise RuntimeError("image inventory mismatch")
    for name, entry in manifest.items():
        if not name or PurePosixPath(name).name != name or "\\" in name or name in (".", ".."):
            raise RuntimeError("unsafe manifest reference")
        file = images / name
        if file.stat().st_size != entry["bytes"] or digest(file) != entry["sha256"].lower():
            raise RuntimeError("image digest mismatch")
    dump = extraction / "database.dump"
    if dump.stat().st_size != metadata["databaseDumpBytes"] or digest(dump) != metadata["databaseDumpSha256"].lower():
        raise RuntimeError("database dump digest mismatch")
    snapshot = json.loads((extraction / "source-snapshot-private.json").read_text(encoding="utf-8-sig"))
    if (len(snapshot["tables"]) != metadata["tableCount"] or len(snapshot["migrations"]) != metadata["migrationCount"] or
            len(manifest) != metadata["imageFiles"] or sum(e["bytes"] for e in manifest.values()) != metadata["imageBytes"] or
            not metadata.get("allReferencedDerivativesPresentIncludingInactive")):
        raise RuntimeError("capture metadata mismatch")
    return metadata


def verify(backup_id, expected_sha, stream_bundle=False, retain_for_restore=False):
    if not ID.fullmatch(backup_id) or not SHA.fullmatch(expected_sha):
        raise RuntimeError("invalid identifier/digest")
    cfg, root, scratch = configuration()
    folder = root / backup_id
    private(folder, True)
    payload = folder / "payload.age"
    receipt_path = folder / "receipt.json"
    private(payload)
    private(receipt_path)
    receipt = json.loads(receipt_path.read_text())
    actual = digest(payload)
    if (actual != expected_sha.lower() or actual != receipt["ciphertextSha256"] or
            payload.stat().st_size != receipt["bytes"] or receipt.get("sourceAlias") != "PILOT-472-CLEAN" or
            receipt.get("environment") != "Homologation" or receipt.get("task") != 475):
        raise RuntimeError("ciphertext/receipt mismatch")
    with tempfile.TemporaryDirectory(prefix=".verify-" + backup_id + "-", dir=scratch) as temp:
        stage = Path(temp)
        stage.chmod(0o700)
        archive_path = stage / "bundle.tar"
        with (stage / "decrypt-private.log").open("wb") as log:
            subprocess.run([cfg["ageExe"], "-d", "-i", cfg["identityFile"], "-o", str(archive_path), str(payload)],
                           stdout=log, stderr=log, check=True, timeout=600)
        archive_path.chmod(0o600)
        extraction = stage / "contents"
        extraction.mkdir(mode=0o700)
        metadata = inspect_archive(archive_path, extraction)
        if metadata["backupId"] != backup_id:
            raise RuntimeError("inner backup identifier mismatch")
        result = {"task": 475, "backupId": backup_id, "sourceAlias": "PILOT-472-CLEAN", "environment": "Homologation",
                  "keyAlias": "PILOT-475-RECOVERY", "ciphertextSha256": actual, "decryptionVerified": True,
                  "packageIntegrityVerified": True, "imageManifestVerified": True, "databaseDumpHashVerified": True,
                  "tableCount": metadata["tableCount"], "migrationCount": metadata["migrationCount"],
                  "imageFiles": metadata["imageFiles"], "imageBytes": metadata["imageBytes"],
                  "plaintextArchiveSha256": digest(archive_path), "windowsProfileOrDpapiUsed": False,
                  "verifiedAtUtc": dt.datetime.now(dt.timezone.utc).isoformat(), "fullDatabaseRestorePassed": False}
        verification_path = folder / "verification.json"
        if verification_path.exists():
            private(verification_path)
            old = json.loads(verification_path.read_text())
            if old.get("ciphertextSha256") == actual and old.get("fullDatabaseRestorePassed"):
                result["fullDatabaseRestorePassed"] = True
        pending = folder / ".verification.pending"
        if pending.exists() or pending.is_symlink():
            raise RuntimeError("pending verification needs operator review")
        with pending.open("x", encoding="utf-8") as output:
            json.dump(result, output, sort_keys=True)
            output.flush()
            os.fsync(output.fileno())
        pending.chmod(0o600)
        os.replace(pending, verification_path)
        if retain_for_restore:
            retained = scratch / backup_id
            if retained.exists() or retained.is_symlink():
                private(retained, True)
                private(retained / "bundle.tar")
                if digest(retained / "bundle.tar") != result["plaintextArchiveSha256"]:
                    raise RuntimeError("existing restore bundle mismatch")
            else:
                retained.mkdir(mode=0o700)
                shutil.copyfile(archive_path, retained / "bundle.tar")
                (retained / "bundle.tar").chmod(0o600)
                shutil.copytree(extraction, retained / "contents")
                for path in (retained / "contents").rglob("*"):
                    path.chmod(0o700 if path.is_dir() else 0o600)
                (retained / "contents").chmod(0o700)
                info = {"backupId": backup_id, "ciphertextSha256": actual,
                        "plaintextArchiveSha256": result["plaintextArchiveSha256"]}
                (retained / "restore-info.json").write_text(json.dumps(info))
                (retained / "restore-info.json").chmod(0o600)
            result["restoreBundleRetained"] = True
            result["restoreAlias"] = "DEVOPS-RESTORE-475"
        if stream_bundle:
            with archive_path.open("rb") as stream:
                shutil.copyfileobj(stream, sys.stdout.buffer, length=1024**2)
            sys.stdout.buffer.flush()
            return None
        return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--backup-id", required=True)
    parser.add_argument("--sha256", required=True)
    parser.add_argument("--stream-bundle", action="store_true")
    parser.add_argument("--retain-for-restore", action="store_true")
    args = parser.parse_args()
    os.umask(0o077)
    lock = None
    try:
        _, root, _ = configuration()
        lock = os.open(root / ".receive.lock", os.O_CREAT | os.O_RDWR | os.O_NOFOLLOW, 0o600)
        lock_info = os.fstat(lock)
        if lock_info.st_uid != os.getuid() or stat.S_IMODE(lock_info.st_mode) != 0o600:
            raise RuntimeError("unsafe verifier lock")
        fcntl.flock(lock, fcntl.LOCK_SH)
        result = verify(args.backup_id, args.sha256, args.stream_bundle, args.retain_for_restore)
        if result is not None:
            print(json.dumps(result, sort_keys=True))
    except Exception as error:
        if not args.stream_bundle:
            print(json.dumps({"task": 475, "backupId": args.backup_id, "decryptionVerified": False,
                              "packageIntegrityVerified": False, "errorType": type(error).__name__,
                              "details": "Private operator review required."}))
        return 1
    finally:
        if lock is not None:
            os.close(lock)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
