#!/usr/bin/env python3
"""Receive an already encrypted GenSW set. No plaintext or decryption keys accepted."""
import argparse
import datetime as dt
import fcntl
import hashlib
import hmac
import json
import os
from pathlib import Path
import re
import shutil
import stat
import sys
import uuid

ID = re.compile(r"gensw-\d{8}T\d{6}Z-[0-9a-f]{8}\Z")
SHA = re.compile(r"[0-9a-fA-F]{64}\Z")
HEADERS = {"age": b"age-encryption.org/v1\n", "gpg-armored": b"-----BEGIN PGP MESSAGE-----"}
MAX_BYTES = 100 * 1024**3
RESERVE_BYTES = 2 * 1024**3


class Rejected(Exception):
    pass


def destination():
    home = Path.home().resolve()
    root = home / ".local" / "share" / "gensw-backup-475"
    for part in (home / ".local", home / ".local" / "share", root):
        if part.is_symlink():
            raise Rejected("unsafe_destination")
    return root


def check_root(root):
    info = root.lstat()
    if not stat.S_ISDIR(info.st_mode) or info.st_uid != os.getuid():
        raise Rejected("destination_owner")
    if stat.S_IMODE(info.st_mode) != 0o700:
        raise Rejected("destination_permissions")
    if info.st_dev != Path("/").stat().st_dev:
        raise Rejected("destination_not_main_disk")


def initialize(root):
    if not root.exists():
        root.mkdir(parents=True, mode=0o700)
    check_root(root)
    return {"destination": "DEVOPS-GENSW-MAIN", "initialized": True, "mode": "0700"}


def status(root):
    check_root(root)
    sets = [p for p in root.iterdir() if ID.fullmatch(p.name) and p.is_dir() and not p.is_symlink()]
    return {"destination": "DEVOPS-GENSW-MAIN", "mode": "0700", "setCount": len(sets),
            "freeBytes": shutil.disk_usage(root).free, "decryptionVerified": False,
            "envelopeCheckIsNotCryptographicValidation": True}


def receive(root, backup_id, expected_sha256, expected_bytes, envelope, stream):
    if not ID.fullmatch(backup_id) or not SHA.fullmatch(expected_sha256):
        raise Rejected("invalid_identifier_or_digest")
    if envelope not in HEADERS or not 0 < expected_bytes <= MAX_BYTES:
        raise Rejected("invalid_format_or_size")
    check_root(root)
    if shutil.disk_usage(root).free - expected_bytes < RESERVE_BYTES:
        raise Rejected("insufficient_space")
    lock_fd = os.open(root / ".receive.lock", os.O_CREAT | os.O_RDWR | os.O_NOFOLLOW, 0o600)
    stage = None
    try:
        lock_info = os.fstat(lock_fd)
        if lock_info.st_uid != os.getuid() or stat.S_IMODE(lock_info.st_mode) != 0o600:
            raise Rejected("lock_permissions")
        try:
            fcntl.flock(lock_fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            raise Rejected("receiver_busy") from None
        final = root / backup_id
        if os.path.lexists(final):
            raise Rejected("set_already_exists")
        stage = root / (".incoming-" + backup_id + "-" + uuid.uuid4().hex)
        stage.mkdir(mode=0o700)
        extension = "age" if envelope == "age" else "asc"
        payload = stage / ("payload." + extension)
        digest, count, first = hashlib.sha256(), 0, b""
        with open(payload, "xb", buffering=0) as output:
            os.chmod(payload, 0o600)
            while True:
                chunk = stream.read(min(1024**2, expected_bytes - count + 1))
                if not chunk:
                    break
                count += len(chunk)
                if count > expected_bytes:
                    raise Rejected("length_mismatch")
                if len(first) < 128:
                    first = (first + chunk)[:128]
                digest.update(chunk)
                output.write(chunk)
            os.fsync(output.fileno())
        if count != expected_bytes or not hmac.compare_digest(digest.hexdigest(), expected_sha256.lower()):
            raise Rejected("digest_or_length_mismatch")
        if not first.startswith(HEADERS[envelope]):
            raise Rejected("unrecognized_encrypted_envelope")
        receipt = {"task": 475, "sourceAlias": "PILOT-472-CLEAN", "environment": "Homologation", "keyAlias": "PILOT-475-RECOVERY", "backupId": backup_id, "destination": "DEVOPS-GENSW-MAIN", "bytes": count,
                   "ciphertextSha256": digest.hexdigest(), "envelope": envelope,
                   "receivedAtUtc": dt.datetime.now(dt.timezone.utc).isoformat(),
                   "transportIntegrity": True, "decryptionVerified": False,
                   "envelopeCheckIsNotCryptographicValidation": True}
        with open(stage / "receipt.json", "x", encoding="utf-8") as output:
            os.chmod(stage / "receipt.json", 0o600)
            json.dump(receipt, output, sort_keys=True)
            output.flush()
            os.fsync(output.fileno())
        directory_fd = os.open(stage, os.O_RDONLY | os.O_DIRECTORY)
        try:
            os.fsync(directory_fd)
        finally:
            os.close(directory_fd)
        os.rename(stage, final)
        stage = None
        root_fd = os.open(root, os.O_RDONLY | os.O_DIRECTORY)
        try:
            os.fsync(root_fd)
        finally:
            os.close(root_fd)
        return receipt
    finally:
        if stage is not None and stage.parent == root and stage.name.startswith(".incoming-"):
            shutil.rmtree(stage)
        os.close(lock_fd)


def main():
    parser = argparse.ArgumentParser()
    action = parser.add_mutually_exclusive_group(required=True)
    action.add_argument("--init", action="store_true")
    action.add_argument("--status", action="store_true")
    action.add_argument("--receive", action="store_true")
    parser.add_argument("--backup-id")
    parser.add_argument("--sha256")
    parser.add_argument("--bytes", type=int)
    parser.add_argument("--format", choices=HEADERS)
    args = parser.parse_args()
    os.umask(0o077)
    try:
        root = destination()
        if args.init:
            result = initialize(root)
        elif args.status:
            result = status(root)
        else:
            if None in (args.backup_id, args.sha256, args.bytes, args.format):
                raise Rejected("missing_parameters")
            result = receive(root, args.backup_id, args.sha256, args.bytes, args.format, sys.stdin.buffer)
        print(json.dumps(result, sort_keys=True))
    except Rejected as exc:
        print(json.dumps({"ok": False, "reason": str(exc)}))
        return 2
    except Exception:
        print(json.dumps({"ok": False, "reason": "operation_failed_review_private_host"}))
        return 3
    return 0


if __name__ == "__main__":
    sys.exit(main())
