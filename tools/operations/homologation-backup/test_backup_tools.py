"""Synthetic age/archive/retention tests; no real database or application data."""
import datetime as dt
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import shutil
import subprocess
import tarfile
import tempfile
import unittest
from unittest import mock


def module(name, filename):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).with_name(filename))
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


verify = module("verifyset", "verify-set.py")
retention = module("retention475", "retention.py")
receiver = module("receiver475", "receiver.py")


def temporary_directory():
    # Keep the actual receiver's main-disk guard active when /tmp is tmpfs.
    # Neither candidate is an application or backup directory.
    device = Path("/").stat().st_dev
    for candidate in dict.fromkeys((tempfile.gettempdir(), "/var/tmp")):
        parent = Path(candidate)
        if parent.is_dir() and parent.stat().st_dev == device and os.access(parent, os.W_OK | os.X_OK):
            return tempfile.TemporaryDirectory(prefix="gensw-backup-tools-tests-", dir=parent)
    raise RuntimeError("Synthetic backup tests require a writable system temporary directory on the main filesystem")


class BackupTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        # CI selects the pinned binary through these variables; local Linux runs
        # can use PATH. An absent tool fails the suite instead of silently skipping.
        cls.age_exe = os.environ.get("GENSW_TEST_AGE_EXE") or shutil.which("age")
        cls.keygen_exe = os.environ.get("GENSW_TEST_AGE_KEYGEN_EXE") or shutil.which("age-keygen")
        if not cls.age_exe or not cls.keygen_exe:
            raise RuntimeError("Synthetic tests require age and age-keygen on PATH or GENSW_TEST_AGE_* variables")

    def setUp(self):
        previous_umask = os.umask(0o077)
        self.addCleanup(os.umask, previous_umask)
        self.temp = temporary_directory()
        self.addCleanup(self.temp.cleanup)
        self.base = Path(self.temp.name)
        self.root = self.base / "sets"
        self.scratch = self.base / "scratch"
        self.root.mkdir(mode=0o700)
        self.scratch.mkdir(mode=0o700)
        identity = self.base / "identity.txt"
        # X25519 fixture keys are disposable and never printed. The operational
        # homologation identity and its hybrid recipient are not read by CI.
        subprocess.run([self.keygen_exe, "-o", str(identity)], capture_output=True, check=True)
        recipient = subprocess.run([self.keygen_exe, "-y", str(identity)], capture_output=True, text=True, check=True)
        self.recipients = self.base / "recipients.txt"
        self.recipients.write_text(recipient.stdout, encoding="utf-8")
        identity.chmod(0o600)
        self.recipients.chmod(0o600)
        self.cfg = {"ageExe": self.age_exe, "identityFile": str(identity)}
        configuration = mock.patch.object(verify, "configuration", return_value=(self.cfg, self.root, self.scratch))
        configuration.start()
        self.addCleanup(configuration.stop)
        destination = mock.patch.object(receiver, "destination", side_effect=AssertionError("real destination forbidden in tests"))
        destination.start()
        self.addCleanup(destination.stop)
        self.backup_id = "gensw-20261018T030000Z-12345678"

    def make_archive(self, wrong_image_hash=False, traversal=False, link_type=None,
                     wrong_dump_hash=False, unmanifested_image=False):
        contents = self.base / "source"
        contents.mkdir(mode=0o700)
        images = contents / "images"
        images.mkdir(mode=0o700)
        image = b"SYNTHETIC IMAGE CONTENT FOR HASH TEST"
        dump = b"SYNTHETIC DATABASE PAYLOAD; NOT A POSTGRESQL BACKUP"
        (images / "opaque.png").write_bytes(image)
        if unmanifested_image:
            (images / "extra.png").write_bytes(b"UNMANIFESTED SYNTHETIC IMAGE")
        (contents / "database.dump").write_bytes(dump)
        manifest = {"opaque.png": {"bytes": len(image), "sha256": "0" * 64 if wrong_image_hash else hashlib.sha256(image).hexdigest()}}
        metadata = {"task": 475, "backupId": self.backup_id, "sourceAlias": "PILOT-472-CLEAN", "environment": "Homologation",
                    "tableCount": 2, "migrationCount": 1, "imageFiles": 1, "imageBytes": len(image),
                    "databaseDumpBytes": len(dump), "databaseDumpSha256": "0" * 64 if wrong_dump_hash else hashlib.sha256(dump).hexdigest(),
                    "allReferencedDerivativesPresentIncludingInactive": True}
        (contents / "manifest.json").write_text(json.dumps(manifest))
        (contents / "capture-metadata.json").write_text(json.dumps(metadata))
        (contents / "source-snapshot-private.json").write_text(json.dumps({"tables": {"one": {}, "two": {}}, "migrations": ["synthetic"]}))
        archive = self.base / "source.tar"
        with tarfile.open(archive, "w") as output:
            for path in contents.iterdir():
                output.add(path, arcname=path.name)
            if traversal:
                info = tarfile.TarInfo("../outside")
                info.size = 1
                output.addfile(info, io.BytesIO(b"x"))
            if link_type is not None:
                info = tarfile.TarInfo("images/shortcut.png")
                info.type = link_type
                info.linkname = "../database.dump"
                output.addfile(info)
        return archive

    def encrypt_and_receive(self, archive):
        encrypted = self.base / "payload.age"
        subprocess.run([self.cfg["ageExe"], "-R", str(self.recipients), "-o", str(encrypted), str(archive)],
                       capture_output=True, check=True)
        digest = verify.digest(encrypted)
        with encrypted.open("rb") as stream:
            receiver.receive(self.root, self.backup_id, digest, encrypted.stat().st_size, "age", stream)
        return digest

    def test_age_decrypt_and_integral_manifest(self):
        digest = self.encrypt_and_receive(self.make_archive())
        result = verify.verify(self.backup_id, digest, retain_for_restore=True)
        self.assertTrue(result["decryptionVerified"])
        self.assertTrue(result["packageIntegrityVerified"])
        self.assertFalse(result["fullDatabaseRestorePassed"])
        self.assertTrue((self.scratch / self.backup_id / "bundle.tar").exists())

    def test_wrong_image_hash_rejected(self):
        digest = self.encrypt_and_receive(self.make_archive(wrong_image_hash=True))
        with self.assertRaises(RuntimeError):
            verify.verify(self.backup_id, digest)
        self.assertFalse((self.root / self.backup_id / "verification.json").exists())

    def test_wrong_dump_hash_rejected(self):
        digest = self.encrypt_and_receive(self.make_archive(wrong_dump_hash=True))
        with self.assertRaises(RuntimeError):
            verify.verify(self.backup_id, digest)
        self.assertFalse((self.root / self.backup_id / "verification.json").exists())

    def test_unmanifested_image_rejected(self):
        digest = self.encrypt_and_receive(self.make_archive(unmanifested_image=True))
        with self.assertRaises(RuntimeError):
            verify.verify(self.backup_id, digest)
        self.assertFalse((self.root / self.backup_id / "verification.json").exists())

    def test_archive_traversal_rejected(self):
        digest = self.encrypt_and_receive(self.make_archive(traversal=True))
        with self.assertRaises(RuntimeError):
            verify.verify(self.backup_id, digest)
        self.assertFalse((self.scratch / "outside").exists())

    def test_archive_symlink_rejected(self):
        digest = self.encrypt_and_receive(self.make_archive(link_type=tarfile.SYMTYPE))
        with self.assertRaises(RuntimeError):
            verify.verify(self.backup_id, digest)
        self.assertFalse((self.root / self.backup_id / "verification.json").exists())

    def test_archive_hardlink_rejected(self):
        digest = self.encrypt_and_receive(self.make_archive(link_type=tarfile.LNKTYPE))
        with self.assertRaises(RuntimeError):
            verify.verify(self.backup_id, digest)
        self.assertFalse((self.root / self.backup_id / "verification.json").exists())

    def test_ciphertext_tampering_rejected(self):
        digest = self.encrypt_and_receive(self.make_archive())
        payload = self.root / self.backup_id / "payload.age"
        data = payload.read_bytes()
        payload.write_bytes(data[:-1] + bytes([data[-1] ^ 1]))
        with self.assertRaises(RuntimeError):
            verify.verify(self.backup_id, digest)

    def test_age_authentication_rejects_tampering_with_updated_transport_digest(self):
        self.encrypt_and_receive(self.make_archive())
        folder = self.root / self.backup_id
        payload = folder / "payload.age"
        data = payload.read_bytes()
        payload.write_bytes(data[:-1] + bytes([data[-1] ^ 1]))
        tampered_digest = verify.digest(payload)
        receipt_path = folder / "receipt.json"
        receipt = json.loads(receipt_path.read_text())
        receipt["ciphertextSha256"] = tampered_digest
        receipt_path.write_text(json.dumps(receipt))
        # Even matching transport metadata cannot authenticate modified ciphertext.
        with self.assertRaises(subprocess.CalledProcessError):
            verify.verify(self.backup_id, tampered_digest)
        self.assertFalse((folder / "verification.json").exists())

    def fake_verified(self, number, when):
        backup_id = when.strftime("gensw-%Y%m%dT%H%M%SZ-") + f"{number:08x}"
        folder = self.root / backup_id
        folder.mkdir(mode=0o700)
        data = b"synthetic retention transport fixture" + str(number).encode()
        digest = hashlib.sha256(data).hexdigest()
        receipt = {"task": 475, "backupId": backup_id, "sourceAlias": "PILOT-472-CLEAN", "environment": "Homologation",
                   "keyAlias": "PILOT-475-RECOVERY", "ciphertextSha256": digest, "bytes": len(data), "receivedAtUtc": when.isoformat()}
        proof = dict(receipt, decryptionVerified=True, packageIntegrityVerified=True, fullDatabaseRestorePassed=False)
        (folder / "payload.age").write_bytes(data)
        (folder / "receipt.json").write_text(json.dumps(receipt))
        (folder / "verification.json").write_text(json.dumps(proof))
        for path in folder.iterdir():
            path.chmod(0o600)
        return backup_id, digest

    def test_retention_requires_full_restore(self):
        state = retention.initialize(self.root)
        current, _ = self.fake_verified(1, dt.datetime.now(dt.timezone.utc))
        with self.assertRaises(RuntimeError):
            retention.plan(self.root, state, current, current)

    def test_retention_preserves_anchor_current_preexisting_and_unverified(self):
        state = retention.initialize(self.root)
        first = dt.datetime(2025, 1, 1, 6, tzinfo=dt.timezone.utc)
        sets = [self.fake_verified(i + 1, first + dt.timedelta(days=10 * i)) for i in range(60)]
        anchor, anchor_hash = sets[1]
        current = sets[-1][0]
        state["protectedExistingSets"] = [sets[0][0]]
        retention.record_restore(self.root, state, anchor, anchor_hash)
        unverified = sets[2][0]
        (self.root / unverified / "verification.json").unlink()
        result = retention.plan(self.root, state, current, anchor)
        self.assertTrue(result["remove"])
        for protected in (anchor, current, sets[0][0], unverified):
            self.assertIn(protected, result["keep"])
        applied = retention.apply(self.root, result)
        self.assertGreater(applied["deletedCount"], 0)
        for protected in (anchor, current, sets[0][0], unverified):
            self.assertTrue((self.root / protected).exists())

    def test_retention_uses_sao_paulo_calendar_buckets(self):
        late_sunday_utc_monday = dt.datetime(2026, 1, 5, 2, 30, tzinfo=dt.timezone.utc).astimezone(retention.SP)
        self.assertEqual(late_sunday_utc_monday.date(), dt.date(2026, 1, 4))
        self.assertEqual(late_sunday_utc_monday.isocalendar()[:2], (2026, 1))

    def test_retention_removes_no_candidate_when_any_digest_is_invalid(self):
        state = retention.initialize(self.root)
        first = dt.datetime(2025, 1, 1, 6, tzinfo=dt.timezone.utc)
        sets = [self.fake_verified(i + 1, first + dt.timedelta(days=10 * i)) for i in range(60)]
        anchor, anchor_hash = sets[0]
        current = sets[-1][0]
        retention.record_restore(self.root, state, anchor, anchor_hash)
        result = retention.plan(self.root, state, current, anchor)
        self.assertGreater(len(result["remove"]), 1)
        last_candidate = self.root / result["remove"][-1] / "payload.age"
        # Preserve byte count so removal reaches its final hash guard.
        data = last_candidate.read_bytes()
        last_candidate.write_bytes(data[:-1] + bytes([data[-1] ^ 1]))
        with self.assertRaisesRegex(RuntimeError, "integrity mismatch"):
            retention.apply(self.root, result)
        for backup_id in result["remove"]:
            self.assertTrue((self.root / backup_id).is_dir())


if __name__ == "__main__":
    unittest.main()
