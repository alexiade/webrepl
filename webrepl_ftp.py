#!/usr/bin/env python
from __future__ import print_function
import sys
import os
import struct
import time

try:
    import usocket as socket
except ImportError:
    import socket

import local_commands as lcmd
import remote_commands as rcmd

USE_BUILTIN_UWEBSOCKET = 0
DEBUG = 0

WEBREPL_FRAME_TXT = 0x81
WEBREPL_FRAME_BIN = 0x82


def debugmsg(msg):
    if DEBUG:
        print(msg)


if USE_BUILTIN_UWEBSOCKET:
    from uwebsocket import websocket
else:
    class websocket:
        def __init__(self, s):
            self.s = s
            self.buf = b""

        def write(self, data, frame=WEBREPL_FRAME_BIN):
            l = len(data)
            if l < 126:
                hdr = struct.pack(">BB", frame, l)
            else:
                hdr = struct.pack(">BBH", frame, 126, l)
            self.s.send(hdr)
            self.s.send(data)

        def recvexactly(self, sz):
            res = b""
            while sz:
                data = self.s.recv(sz)
                if not data:
                    break
                res += data
                sz -= len(data)
            return res

        def read(self, size, text_ok=False):
            if not self.buf:
                while True:
                    hdr = self.recvexactly(2)
                    assert len(hdr) == 2
                    fl, sz = struct.unpack(">BB", hdr)
                    if sz == 126:
                        hdr = self.recvexactly(2)
                        assert len(hdr) == 2
                        (sz,) = struct.unpack(">H", hdr)
                    if fl == 0x82:
                        break
                    if text_ok and fl == 0x81:
                        break
                    debugmsg("Got unexpected websocket record of type %x, skipping it" % fl)
                    while sz:
                        skip = self.s.recv(sz)
                        debugmsg("Skip data: %s" % skip)
                        sz -= len(skip)
                data = self.recvexactly(sz)
                assert len(data) == sz
                self.buf = data
            d = self.buf[:size]
            self.buf = self.buf[size:]
            assert len(d) == size, len(d)
            return d

        def ioctl(self, req, val):
            assert req == 9 and val == 2


def login(ws, passwd):
    while True:
        c = ws.read(1, text_ok=True)
        if c == b":":
            assert ws.read(1, text_ok=True) == b" "
            break
    ws.write(passwd.encode("utf-8") + b"\r")


def read_resp(ws):
    data = ws.read(4)
    sig, code = struct.unpack("<2sH", data)
    assert sig == b"WB"
    return code


def send_req(ws, op, sz=0, fname=b""):
    rec = struct.pack(WEBREPL_REQ_S, b"WA", op, 0, 0, sz, len(fname), fname)
    debugmsg("%r %d" % (rec, len(rec)))
    ws.write(rec)


def get_ver(ws):
    send_req(ws, WEBREPL_GET_VER)
    d = ws.read(3)
    d = struct.unpack("<BBB", d)
    return d


def put_file(ws, local_file, remote_file):
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


# --- Remote execution helpers using WebREPL's eval interface (REPL) ---

def clear_buffer(ws, timeout=1.0):
    """Aggressively drain all data from websocket buffer and socket"""
    # Clear the internal buffer first
    ws.buf = b""

    # Now drain anything waiting on the socket
    drained = b""
    start_time = time.time()

    try:
        # Set socket to non-blocking temporarily
        ws.s.setblocking(False)

        while time.time() - start_time < timeout:
            try:
                # Try to read websocket frames
                readable, _, _ = select.select([ws.s], [], [], 0.01)
                if readable:
                    # Read the frame header
                    hdr = ws.s.recv(2)
                    if len(hdr) < 2:
                        break
                    fl, sz = struct.unpack(">BB", hdr)
                    if sz == 126:
                        hdr2 = ws.s.recv(2)
                        if len(hdr2) < 2:
                            break
                        (sz,) = struct.unpack(">H", hdr2)

                    # Read and discard the payload
                    while sz > 0:
                        chunk = ws.s.recv(min(sz, 4096))
                        if not chunk:
                            break
                        drained += chunk
                        sz -= len(chunk)
                else:
                    # No more data available
                    break
            except BlockingIOError:
                # No data available
                break
            except Exception as e:
                debugmsg(f"Error draining buffer: {e}")
                break
    finally:
        # Restore blocking mode
        ws.s.setblocking(True)

    if DEBUG and drained:
        debugmsg(f"Drained {len(drained)} bytes from buffer: {drained[:100]}...")


