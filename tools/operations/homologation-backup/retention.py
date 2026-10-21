"""Conservative 7/4/12 retention, gated by daily verification and a full restore."""
import argparse
import datetime as dt
import fcntl
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import stat
from zoneinfo import ZoneInfo

ID = re.compile(r"gensw-\d{8}T\d{6}Z-[0-9a-f]{8}\Z")
ROOT = Path.home().resolve() / ".local/share/gensw-backup-475"
STATE = "retention-state.json"
SP = ZoneInfo("America/Sao_Paulo")
ALLOWED = {"payload.age", "receipt.json", "verification.json"}


def private(path, directory=False):
    info = path.lstat()
    if info.st_uid != os.getuid() or path.is_symlink() or stat.S_IMODE(info.st_mode) != (0o700 if directory else 0o600):
        raise RuntimeError("unsafe retention path")
    if (directory and not stat.S_ISDIR(info.st_mode)) or (not directory and not stat.S_ISREG(info.st_mode)):
        raise RuntimeError("unexpected retention path type")


def atomic_json(path, data):
    pending = path.with_name(path.name + ".pending")
    if pending.exists() or pending.is_symlink():
        raise RuntimeError("pending state requires review")
    with pending.open("x", encoding="utf-8") as output:
        json.dump(data, output, sort_keys=True)
        output.flush()
        os.fsync(output.fileno())
    pending.chmod(0o600)
    os.replace(pending, path)


def initialize(root):
    private(root, True)
    path = root / STATE
    if path.exists():
        private(path)
        return json.loads(path.read_text())
    state = {"task": 475, "sourceAlias": "PILOT-472-CLEAN", "environment": "Homologation",
             "daily": 7, "weekly": 4, "monthly": 12, "timezone": "America/Sao_Paulo",
             "protectedExistingSets": sorted(p.name for p in root.iterdir() if ID.fullmatch(p.name)),
             "fullRestoreGate": False, "restoreProtectedId": None,
             "initializedAtUtc": dt.datetime.now(dt.timezone.utc).isoformat()}
    atomic_json(path, state)
    return state


def verified_set(root, backup_id):
    if not ID.fullmatch(backup_id):
        raise RuntimeError("invalid protected identifier")
    folder = root / backup_id
    private(folder, True)
    for name in ALLOWED:
        private(folder / name)
    if {p.name for p in folder.iterdir()} != ALLOWED:
        raise RuntimeError("unexpected set members")
    receipt = json.loads((folder / "receipt.json").read_text())
    verification = json.loads((folder / "verification.json").read_text())
    for record in (receipt, verification):
        if (record.get("task") != 475 or record.get("backupId") != backup_id or
                record.get("sourceAlias") != "PILOT-472-CLEAN" or record.get("environment") != "Homologation" or
                record.get("keyAlias") != "PILOT-475-RECOVERY"):
            raise RuntimeError("unexpected set identity")
    if (not verification.get("decryptionVerified") or not verification.get("packageIntegrityVerified") or
            receipt["ciphertextSha256"] != verification["ciphertextSha256"] or
            (folder / "payload.age").stat().st_size != receipt["bytes"]):
        raise RuntimeError("set is not daily-verified")
    stamp = dt.datetime.fromisoformat(receipt["receivedAtUtc"])
    if stamp.tzinfo is None:
        raise RuntimeError("timestamp must include timezone")
    return {"id": backup_id, "time": stamp.astimezone(SP), "receipt": receipt, "verification": verification}


def selected_ids(sets):
    selected = set()
    ordered = sorted(sets, key=lambda x: (x["time"], x["id"]), reverse=True)
    for limit, bucket in ((7, lambda d: d.date()), (4, lambda d: d.isocalendar()[:2]), (12, lambda d: (d.year, d.month))):
        buckets = set()
        for item in ordered:
            key = bucket(item["time"])
            if key not in buckets:
                if len(buckets) == limit:
                    break
                buckets.add(key)
                selected.add(item["id"])
    return selected


