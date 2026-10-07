"""Create the demo accounts the manuals sign in as. Run once after a fresh start (reset_env.ps1 does this)."""
import sys
from lib import api, api_token, PASSWORD

PERSONAS = [  # email, first, last, role, chapter
    ("pat.packer@example.org",   "Pat",   "Packer",  "Volunteer",   1),
    ("penny.prayer@example.org", "Penny", "Prayer",  "Volunteer",   1),
    ("bea.board@example.org",    "Bea",   "Board",   "Board",       None),
    ("nora.newhire@example.org", "Nora",  "Newhire", "ChapterStaff", 1),
    ("sam.staff@example.org",    "Sam",   "Staffer", "ChapterStaff", 1),
]

def main():
    tok = api_token("chris.kremer")
    for email, f, l, role, ch in PERSONAS:
        r = api("POST", "/api/v1/auth/register", tok, json={"email": email, "password": PASSWORD, "firstName": f, "lastName": l, "role": role, "chapterId": ch})
        print(email, r.status_code, "" if r.ok else r.text[:200])

if __name__ == "__main__":
    main()
