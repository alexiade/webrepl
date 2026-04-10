#!/usr/bin/env python
"""Remote operations for MicroPython WebREPL"""
from __future__ import print_function
import sys
import os
import struct
import time
import select

WEBREPL_REQ_S = "<2sBBQLH64s"
WEBREPL_PUT_FILE = 1
WEBREPL_GET_FILE = 2
WEBREPL_GET_VER = 3
WEBREPL_FRAME_TXT = 0x81
WEBREPL_FRAME_BIN = 0x82

SANDBOX = ""
DEBUG = 0


def debugmsg(msg):
    if DEBUG:
        print(msg)


def read_resp(ws):
    """Read response from WebREPL"""
    data = ws.read(4)
    sig, code = struct.unpack("<2sH", data)
    assert sig == b"WB"
    return code


def send_req(ws, op, sz=0, fname=b""):
    """Send request to WebREPL"""
    rec = struct.pack(WEBREPL_REQ_S, b"WA", op, 0, 0, sz, len(fname), fname)
    debugmsg("%r %d" % (rec, len(rec)))
    ws.write(rec)


def put_file(ws, local_file, remote_file):
    """Upload file to remote"""
    sz = os.stat(local_file)[6]
    dest_fname = (SANDBOX + remote_file).encode("utf-8")
    rec = struct.pack(WEBREPL_REQ_S, b"WA", WEBREPL_PUT_FILE, 0, 0, sz, len(dest_fname), dest_fname)
    debugmsg("%r %d" % (rec, len(rec)))
    ws.write(rec[:10])
    ws.write(rec[10:])
    assert read_resp(ws) == 0
    cnt = 0
    with open(local_file, "rb") as f:
        while True:
            sys.stdout.write("Sent %d of %d bytes\r" % (cnt, sz))
            sys.stdout.flush()
            buf = f.read(1024)
            if not buf:
                break
            ws.write(buf)
            cnt += len(buf)
    print()
    assert read_resp(ws) == 0


def get_file(ws, local_file, remote_file):
    """Download file from remote"""
    src_fname = (SANDBOX + remote_file).encode("utf-8")
    rec = struct.pack(WEBREPL_REQ_S, b"WA", WEBREPL_GET_FILE, 0, 0, 0, len(src_fname), src_fname)
    debugmsg("%r %d" % (rec, len(rec)))
    ws.write(rec)
    assert read_resp(ws) == 0
    with open(local_file, "wb") as f:
        cnt = 0
        while True:
            ws.write(b"\0")
            (sz,) = struct.unpack("<H", ws.read(2))
            if sz == 0:
                break
            while sz:
                buf = ws.read(sz)
                if not buf:
                    raise OSError()
                cnt += len(buf)
                f.write(buf)
                sz -= len(buf)
                sys.stdout.write("Received %d bytes\r" % cnt)
                sys.stdout.flush()
    print()
    assert read_resp(ws) == 0


def clear_buffer(ws, timeout=1.0):
    """Aggressively drain all data from websocket buffer and socket"""
    ws.buf = b""
    drained = b""
    start_time = time.time()

    try:
        ws.s.setblocking(False)
        while time.time() - start_time < timeout:
            try:
                readable, _, _ = select.select([ws.s], [], [], 0.01)
                if readable:
                    hdr = ws.s.recv(2)
                    if len(hdr) < 2:
                        break
                    fl, sz = struct.unpack(">BB", hdr)
                    if sz == 126:
                        hdr2 = ws.s.recv(2)
                        if len(hdr2) < 2:
                            break
                        (sz,) = struct.unpack(">H", hdr2)

                    while sz > 0:
                        chunk = ws.s.recv(min(sz, 4096))
                        if not chunk:
                            break
                        drained += chunk
                        sz -= len(chunk)
                else:
                    break
            except BlockingIOError:
                break
            except Exception as e:
                debugmsg(f"Error draining buffer: {e}")
                break
    finally:
        ws.s.setblocking(True)

    if DEBUG and drained:
        debugmsg(f"Drained {len(drained)} bytes from buffer: {drained[:100]}...")


def interrupt_running_code(ws):
    """Send Ctrl+C to interrupt any running code on the MicroPython board"""
    print("Interrupting any running code...")
    clear_buffer(ws, timeout=0.5)

    for _ in range(3):
        ws.write(b"\x03", frame=WEBREPL_FRAME_TXT)
        time.sleep(0.1)

    time.sleep(0.5)
    clear_buffer(ws, timeout=0.5)

    ws.write(b"\r\n", frame=WEBREPL_FRAME_TXT)
    time.sleep(0.3)
    clear_buffer(ws, timeout=0.3)