def interrupt_running_code(ws):
    """Send Ctrl+C to interrupt any running code on the MicroPython board"""
    print("Interrupting any running code...")

    # First, drain any existing output
    clear_buffer(ws, timeout=0.5)

    # Send Ctrl+C (0x03) multiple times to ensure interruption
    for _ in range(3):
        ws.write(b"\x03", frame=WEBREPL_FRAME_TXT)
        time.sleep(0.1)

    # Wait for interruption to complete
    time.sleep(0.5)

    # Drain the response (traceback, etc.)
    clear_buffer(ws, timeout=0.5)

    # Send Enter to get fresh prompt
    ws.write(b"\r\n", frame=WEBREPL_FRAME_TXT)
    time.sleep(0.3)

    # Clear any response to the Enter
    clear_buffer(ws, timeout=0.3)


def remote_eval(ws, python_expr, timeout=3000):
    """Execute Python expression on remote and return ONLY the clean output"""
    # CRITICAL: Clear buffer before sending command
    clear_buffer(ws, timeout=0.5)

    # Use a fixed unique marker
    marker = "THE_END_OF_THIS_GENERATED_COMMAND"

    # Send the command followed by a print statement with our marker
    full_cmd = f"{python_expr};print('{marker}')\r\n"
    ws.write(full_cmd.encode("utf-8"), frame=WEBREPL_FRAME_TXT)

    buf = b""
    t_start = time.time()
    marker_bytes = marker.encode("utf-8")

    # Read until we see the marker TWICE (once as echo, once as actual output)
    while time.time() - t_start < timeout:
        try:
            b = ws.read(1, text_ok=True)
            if b:
                buf += b
                # Count occurrences of marker
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

    # Split by marker
    # parts[0] = everything before first marker (echo of commands up to "print('")
    # parts[1] = between first and second marker ("')\r\n" from echo + ACTUAL OUTPUT)
    # parts[2] = after second marker (empty, we stop reading right at the marker)
    parts = buf.split(marker_bytes)

    if len(parts) < 2:
        return b""

    # parts[1] contains: "')\r\n" + ACTUAL_OUTPUT
    output_with_echo_tail = parts[1]

    # Remove the closing of the print statement echo: ')
    if output_with_echo_tail.startswith(b"')"):
        clean_output = output_with_echo_tail[2:]
    else:
        clean_output = output_with_echo_tail

    # Strip whitespace
    clean_output = clean_output.strip()

    # Remove any >>> prompts that might remain
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

    # parse output (very hacky)
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
                            pass  # Skip malformed entries
    return files


def remote_cd(ws, path):
    """Change remote directory"""
    # Escape single quotes in path
    path_escaped = path.replace("'", "\\'")
    pyexpr = f"import os; os.chdir('{path_escaped}')"
    remote_eval(ws, pyexpr)


def remote_pwd(ws):
    """Get current remote directory"""
    pyexpr = "import os;print(os.getcwd())"
    out = remote_eval(ws, pyexpr).decode("utf-8", errors='ignore')

    # extract latest non-empty line that's not a prompt or echo
    for l in reversed(out.strip().split("\n")):
        l_stripped = l.strip()
        if l_stripped and not l_stripped.startswith(">>>") and not l_stripped.startswith(
                "import os") and not l_stripped.startswith("print("):
            return l_stripped
    return "/"


def remote_reset(ws, hard=False):
    """Reset the MicroPython board"""
    # Clear buffer first
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
    # Clear buffer first
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


