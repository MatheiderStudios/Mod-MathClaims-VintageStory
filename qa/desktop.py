"""Local X11 game-window input/capture for manual GUI regression evidence."""
import ctypes as c
import sys
import time
from PIL import Image

x = c.CDLL('libX11.so.6')
t = c.CDLL('libXtst.so.6')
P = c.c_void_p
U = c.c_ulong
def signature(lib, name, args, result):
    f = getattr(lib, name)
    f.argtypes, f.restype = args, result
    return f
signature(x, 'XOpenDisplay', [c.c_char_p], P)
signature(x, 'XDefaultRootWindow', [P], U)
signature(x, 'XQueryTree', [P,U,c.POINTER(U),c.POINTER(U),c.POINTER(c.POINTER(U)),c.POINTER(c.c_uint)], c.c_int)
signature(x, 'XFetchName', [P,U,c.POINTER(c.c_char_p)], c.c_int)
signature(x, 'XGetGeometry', [P,U,c.POINTER(U),c.POINTER(c.c_int),c.POINTER(c.c_int),c.POINTER(c.c_uint),c.POINTER(c.c_uint),c.POINTER(c.c_uint),c.POINTER(c.c_uint)], c.c_int)
signature(x, 'XFree', [P], c.c_int)
signature(x, 'XGetImage', [P,U,c.c_int,c.c_int,c.c_uint,c.c_uint,U,c.c_int], P)
signature(x, 'XDestroyImage', [P], c.c_int)
signature(x, 'XFlush', [P], c.c_int)
signature(x, 'XSync', [P,c.c_int], c.c_int)
signature(x, 'XRaiseWindow', [P,U], c.c_int)
signature(x, 'XSetInputFocus', [P,U,c.c_int,U], c.c_int)
signature(x, 'XInternAtom', [P,c.c_char_p,c.c_int], U)
signature(x, 'XSendEvent', [P,U,c.c_int,c.c_long,P], c.c_int)
signature(x, 'XTranslateCoordinates', [P,U,U,c.c_int,c.c_int,c.POINTER(c.c_int),c.POINTER(c.c_int),c.POINTER(U)], c.c_int)
signature(x, 'XStringToKeysym', [c.c_char_p], U)
signature(x, 'XKeysymToKeycode', [P,U], c.c_uint)
signature(t, 'XTestFakeMotionEvent', [P,c.c_int,c.c_int,c.c_int,U], c.c_int)
signature(t, 'XTestFakeButtonEvent', [P,c.c_uint,c.c_int,U], c.c_int)
signature(t, 'XTestFakeKeyEvent', [P,c.c_uint,c.c_int,U], c.c_int)
d = x.XOpenDisplay(None)
if not d:
    raise SystemExit('X display unavailable')
root = x.XDefaultRootWindow(d)
def windows(parent):
    r, p, count, children = U(), U(), c.c_uint(), c.POINTER(U)()
    x.XQueryTree(d,parent,c.byref(r),c.byref(p),c.byref(children),c.byref(count))
    ids = list(children[:count.value])
    if children: x.XFree(children)
    for w in ids:
        name = c.c_char_p()
        x.XFetchName(d,w,c.byref(name))
        title = name.value.decode(errors='replace') if name.value else ''
        if name: x.XFree(name)
        yield w,title
        yield from windows(w)
games = [(w,n) for w,n in windows(root) if 'Vintage Story' in n]
if not games: raise SystemExit('Game window not found')
w = games[-1][0]
action = sys.argv[1]
if action == 'list':
    for gw, gn in games:
        gx, gy, child = c.c_int(), c.c_int(), U()
        x.XTranslateCoordinates(d,gw,root,0,0,c.byref(gx),c.byref(gy),c.byref(child))
        print(gw,gn,gx.value,gy.value)
elif action == 'capture':
    rr, xx, yy, ww, hh, border, depth = U(),c.c_int(),c.c_int(),c.c_uint(),c.c_uint(),c.c_uint(),c.c_uint()
    x.XGetGeometry(d,w,c.byref(rr),c.byref(xx),c.byref(yy),c.byref(ww),c.byref(hh),c.byref(border),c.byref(depth))
    class XI(c.Structure):
        _fields_ = [('width',c.c_int),('height',c.c_int),('xoffset',c.c_int),('format',c.c_int),('data',P),('byte_order',c.c_int),('bitmap_unit',c.c_int),('bitmap_bit_order',c.c_int),('bitmap_pad',c.c_int),('depth',c.c_int),('bytes_per_line',c.c_int),('bits_per_pixel',c.c_int)]
    ptr = x.XGetImage(d,w,0,0,ww.value,hh.value,U(-1),2)
    if not ptr: raise SystemExit('Game capture unavailable')
    im = c.cast(ptr,c.POINTER(XI)).contents
    Image.frombytes('RGB',(im.width,im.height),c.string_at(im.data, im.bytes_per_line*im.height),'raw','BGRX',im.bytes_per_line).save(sys.argv[2])
    x.XDestroyImage(ptr)
    print(sys.argv[2])
else:
    class ClientMessage(c.Structure):
        _fields_ = [('type',c.c_int),('serial',U),('send_event',c.c_int),('display',P),('window',U),('message_type',U),('format',c.c_int),('data',c.c_long*5),('padding',c.c_long*8)]
    event = ClientMessage()
    event.type, event.display, event.window, event.format = 33,d,w,32
    event.message_type = x.XInternAtom(d,b'_NET_ACTIVE_WINDOW',0)
    event.data[0] = 2
    x.XSendEvent(d,root,0,(1<<20)|(1<<19),c.byref(event))
    x.XFlush(d)
    x.XSync(d,0)
    time.sleep(.1)
    time.sleep(.2)
    x.XRaiseWindow(d,w)
    x.XSetInputFocus(d,w,1,0)
    if action == 'move':
        gx, gy, child = c.c_int(), c.c_int(), U()
        x.XTranslateCoordinates(d,w,root,int(sys.argv[2]),int(sys.argv[3]),c.byref(gx),c.byref(gy),c.byref(child))
        t.XTestFakeMotionEvent(d,-1,gx.value,gy.value,0)
    if action in ('down','up','click'):
        button = int(sys.argv[2])
        if action != 'up': t.XTestFakeButtonEvent(d,button,1,0)
        x.XFlush(d)
        if action == 'click': time.sleep(.12)
        if action != 'down': t.XTestFakeButtonEvent(d,button,0,0)
    if action in ('key','keydown','keyup'):
        key = x.XKeysymToKeycode(d,x.XStringToKeysym(sys.argv[2].encode()))
        if action != 'keyup': t.XTestFakeKeyEvent(d,key,1,0)
        x.XFlush(d)
        if action == 'key': time.sleep(.1)
        if action != 'keydown': t.XTestFakeKeyEvent(d,key,0,0)
    x.XFlush(d)
    x.XSync(d,0)
    time.sleep(.2)
