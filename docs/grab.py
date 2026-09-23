import time
import subprocess
import ctypes
from PIL import ImageGrab

user32 = ctypes.windll.user32

# Find window titled KeySnap
def find_window():
    found_hwnd = []
    def enum_windows_proc(hwnd, lParam):
        if user32.IsWindowVisible(hwnd):
            length = user32.GetWindowTextLengthW(hwnd)
            if length > 0:
                buff = ctypes.create_unicode_buffer(length + 1)
                user32.GetWindowTextW(hwnd, buff, length + 1)
                if "KeySnap" in buff.value:
                    found_hwnd.append((hwnd, buff.value))
        return True

    CMPFUNC = ctypes.WINFUNCTYPE(ctypes.c_bool, ctypes.c_void_p, ctypes.c_void_p)
    user32.EnumWindows(CMPFUNC(enum_windows_proc), 0)
    return found_hwnd

hwnds = find_window()
print("Found windows:", hwnds)

if not hwnds:
    print("Starting KeySnap...")
    subprocess.Popen([r"d:\Code\QuickReplace\dist\KeySnap.exe"])
    time.sleep(2)
    hwnds = find_window()
    print("Found windows after start:", hwnds)

if hwnds:
    hwnd = hwnds[0][0]
    user32.ShowWindow(hwnd, 9) # SW_RESTORE
    user32.SetForegroundWindow(hwnd)
    time.sleep(1)

    class RECT(ctypes.Structure):
        _fields_ = [("left", ctypes.c_long), ("top", ctypes.c_long),
                    ("right", ctypes.c_long), ("bottom", ctypes.c_long)]
    r = RECT()
    user32.GetWindowRect(hwnd, ctypes.byref(r))
    print(f"Window bounds: {r.left}, {r.top}, {r.right}, {r.bottom}")

    if r.right > r.left and r.bottom > r.top:
        img = ImageGrab.grab(bbox=(r.left, r.top, r.right, r.bottom))
        img.save(r"d:\Code\QuickReplace\docs\images\screenshot_main.png")
        print("Successfully saved screenshot_main.png")
