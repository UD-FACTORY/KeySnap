import http.server
import socketserver
import threading
import subprocess
import time
import os

PORT = 8999
DIRECTORY = r"d:\Code\QuickReplace\docs"

class Handler(http.server.SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=DIRECTORY, **kwargs)

socketserver.TCPServer.allow_reuse_address = True
httpd = socketserver.TCPServer(("", PORT), Handler)
thread = threading.Thread(target=httpd.serve_forever)
thread.daemon = True
thread.start()

print("Server started on port", PORT)
time.sleep(1)

out_pdf = r"d:\Code\QuickReplace\docs\KeySnap_사용설명서.pdf"
if os.path.exists(out_pdf):
    os.remove(out_pdf)

browser_path = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
if not os.path.exists(browser_path):
    browser_path = r"C:\Program Files\Google\Chrome\Application\chrome.exe"

import tempfile

temp_profile = tempfile.mkdtemp()

cmd = [
    browser_path,
    "--headless",
    "--disable-gpu",
    "--no-first-run",
    "--no-default-browser-check",
    f"--user-data-dir={temp_profile}",
    "--no-pdf-header-footer",
    f"--print-to-pdf={out_pdf}",
    f"http://localhost:{PORT}/manual.html"
]

print("Running command:", " ".join(cmd))
res = subprocess.run(cmd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=20)
print("Return code:", res.returncode)

httpd.shutdown()
time.sleep(1)

if os.path.exists(out_pdf):
    print("Success! PDF created with size:", os.path.getsize(out_pdf))
else:
    print("Failed to create PDF.")
