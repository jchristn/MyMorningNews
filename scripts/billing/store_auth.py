"""Short-lived store API authentication. Never prints credentials. (c) 2026 Lattice AI LLC."""
import base64
import json
import time
from pathlib import Path
import requests
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import ec, padding, utils


def _base64(value):
    return base64.urlsafe_b64encode(value).decode().rstrip('=')


def signed_jwt(header, payload, pem, elliptic=False):
    value = _base64(json.dumps(header, separators=(',', ':')).encode()) + '.' + _base64(json.dumps(payload, separators=(',', ':')).encode())
    key = serialization.load_pem_private_key(pem, password=None)
    if elliptic:
        r, s = utils.decode_dss_signature(key.sign(value.encode(), ec.ECDSA(hashes.SHA256())))
        signature = r.to_bytes(32, 'big') + s.to_bytes(32, 'big')
    else:
        signature = key.sign(value.encode(), padding.PKCS1v15(), hashes.SHA256())
    return value + '.' + _base64(signature)


def apple_session(issuer, key_id, key_file):
    now = int(time.time())
    token = signed_jwt({'alg': 'ES256', 'kid': key_id, 'typ': 'JWT'},
                       {'iss': issuer, 'iat': now, 'exp': now + 1100, 'aud': 'appstoreconnect-v1'}, Path(key_file).expanduser().read_bytes(), True)
    session = requests.Session()
    session.headers['Authorization'] = 'Bearer ' + token
    return session


def google_session(key_file, scope='https://www.googleapis.com/auth/androidpublisher'):
    credentials = json.loads(Path(key_file).expanduser().read_text())
    now = int(time.time())
    assertion = signed_jwt({'alg': 'RS256', 'kid': credentials['private_key_id'], 'typ': 'JWT'},
                           {'iss': credentials['client_email'], 'iat': now, 'exp': now + 3500, 'scope': scope, 'aud': 'https://oauth2.googleapis.com/token'}, credentials['private_key'].encode())
    response = requests.post('https://oauth2.googleapis.com/token', data={'grant_type': 'urn:ietf:params:oauth:grant-type:jwt-bearer', 'assertion': assertion}, timeout=30)
    if not response.ok:
        raise RuntimeError('Google authentication failed: HTTP ' + str(response.status_code))
    session = requests.Session()
    session.headers['Authorization'] = 'Bearer ' + response.json()['access_token']
    return session
