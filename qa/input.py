"""Temporary local uinput device for game QA; destroyed when stdin closes.

Commands: move X Y, click BTN, down BTN, up BTN, key KEYCODE,
keydown KEYCODE, keyup KEYCODE. Coordinates are screen pixels.
No input is recorded. Requires existing write access to /dev/uinput.
"""
import ctypes as c
import fcntl
import os
import struct
import sys
import time

fd = os.open('/dev/uinput', os.O_WRONLY | os.O_NONBLOCK)
def ioctl(n, value): fcntl.ioctl(fd, 0x40045500 + n, value)
ioctl(100, 1)
ioctl(100, 2)
for key in list(range(1, 256)) + [272, 273, 274]: ioctl(101, key)
for axis in [0, 1, 8]: ioctl(102, axis)
fcntl.ioctl(fd, 0x405c5503, struct.pack('HHHH80sI', 3, 0x1234, 0x1234, 1, b'MathClaims QA input', 0))
fcntl.ioctl(fd, 0x5501)
x = c.CDLL('libX11.so.6')
x.XOpenDisplay.argtypes, x.XOpenDisplay.restype = [c.c_char_p], c.c_void_p
x.XDefaultRootWindow.argtypes, x.XDefaultRootWindow.restype = [c.c_void_p], c.c_ulong
x.XQueryPointer.argtypes = [c.c_void_p,c.c_ulong,c.POINTER(c.c_ulong),c.POINTER(c.c_ulong),c.POINTER(c.c_int),c.POINTER(c.c_int),c.POINTER(c.c_int),c.POINTER(c.c_int),c.POINTER(c.c_uint)]
d = x.XOpenDisplay(None)
root = x.XDefaultRootWindow(d)
def pointer():
    r,ch,rx,ry,wx,wy,m = c.c_ulong(),c.c_ulong(),c.c_int(),c.c_int(),c.c_int(),c.c_int(),c.c_uint()
    x.XQueryPointer(d,root,c.byref(r),c.byref(ch),c.byref(rx),c.byref(ry),c.byref(wx),c.byref(wy),c.byref(m))
    return rx.value,ry.value
def emit(kind,code,value): os.write(fd,struct.pack('llHHi',0,0,kind,code,value))
def sync(): emit(0,0,0)
time.sleep(1)
print('READY',pointer(),flush=True)
try:
    for line in sys.stdin:
        a = line.split()
        if not a: continue
        if a[0] == 'quit': break
        if a[0] == 'move':
            tx,ty = map(int,a[1:])
            for _ in range(30):
                px,py = pointer()
                dx,dy = tx-px,ty-py
                if abs(dx)<2 and abs(dy)<2: break
                emit(2,0,int(dx/2) or (1 if dx>0 else -1) if dx else 0)
                emit(2,1,int(dy/2) or (1 if dy>0 else -1) if dy else 0)
                sync()
                time.sleep(.04)
        elif a[0] == 'wheel': emit(2,8,int(a[1])); sync()
        else:
            code = int(a[1])
            if a[0] in ('click','down','up'): code = {1:272,3:273,2:274}[code]
            if a[0] not in ('up','keyup'): emit(1,code,1); sync()
            if a[0] in ('key','click'): time.sleep(.1)
            if a[0] not in ('down','keydown'): emit(1,code,0); sync()
        time.sleep(.15)
        print('OK',line.strip(),pointer(),flush=True)
finally:
    fcntl.ioctl(fd,0x5502)
    os.close(fd)