def cmdloop(ws):
    """Interactive FTP-ish shell"""
    localcwd = os.getcwd()

    try:
        remotecwd = remote_pwd(ws)
    except Exception as e:
        print(f"Warning: Could not get remote directory: {e}")
        remotecwd = "/"

    while True:
        try:
            cmd = input(f"cmd [local:{localcwd} remote:{remotecwd}]: ").strip()
        except EOFError:
            break
        if not cmd:
            continue
        parts = cmd.split()
        if not parts:
            continue
        c = parts[0].lower()
        args = parts[1:]

        if c in ("exit", "quit"):
            break
        elif c == "lls":
            files = os.listdir(localcwd)
            for f in files:
                print(f)
        elif c == "lcd":
            if not args:
                print(localcwd)
            else:
                try:
                    os.chdir(args[0])
                    localcwd = os.getcwd()
                except Exception as e:
                    print(f"lcd: {e}")
        elif c == "lpwd":
            print(localcwd)
        elif c == "ls":
            try:
                files = remote_ls(ws, remotecwd)
                print_remote_ls(files)
            except Exception as e:
                print(f"ls: {e}")
        elif c == "cd":
            if not args:
                print(remotecwd)
            else:
                try:
                    remote_cd(ws, args[0])
                    remotecwd = remote_pwd(ws)
                except Exception as e:
                    print(f"cd: {e}")
        elif c == "pwd":
            try:
                remotecwd = remote_pwd(ws)
                print(remotecwd)
            except Exception as e:
                print(f"pwd: {e}")
        elif c == "get":
            if not args:
                print("get <remote_file> [local_file]")
            else:
                remote_file = args[0]
                if len(args) > 1:
                    local_file = args[1]
                else:
                    local_file = os.path.basename(remote_file)
                try:
                    get_file(ws, local_file, remote_file)
                    print("Downloaded", remote_file, "to", local_file)
                except Exception as e:
                    print(f"get: {e}")
        elif c == "put":
            if not args:
                print("put <local_file> [remote_file]")
            else:
                local_file = args[0]
                if len(args) > 1:
                    remote_file = args[1]
                else:
                    remote_file = os.path.basename(local_file)
                try:
                    put_file(ws, local_file, remote_file)
                    print("Uploaded", local_file, "to", remote_file)
                except Exception as e:
                    print(f"put: {e}")
        elif c == "reset" or c == "reboot":
            try:
                remote_reset(ws, hard=True)
                break  # Exit after reset
            except Exception as e:
                print(f"reset: {e}")
        elif c == "restart":
            try:
                remote_restart(ws) #this executes worker restart command. My own
                break  # Exit after reset
            except Exception as e:
                print(f"restart: {e}")
        elif c == "interrupt" or c == "break":
            try:
                interrupt_running_code(ws)
                print("Interrupt sent.")
                # Refresh remote cwd after interrupt
                remotecwd = remote_pwd(ws)
            except Exception as e:
                print(f"interrupt: {e}")
        elif c == "help":
            print("Available commands:")
            print("  ls                # List remote directory")
            print("  cd <dir>          # Change remote directory")
            print("  pwd               # Print remote directory")
            print("  get rfile [lfile] # Download remote file")
            print("  put lfile [rfile] # Upload file")
            print("  lls               # List local directory")
            print("  lcd <dir>         # Change local directory")
            print("  lpwd              # Print local directory")
            print("  reset, reboot     # Soft reset MicroPython board")
            print("  hardreset         # Hard reset MicroPython board")
            print("  interrupt, break  # Send Ctrl+C to break running code")
            print("  exit, quit        # Exit")
            print("  help              # This help message")
        else:
            print("Unknown command (try 'help')")


def help(rc=0):
    print("webrepl_ftp.py - FTP-like client for MicroPython WebREPL")
    print("Usage: webrepl_ftp.py [-p password] <host>[:port]")
    print("Options:")
    print("  -p password    Specify password (otherwise prompted)")
    print("  -h, --help     Show this help")
    sys.exit(rc)


def error(msg):
    print(msg)
    sys.exit(1)


def parse_remote(remote):
    host, fname = remote.rsplit(":", 1)
    if fname == "":
        fname = "/"
    port = 8266
    if ":" in host:
        host, port = host.split(":")
        port = int(port)
    return (host, port, fname)


def client_handshake(sock):
    cl = sock.makefile("rwb", 0)
    cl.write(b"""\
GET / HTTP/1.1\r
Host: echo.websocket.org\r
Connection: Upgrade\r
Upgrade: websocket\r
Sec-WebSocket-Key: foo\r
\r
""")
    l = cl.readline()
    while 1:
        l = cl.readline()
        if l == b"\r\n":
            break


def main():
    # Parse -p password flag first
    passwd = None
    for i in range(len(sys.argv)):
        if sys.argv[i] == '-p':
            sys.argv.pop(i)
            passwd = sys.argv.pop(i)
            break

    # Check for help
    if len(sys.argv) == 2 and sys.argv[1] in ("-h", "--help", "help"):
        help(0)

    # Minimal args: host [:port]
    if len(sys.argv) < 2:
        print("Usage: %s [-p password] <host>[:port]" % sys.argv[0])
        sys.exit(1)

    hoststr = sys.argv[1]
    port = 8266
    if ":" in hoststr:
        host, port = hoststr.split(":")
        port = int(port)
    else:
        host = hoststr

    # Get password if not provided via -p
    if passwd is None:
        import getpass
        try:
            passwd = getpass.getpass("Password: ")
        except Exception as e:
            # Handle non-TTY environments or getpass errors
            print(f"Error: Cannot prompt for password ({e})")
            print("Please use: -p <password>")
            sys.exit(1)

    print(f"Connecting to {host}:{port} ...")
    s = socket.socket()
    ai = socket.getaddrinfo(host, port)
    addr = ai[0][4]
    s.connect(addr)
    client_handshake(s)
    ws = websocket(s)
    login(ws, passwd)
    print("Remote WebREPL version:", get_ver(ws))
    ws.ioctl(9, 2)

    # Interrupt any running code before starting
    interrupt_running_code(ws)

    cmdloop(ws)
    s.close()


if __name__ == "__main__":
    main()