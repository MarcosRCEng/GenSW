"""Verify official age release assets on DevOPS; no application data or keys."""
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tarfile
import urllib.request

VERSION = "v1.3.2"
EXPECTED = {
    "age-v1.3.2-linux-amd64.tar.gz": "cbe24006683f8eb669266162894b9a522a1af52f2665fbc63a4bb032ed26ac10",
    "age-v1.3.2-linux-amd64.tar.gz.proof": "98402fa59b4e433426156b4a7e74a32e44902e0434c61158f7de18ffc84f1f97",
    "age-v1.3.2-windows-amd64.zip": "f48d8f8f9ebe903ab5027ed067652f2cc1db94bc206976430133b905dcd8e8c7",
    "age-v1.3.2-windows-amd64.zip.proof": "00ae35a24a47b9bace52ec62f06d402f38f6ecba4ca099d060a137e82ef0bc1d",
}


def main():
    os.umask(0o077)
    root = Path.home() / ".local/share/gensw-backup-tools"
    downloads = root / "downloads"
    for name, expected in EXPECTED.items():
        if hashlib.sha256((downloads / name).read_bytes()).hexdigest() != expected:
            raise RuntimeError("release digest mismatch")
    document = urllib.request.urlopen(
        "https://raw.githubusercontent.com/FiloSottile/age/v1.3.2/SIGSUM.md", timeout=30
    ).read().decode()
    public_lines = [line for line in document.splitlines() if line.startswith("ssh-ed25519 ")]
    if len(public_lines) != 2:
        raise RuntimeError("unexpected official Sigsum signing keys")
    key_path = root / "age-sigsum-upstream.pub"
    key_path.write_text("\n".join(public_lines) + "\n")
    # Toolchain is private to this namespace; no apt/system PATH/service change.
    releases = json.load(urllib.request.urlopen("https://go.dev/dl/?mode=json", timeout=30))
    release = next(item for item in releases if item.get("stable"))
    asset = next(item for item in release["files"] if item["os"] == "linux" and item["arch"] == "amd64" and item["kind"] == "archive")
    sdk_archive = downloads / asset["filename"]
    if not sdk_archive.exists():
        urllib.request.urlretrieve("https://go.dev/dl/" + asset["filename"], sdk_archive)
    if hashlib.sha256(sdk_archive.read_bytes()).hexdigest() != asset["sha256"]:
        raise RuntimeError("official Go toolchain digest mismatch")
    sdk_root = root / "go-sdk"
    if not (sdk_root / "go/bin/go").exists():
        sdk_root.mkdir(mode=0o700)
        with tarfile.open(sdk_archive) as archive:
            archive.extractall(sdk_root, filter="data")
    go_exe = sdk_root / "go/bin/go"
    env = os.environ.copy()
    env.update({"GOBIN": str(root / "bin"), "GOPATH": str(root / "go-workspace"),
                "GOCACHE": str(root / "go-cache"), "GOTOOLCHAIN": "local"})
    (root / "bin").mkdir(mode=0o700, exist_ok=True)
    with (root / "sigsum-private-build.log").open("wb") as log:
        subprocess.run([str(go_exe), "install", "sigsum.org/sigsum-go/cmd/sigsum-verify@v0.13.1"],
                       env=env, stdout=log, stderr=log, check=True, timeout=300)
    results = {}
    for name in ("age-v1.3.2-linux-amd64.tar.gz", "age-v1.3.2-windows-amd64.zip"):
        with (downloads / name).open("rb") as data, (root / (name + ".verification.log")).open("wb") as log:
            subprocess.run([str(root / "bin/sigsum-verify"), "-k", str(key_path), "-P", "sigsum-generic-2025-1",
                            str(downloads / (name + ".proof"))], stdin=data, stdout=log, stderr=log, check=True, timeout=60)
        results[name] = True
    age_root = root / ("age-" + VERSION)
    if not (age_root / "age/age").exists():
        age_root.mkdir(mode=0o700)
        with tarfile.open(downloads / "age-v1.3.2-linux-amd64.tar.gz") as archive:
            archive.extractall(age_root, filter="data")
    age_exe = age_root / "age/age"
    age_exe.chmod(0o700)
    version = subprocess.check_output([str(age_exe), "--version"], text=True).strip()
    report = {"task": 475, "ageVersion": version, "officialAssetDigestsMatched": True,
              "sigsumProofsVerified": results, "sigsumVerifier": "v0.13.1",
              "goToolchain": release["version"], "goToolchainOfficialDigestMatched": True,
              "systemPackagesOrServicesChanged": False}
    (root / "age-verification.json").write_text(json.dumps(report, indent=2))
    print(json.dumps(report))


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print(json.dumps({"task": 475, "verified": False, "errorType": type(error).__name__,
                          "details": "Review private namespace logs; no application data involved."}))
        raise SystemExit(1)