def plan(root, state, current, restored):
    private(root, True)
    if not current or not restored:
        raise RuntimeError("current and full-restore protected sets required")
    current_set, anchor = verified_set(root, current), verified_set(root, restored)
    if (not state.get("fullRestoreGate") or state.get("restoreProtectedId") != restored or
            not anchor["verification"].get("fullDatabaseRestorePassed")):
        raise RuntimeError("full restore gate not satisfied")
    protected = set(state["protectedExistingSets"]) | {current, restored}
    verified, unverified = [], []
    for path in root.iterdir():
        if not ID.fullmatch(path.name):
            continue
        try:
            verified.append(verified_set(root, path.name))
        except Exception:
            unverified.append(path.name)
    keep = selected_ids(verified) | protected | set(unverified)
    remove = sorted(item["id"] for item in verified if item["id"] not in keep)
    result = {"task": 475, "ok": True, "policy": {"daily": 7, "weekly": 4, "monthly": 12, "timezone": "America/Sao_Paulo"},
              "protect": current, "restoreProtect": restored, "keep": sorted(keep), "remove": remove,
              "unverifiedAlwaysPreserved": sorted(unverified), "preexistingAlwaysPreserved": state["protectedExistingSets"]}
    result["planSha256"] = hashlib.sha256(json.dumps(result, sort_keys=True).encode()).hexdigest()
    return result


def record_restore(root, state, backup_id, expected_sha):
    value = verified_set(root, backup_id)
    if value["receipt"]["ciphertextSha256"] != expected_sha.lower():
        raise RuntimeError("restore ciphertext mismatch")
    proof = value["verification"]
    proof["fullDatabaseRestorePassed"] = True
    proof["fullRestoreRecordedAtUtc"] = dt.datetime.now(dt.timezone.utc).isoformat()
    proof["fullRestoreEvidence"] = "Coordinator recorded isolated database/image/API restore PASS under task 475"
    atomic_json(root / backup_id / "verification.json", proof)
    state["fullRestoreGate"] = True
    state["restoreProtectedId"] = backup_id
    atomic_json(root / STATE, state)
    return {"task": 475, "ok": True, "restoreProtectedId": backup_id, "deleted": [], "fullRestoreGate": True}


def apply(root, result):
    # Validate every removal before removing any. All other namespaces/sets stay untouched.
    targets = []
    resolved_root = root.resolve(strict=True)
    for backup_id in result["remove"]:
        value = verified_set(root, backup_id)
        target = root / backup_id
        if target.resolve(strict=True).parent != resolved_root or backup_id in (result["protect"], result["restoreProtect"]):
            raise RuntimeError("removal outside approved scope")
        payload = target / "payload.age"
        digest = hashlib.sha256()
        with payload.open("rb") as stream:
            for block in iter(lambda: stream.read(1024**2), b""):
                digest.update(block)
        if digest.hexdigest() != value["receipt"]["ciphertextSha256"]:
            raise RuntimeError("removal candidate integrity mismatch")
        targets.append(target)
    for target in targets:
        shutil.rmtree(target)
    return {"task": 475, "ok": True, "deleted": result["remove"], "deletedCount": len(targets),
            "keepCount": len(result["keep"]), "planSha256": result["planSha256"]}


def main():
    parser = argparse.ArgumentParser()
    actions = parser.add_mutually_exclusive_group(required=True)
    for flag in ("init", "plan", "apply", "record-restore"):
        actions.add_argument("--" + flag, action="store_true")
    parser.add_argument("--protect")
    parser.add_argument("--restore-protect")
    parser.add_argument("--sha256")
    args = parser.parse_args()
    os.umask(0o077)
    lock = None
    try:
        private(ROOT, True)
        lock = os.open(ROOT / ".receive.lock", os.O_CREAT | os.O_RDWR | os.O_NOFOLLOW, 0o600)
        lock_info = os.fstat(lock)
        if lock_info.st_uid != os.getuid() or stat.S_IMODE(lock_info.st_mode) != 0o600:
            raise RuntimeError("unsafe retention lock")
        fcntl.flock(lock, fcntl.LOCK_EX)
        state = initialize(ROOT)
        if args.init:
            result = {"task": 475, "ok": True, "policyInitialized": True,
                      "protectedExistingCount": len(state["protectedExistingSets"]), "fullRestoreGate": state["fullRestoreGate"], "deleted": []}
        elif args.record_restore:
            if not args.restore_protect or not args.sha256:
                raise RuntimeError("full restore identifier and digest required")
            result = record_restore(ROOT, state, args.restore_protect, args.sha256)
        else:
            result = plan(ROOT, state, args.protect, args.restore_protect)
            if args.apply:
                result = apply(ROOT, result)
        print(json.dumps(result, sort_keys=True))
    except Exception as error:
        print(json.dumps({"task": 475, "ok": False, "errorType": type(error).__name__,
                          "details": "Retention refused; review private state. No automatic override."}))
        return 1
    finally:
        if lock is not None:
            os.close(lock)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