def remote_eval(ws, python_expr, timeout=3000):
    """Execute Python expression on remote and return ONLY the clean output"""
    clear_buffer(ws, timeout=0.5)

    marker = "THE_END_OF_THIS_GENERATED_COMMAND"
    full_cmd = f"{python_expr};print('{marker}')\r\n"
    ws.write(full_cmd.encode("utf-8"), frame=WEBREPL_FRAME_TXT)

    buf = b""
    t_start = time.time()
    marker_bytes = marker.encode("utf-8")

    while time.time() - t_start < timeout:
        try:
            b = ws.read(1, text_ok=True)
            if b:
                buf += b
                marker_count = buf.count(marker_bytes)
                if marker_count >= 2:
                    debugmsg("Found marker twice in output")
                    break
        except Exception as e:
            debugmsg(f"Error reading response: {e}")
            break

    marker_count = buf.count(marker_bytes)
    if marker_count < 2:
        debugmsg(f"Warning: Marker found only {marker_count} time(s), expected 2")
        return b""

    if DEBUG:
        debugmsg(f"Raw buffer:\n{buf}")

    parts = buf.split(marker_bytes)
    if len(parts) < 2:
        return b""

    output_with_echo_tail = parts[1]
    if output_with_echo_tail.startswith(b"')"):
        clean_output = output_with_echo_tail[2:]
    else:
        clean_output = output_with_echo_tail

    clean_output = clean_output.strip()

    while clean_output.startswith(b">>>") or clean_output.endswith(b">>>"):
        if clean_output.startswith(b">>>"):
            clean_output = clean_output[3:].strip()
        if clean_output.endswith(b">>>"):
            clean_output = clean_output[:-3].strip()

    if DEBUG:
        debugmsg(f"Clean output:\n{clean_output}")

    return clean_output


def remote_ls(ws, cwd):
    """List files in remote directory"""
    pyexpr = (
        "import os;print(';'.join([f'{f},{os.stat(f)[0] & 0x4000 != 0},{os.stat(f)[6]}' for f in os.listdir()]))"
    )
    out = remote_eval(ws, pyexpr).decode("utf-8", errors='ignore')

    out = out.strip().split("\n")
    files = []
    for l in out:
        if "," in l:
            for entry in l.strip().split(";"):
                parts = entry.split(",")
                if len(parts) == 3:
                    name, isdir, size = parts
                    if name not in (".", ".."):
                        try:
                            files.append((name, isdir == "True", int(size)))
                        except ValueError:
                            pass
    return files


def remote_cd(ws, path):
    """Change remote directory"""
    path_escaped = path.replace("'", "\\'")
    pyexpr = f"import os; os.chdir('{path_escaped}')"
    remote_eval(ws, pyexpr)


def remote_pwd(ws):
    """Get current remote directory"""
    pyexpr = "import os;print(os.getcwd())"
    out = remote_eval(ws, pyexpr).decode("utf-8", errors='ignore')

    for l in reversed(out.strip().split("\n")):
        l_stripped = l.strip()
        if l_stripped and not l_stripped.startswith(">>>") and not l_stripped.startswith(
                "import os") and not l_stripped.startswith("print("):
            return l_stripped
    return "/"


def remote_reset(ws, hard=False):
    """Reset the MicroPython board"""
    clear_buffer(ws, timeout=0.5)

    if hard:
        print("Performing hard reset...")
        pyexpr = "import machine; machine.reset()"
    else:
        print("Performing soft reset...")
        pyexpr = "import machine; machine.soft_reset()"

    ws.write((pyexpr + "\r\n").encode("utf-8"), frame=WEBREPL_FRAME_TXT)
    time.sleep(0.5)
    print("Reset command sent. Connection will be closed.")


def remote_restart(ws):
    """Restart the worker. This is magic specific to my personal framework."""
    clear_buffer(ws, timeout=0.5)
    print("Performing worker restart...")
    pyexpr = "import os; os.chdir('/');import worker; worker.main()"

    ws.write((pyexpr + "\r\n").encode("utf-8"), frame=WEBREPL_FRAME_TXT)
    time.sleep(0.5)
    print("Restart command sent.")


def print_remote_ls(filelist):
    """Print formatted file listing"""
    if not filelist:
        print("(empty directory)")
        return

    for name, isdir, size in filelist:
        flag = "d" if isdir else "-"
        print(f"{flag} {name:30} {size:>8}")


def remote_del(ws, path):
    """Delete a file on the remote"""
    path_escaped = path.replace("'", "\\'")
    pyexpr = f"import os; os.remove('{path_escaped}')"
    remote_eval(ws, pyexpr)


def remote_getall(ws, remote_base_dir, local_base_dir):
    """Recursively download all files and directories from remote_base_dir to local_base_dir"""

    def download_recursive(ws, remote_path, local_path):
        """Recursively download a directory tree"""
        # Save current remote directory
        orig_remote_cwd = remote_pwd(ws)

        # Change to the remote directory
        remote_cd(ws, remote_path)

        # Get file listing
        files = remote_ls(ws, remote_path)

        # Create local directory if it doesn't exist
        if not os.path.exists(local_path):
            os.makedirs(local_path)
            print(f"Created directory: {local_path}")

        # Process each file/directory
        for name, isdir, size in files:
            remote_item = name
            local_item = os.path.join(local_path, name)

            if isdir:
                # Recursively download subdirectory
                print(f"Entering directory: {name}")
                download_recursive(ws, remote_item, local_item)
            else:
                # Download file
                try:
                    print(f"Downloading: {name} ({size} bytes)")
                    get_file(ws, local_item, remote_item)
                except Exception as e:
                    print(f"Error downloading {name}: {e}")

        # Restore original remote directory
        remote_cd(ws, orig_remote_cwd)

    # Start the recursive download
    download_recursive(ws, remote_base_dir, local_base_dir)
    print(f"\nDownload complete. Files saved to: {local_base_dir}")
