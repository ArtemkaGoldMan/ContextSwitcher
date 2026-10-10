#!/bin/bash
# Creates the self-signed certificate every release of ContextSwitcher is signed with. Run it ONCE.
#
#   ./scripts/create-signing-identity.sh [dir]      (default: ~/.contextswitcher-signing)
#
# Why a certificate at all, when there is no paid Apple Developer ID: macOS remembers the
# permissions it grants (controlling other apps) against the app's signature. An ad-hoc signature is
# a hash of that one build, so every update looked like a new app and every permission was asked
# for again. Signed with the same certificate, each build is "the same app" to macOS, and the app's
# updater can check that a download was signed by it before installing it.
#
# It does not get past Gatekeeper - only notarisation does - so the quarantine step on first install
# stays.
#
# KEEP THE RESULT SAFE AND BACKED UP. A release signed with a different certificate cannot update an
# installed copy: the updater refuses it, and macOS asks for every permission again.
set -euo pipefail

dir="${1:-$HOME/.contextswitcher-signing}"
name="ContextSwitcher Release Signing"
p12="$dir/signing.p12"
password_file="$dir/signing-password.txt"

if [ -e "$p12" ]; then
  echo "$p12 already exists - refusing to replace the signing identity." >&2
  echo "Replacing it breaks updates for everyone who has the app installed." >&2
  exit 1
fi

mkdir -p "$dir"
chmod 700 "$dir"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

cat > "$work/cert.cnf" <<CNF
[req]
distinguished_name = dn
prompt = no
x509_extensions = ext
[dn]
CN = $name
[ext]
basicConstraints = critical,CA:false
keyUsage = critical,digitalSignature
extendedKeyUsage = critical,codeSigning
subjectKeyIdentifier = hash
CNF

# macOS's own LibreSSL on purpose: its PKCS#12 defaults are what `security import` reads. OpenSSL 3
# from Homebrew writes AES-encrypted files that the keychain rejects.
/usr/bin/openssl req -x509 -newkey rsa:3072 -nodes -days 7300 \
  -keyout "$work/key.pem" -out "$work/cert.pem" -config "$work/cert.cnf" 2>/dev/null

password="$(/usr/bin/openssl rand -base64 24)"
/usr/bin/openssl pkcs12 -export -name "$name" \
  -inkey "$work/key.pem" -in "$work/cert.pem" \
  -out "$p12" -passout "pass:$password"
printf '%s' "$password" > "$password_file"
chmod 600 "$p12" "$password_file"

fingerprint="$(/usr/bin/openssl x509 -in "$work/cert.pem" -noout -fingerprint -sha1 | sed 's/.*=//; s/://g' | tr 'A-F' 'a-f')"

cat <<DONE
Created the signing identity in $dir:
  signing.p12            certificate and private key
  signing-password.txt   the password for signing.p12

Every build signed with it has this designated requirement:
  identifier "com.artem.contextswitcher" and certificate leaf = H"$fingerprint"

Next:
  1. Back both files up somewhere safe (a password manager will do).
  2. Give them to the release workflow as repository secrets:
       base64 -i "$p12" | gh secret set CS_SIGNING_P12_BASE64
       gh secret set CS_SIGNING_PASSWORD < "$password_file"
  3. ./scripts/build-app.sh picks them up from $dir on this Mac by itself.
DONE
