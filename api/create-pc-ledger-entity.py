"""Create the PC Ledger entity in INFRA-CAP.

The reference workbook (IT FORM SG031, sheet "Ledger") has 16 columns: No plus 15 data
columns, all 15 data columns carrying the light-yellow FFFFFFCC fill that means "the IT rep
maintains this". That maps 1:1 onto one entity field each, so the export can reproduce the
sheet column for column.

Field order matters: it is the order the generic engine returns, and the export uses an
explicit column list anyway, but keeping it aligned with the sheet makes the two easy to
compare. `nomor` is required+unique on purpose - LogbookNumberService keys off a field with
that exact name (NO_FIELD = "nomor") to fill the sequential No automatically.
"""
import json
import sys
import urllib.request
import urllib.error

API = "http://localhost:5099"

with open("appsettings.json", encoding="utf-8-sig") as fh:
    cfg = json.load(fh)
USER = cfg.get("Seed", {}).get("AdminUsername", "admin")
PASS = cfg.get("Seed", {}).get("AdminPassword")


def call(method, path, body=None, token=None):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(API + path, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req, timeout=60) as resp:
            raw = resp.read().decode()
            return resp.status, (json.loads(raw) if raw else None)
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode()[:400]


def field(name, label, ftype, order, required=False, unique=False, searchable=True,
          max_len=None, default=None):
    return {
        "name": name,
        "label": label,
        "type": ftype,
        "isRequired": required,
        "isUnique": unique,
        "isSearchable": searchable,
        "isVisible": True,
        "maxLength": max_len,
        "defaultValue": default,
        "optionsJson": None,
        "lookupEntityId": None,
        "lookupDisplayField": None,
        # SortOrder is not part of CreateFieldRequest - the server assigns it from array order.
    }


status, token_payload = call("POST", "/api/auth/login", {"username": USER, "password": PASS})
if status != 200:
    print("LOGIN FAILED", status, token_payload)
    sys.exit(1)
TOKEN = token_payload["token"]

FIELDS = [
    field("nomor", "No", "Text", 0, required=True, unique=True, max_len=30),
    field("staff_name", "Staff Name", "Text", 1, max_len=200),
    field("email", "Email Address", "Text", 2, max_len=200),
    field("gid", "GID", "Text", 3, max_len=50),
    field("japan_hostname", "JAPAN Hostname", "Text", 4, max_len=100),
    field("computer_model", "Computer Model", "Text", 5, max_len=100),
    field("computer_sn", "Computer S/N", "Text", 6, max_len=100),
    field("tanggal", "Date", "Date", 7),
    field("chassis", "Computer Chassis", "Text", 8, max_len=50),
    field("manufacturer", "Computer Manufacturer", "Text", 9, max_len=100),
    field("os_name", "Computer O/S Name", "Text", 10, max_len=100),
    field("os_arch", "Computer O/S Architecture", "Text", 11, max_len=50),
    field("lokasi", "Location", "Text", 12, max_len=200),
    field("remark2", "Remark2", "Text", 13, max_len=200),
    field("remark3", "Remark3", "Text", 14, max_len=200),
    field("departemen", "Department", "Text", 15, max_len=100, default="Capacitor"),
]

body = {
    "name": "PC Ledger",
    "slug": "pc_ledger",
    "description": "IT FORM SG031 Department PC Ledger - replacement for the manual Excel sheet.",
    "kind": "Transaction",
    "displayField": "nomor",
    "sortOrder": 30,
    "fields": FIELDS,
}

status, payload = call("POST", "/api/entities", body, TOKEN)
print("CREATE:", status, json.dumps(payload)[:300] if not isinstance(payload, dict) else
      str({k: payload.get(k) for k in ("id", "name", "slug", "kind", "recordCount")}))

status, entities = call("GET", "/api/entities?all=true", None, TOKEN)
if isinstance(entities, list):
    for e in entities:
        print(" -", e.get("id"), e.get("slug"), "fields:", len(e.get("fields") or []),
              "records:", e.get("recordCount"))
