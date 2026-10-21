"""Synthetic receiver tests; they do not test encryption or restore."""
import hashlib
import importlib.util
import io
import os
from pathlib import Path
import tempfile
import unittest
from unittest import mock

spec = importlib.util.spec_from_file_location("receiver", Path(__file__).with_name("receiver.py"))
receiver = importlib.util.module_from_spec(spec)
spec.loader.exec_module(receiver)


def temporary_directory():
    # /tmp may be tmpfs. Exercise the production main-disk guard unchanged,
    # using only a fresh system temporary directory, never the backup namespace.
    device = Path("/").stat().st_dev
    for candidate in dict.fromkeys((tempfile.gettempdir(), "/var/tmp")):
        parent = Path(candidate)
        if parent.is_dir() and parent.stat().st_dev == device and os.access(parent, os.W_OK | os.X_OK):
            return tempfile.TemporaryDirectory(prefix="gensw-receiver-tests-", dir=parent)
    raise RuntimeError("Synthetic receiver tests require a writable system temporary directory on the main filesystem")


class ReceiverTests(unittest.TestCase):
    def setUp(self):
        # A test must never consult or create the operator's real namespace.
        guard = mock.patch.object(receiver, "destination", side_effect=AssertionError("real destination forbidden in tests"))
        guard.start()
        self.addCleanup(guard.stop)
        self.temp = temporary_directory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        os.chmod(self.root, 0o700)
        self.backup_id = "gensw-20261018T060000Z-12345678"
        # A fake envelope intentionally tests transport only, never decryption.
        self.fake = b"age-encryption.org/v1\nSYNTHETIC TRANSPORT TEST ONLY\n"

    def send(self, content=None, digest=None, backup_id=None, expected_bytes=None):
        data = self.fake if content is None else content
        return receiver.receive(self.root, backup_id or self.backup_id,
                                digest or hashlib.sha256(data).hexdigest(),
                                len(data) if expected_bytes is None else expected_bytes, "age", io.BytesIO(data))

    def test_publish_has_private_permissions_and_no_crypto_claim(self):
        result = self.send()
        self.assertTrue(result["transportIntegrity"])
        self.assertFalse(result["decryptionVerified"])
        self.assertEqual((self.root / self.backup_id).stat().st_mode & 0o777, 0o700)
        self.assertEqual((self.root / self.backup_id / "payload.age").stat().st_mode & 0o777, 0o600)

    def test_no_overwrite(self):
        self.send()
        with self.assertRaisesRegex(receiver.Rejected, "set_already_exists"):
            self.send()

    def test_wrong_hash_removes_partial_only(self):
        with self.assertRaisesRegex(receiver.Rejected, "digest_or_length_mismatch"):
            self.send(digest="0" * 64)
        self.assertFalse((self.root / self.backup_id).exists())
        self.assertFalse(list(self.root.glob(".incoming-*")))

    def test_short_stream_rejected_and_partial_removed(self):
        with self.assertRaisesRegex(receiver.Rejected, "digest_or_length_mismatch"):
            self.send(expected_bytes=len(self.fake) + 1)
        self.assertFalse((self.root / self.backup_id).exists())
        self.assertFalse(list(self.root.glob(".incoming-*")))

    def test_extra_stream_bytes_rejected_and_partial_removed(self):
        with self.assertRaisesRegex(receiver.Rejected, "length_mismatch"):
            self.send(expected_bytes=len(self.fake) - 1)
        self.assertFalse((self.root / self.backup_id).exists())
        self.assertFalse(list(self.root.glob(".incoming-*")))

    def test_plaintext_rejected(self):
        with self.assertRaisesRegex(receiver.Rejected, "unrecognized_encrypted_envelope"):
            self.send(content=b"PGDMP-PLAINTEXT-SYNTHETIC")

    def test_traversal_rejected(self):
        with self.assertRaisesRegex(receiver.Rejected, "invalid_identifier"):
            self.send(backup_id="../elsewhere")

    def test_symlink_target_not_followed(self):
        (self.root / self.backup_id).symlink_to(self.root, target_is_directory=True)
        with self.assertRaisesRegex(receiver.Rejected, "set_already_exists"):
            self.send()


if __name__ == "__main__":
    unittest.main()
