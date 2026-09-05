"""Build original, game-native cuboid geometry and deterministic pixel textures.
Requires Pillow + NumPy. No downloaded art, no real-world dimensions.
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import json, math, random, numpy as np, wave, struct
ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / 'assets/navy1851'
for folder in ['shapes/item','textures/item','itemtypes','lang','recipes/grid','sounds']:
    (ASSETS/folder).mkdir(parents=True,exist_ok=True)
rng=np.random.default_rng(1851)
N=128
# Warm walnut, cold bluing, mottled case steel, and aged brass. Textures stay pixel crisp.
for name,base in [('blue',(43,53,65)),('edge',(105,116,120)),('brass',(128,96,49)),('walnut',(62,32,20)),('case',(70,74,79)),('black',(13,15,18)),('paper',(176,143,94))]:
    yy,xx=np.mgrid[0:N,0:N]
    noise=rng.normal(0,2,(N,N))
    if name=='walnut':
        flow=xx+(128-yy)*.34+8*np.sin(yy*.018)
        grain=np.sin(flow*.38)*5+np.sin(flow*.83)*2
        grain+=np.sin(xx*.10+yy*.006)*3
    elif name=='case':grain=np.sin(xx*.13+np.sin(yy*.11))*3+np.cos(yy*.13+xx*.047)*2
    else:grain=np.sin(yy*.19)*1.2+np.sin(xx*.044)*2
    a=np.clip(np.array(base)+noise[:,:,None]+grain[:,:,None],0,255).astype('uint8')
    if name=='case':
        a[:,:,0]=np.clip(a[:,:,0]+3*np.sin(xx*.09+yy*.05),0,255)
        a[:,:,2]=np.clip(a[:,:,2]+4*np.cos(xx*.13+yy*.1),0,255)
    im=Image.fromarray(a,'RGB');d=ImageDraw.Draw(im)
    if name in ['blue','edge','brass']:
        for _ in range(60):
            x,y=map(int,rng.integers(3,N-4,2)); shade=tuple(min(255,int(v)+14) for v in base)
            d.line((x,y,min(N-1,x+int(rng.integers(1,9))),y),fill=shade)
        d.line((0,1,N,1),fill=tuple(min(255,v+24) for v in base))
    im.save(ASSETS/f'textures/item/{name}.png')
# Original stylised naval engraving, not a scan of the historic roll engraving.
eng=Image.open(ASSETS/'textures/item/blue.png').resize((256,128))
d=ImageDraw.Draw(eng)
for yy in [3,7,120,124]:d.line((0,yy,255,yy),fill=(128,133,129),width=1)
for y in [40,48,58,90,102]:
    points=[(x,y+round(2*math.sin(x*.13+y))) for x in range(256)]
    d.line(points,fill=(91,104,112))
for cx in [47,132,214]:
    cy=78
    d.line([(cx-26,cy),(cx-15,cy+8),(cx+17,cy+8),(cx+28,cy-1),(cx-26,cy)],fill=(151,146,127),width=1)
    for x,h in [(cx-10,30),(cx+3,42),(cx+17,27)]:
        d.line((x,cy,x,cy-h),fill=(158,149,127))
        d.line([(x,cy-h+3),(x-13,cy-9),(x,cy-11),(x,cy-h+3)],fill=(134,141,139))
        d.line([(x+1,cy-h+6),(x+12,cy-12),(x+1,cy-13)],fill=(104,119,129))
    for z in range(cx-14,cx+17,6):d.point((z,cy+4),fill=(183,167,135))
eng.save(ASSETS/'textures/item/engraving.png')
# Tiny maker's plate. All artwork is game-scaled and decorative.
plate=Image.open(ASSETS/'textures/item/blue.png').resize((256,64));d=ImageDraw.Draw(plate)
d.rectangle((8,8,247,55),outline=(123,127,122),width=1)
d.text((39,23),'NAVY . 1851   /   HARTFORD',font=ImageFont.load_default(),fill=(164,157,134))
plate.save(ASSETS/'textures/item/mark.png')

textures={k:f'navy1851:item/{k}' for k in ['blue','edge','brass','walnut','case','black','paper','engraving','mark']}
sizes={k:([256,128] if k=='engraving' else [256,64] if k=='mark' else [128,128]) for k in textures}
elements=[]

def box(name,frm,to,mat='blue',rot=None,origin=None,faceuv=None):
    faces={f:{'texture':'#'+mat,'uv':[0,0,*sizes[mat]]} for f in ['north','east','south','west','up','down']}
    if faceuv:
        for f,(m,uv) in faceuv.items():faces[f]={'texture':'#'+m,'uv':uv}
    e={'name':name,'from':[round(v,5) for v in frm],'to':[round(v,5) for v in to],'faces':faces}
    if rot:
        e['rotationOrigin']=origin
        for axis,a in zip('XYZ',rot):
            if a:e['rotation'+axis]=a
    elements.append(e);return e

def tube(name,x0,x1,cy,cz,r,mat,n=16,thick=.1,engrave=False):
    # Flat tangent panels form a hollow regular polygon; muzzle is genuinely recessed.
    half=r*math.tan(math.pi/n)
    for i in range(n):
        uv={'up':('engraving',[0,i*128/n,256,(i+1)*128/n])} if engrave else None
        box(f'{name}-{i:02}',[x0,cy+r-thick/2,cz-half],[x1,cy+r+thick/2,cz+half],mat,
            [i*360/n,0,0],[0,cy,cz],uv)

def disc(name,x0,x1,cy,cz,r,mat,n=8):
    # Overlapping strips provide a faceted filled cap within the outer tube.
    for i in range(n//2):
        box(f'{name}-{i}',[x0,cy-r*.92,cz-r*.38],[x1,cy+r*.92,cz+r*.38],mat,[i*180/(n//2),0,0],[0,cy,cz])

def segment(name,a,b,width,depth,mat='brass',z=8):
    dx,dy=b[0]-a[0],b[1]-a[1];length=math.hypot(dx,dy)
    cx,cy=(a[0]+b[0])/2,(a[1]+b[1])/2
    box(name,[cx-length/2,cy-width/2,z-depth/2],[cx+length/2,cy+width/2,z+depth/2],mat,
        [0,0,math.degrees(math.atan2(dy,dx))],[cx,cy,z])

# Geometry points along +X. Origin and dimensions are arbitrary Vintage Story model units.
tube('octagonal-barrel',12,27,11.8,8,.76,'blue',8,.15)
tube('muzzle-crown',26.89,27.06,11.8,8,.74,'edge',8,.08)
disc('recessed-bore',26.58,26.6,11.8,8,.57,'black')
box('barrel-heel',[11.3,10.35,7.22],[13.3,12.4,8.78],'blue')
box('barrel-top-mark',[15,12.582,7.72],[22.3,12.591,8.28],'blue',faceuv={'up':('mark',[0,0,256,64])})
box('front-sight-foot',[25.7,12.5,7.82],[26.48,12.69,8.18],'brass')
box('front-sight-blade',[25.9,12.65,7.91],[26.29,13.05,8.09],'brass')
# Under-barrel loading lever, latch, rammer, pivot.
box('loading-lever-spine',[14,9.99,7.79],[25.7,10.31,8.21],'blue')
box('loading-lever-edge',[14.3,10.30,7.88],[25.4,10.34,8.12],'edge')
box('lever-latch',[24.3,10.3,7.73],[24.66,11.16,8.27],'blue')
box('lever-grip',[24.1,9.9,7.73],[25.8,10.35,8.27],'blue')
box('rammer-arm',[11.9,9.47,7.64],[15,10.14,8.36],'case')
box('rammer-plunger',[10.94,9.7,7.75],[12.5,10.25,8.25],'edge')
# Cylinder open above. No top strap on a Navy.
tube('cylinder',7.75,11.05,11.25,8,1.3,'blue',16,.13,True)
disc('cylinder-face',10.97,11.10,11.25,8,1.32,'case',16)
disc('cylinder-rear',7.70,7.86,11.25,8,1.32,'blue',16)
tube('cylinder-front-rim',10.80,11.0,11.25,8,1.315,'edge',16,.065)
tube('cylinder-rear-rim',7.8,7.96,11.25,8,1.315,'edge',16,.065)
for i in range(6):
    a=i*math.tau/6;cy=11.25+.83*math.cos(a);cz=8+.83*math.sin(a)
    disc(f'chamber-{i}',11.11,11.13,cy,cz,.255,'black',8)
    disc(f'cap-{i}',7.55,7.71,cy,cz,.14,'brass',8)
# Recoil shield is a narrow disc behind the cylinder, with warm case hardening.
disc('recoil-shield',7.15,7.58,11.17,8,1.30,'case',16)
box('frame-bed',[5.96,8.55,7.25],[11.67,10.02,8.75],'case')
box('frame-left-cheek',[5.94,8.62,7.07],[7.55,11.67,7.28],'case')
box('frame-right-cheek',[5.94,8.62,8.72],[7.55,11.67,8.93],'case')
box('wedge',[11.8,10.74,6.99],[12.47,11.16,9.01],'edge')
box('wedge-retainer',[11.42,11.1,7.06],[11.7,11.41,7.26],'blue')
# Sculpted exposed hammer and a visibly separate spur.
segment('hammer-root',(6.55,10.24),(6.55,11.82),.46,.39,'case')
segment('hammer-neck',(6.55,11.7),(6.25,12.68),.40,.37,'case')
segment('hammer-spur',(6.22,12.62),(5.32,12.94),.27,.52,'edge')
for i in range(6):
    box(f'hammer-checkering-{i}',[5.34+i*.135,12.85,7.74],[5.39+i*.135,12.95,8.26],'blue')
# Continuous brass trigger guard, negative space retained.
guard=[]
for i in range(25):
    a=2*math.pi*i/24
    guard.append((8.05+1.30*math.cos(a),7.45+1.03*math.sin(a)))
for i,(a,b) in enumerate(zip(guard,guard[1:])):segment(f'trigger-guard-{i}',a,b,.17,.40)
segment('trigger-upper',(7.49,8.49),(7.54,7.52),.14,.23,'blue')
segment('trigger-curve',(7.54,7.52),(7.85,6.94),.14,.23,'blue')
# Walnut grip contour built from shallow slices; bevelled sides and metal straps.
# y, x-back, x-front: the heel flares and the neck narrows, like the original silhouette.
# Profile traced from Ari's second reference image (1920 x 1080 image coordinates).
# The reference contributes silhouette only; all texture pixels are original.
# Front has a deep concave finger relief, back has a convex shoulder and swept heel.
reference_profile=[
    (880,62,360),(860,60,356),(820,83,348),(760,112,345),
    (700,141,349),(650,167,368),(615,183,394),(595,194,425),
    (582,203,455),(555,218,455),(530,237,455),(507,261,455),
    (485,290,455),(467,325,455),(455,368,455),(450,417,455)
]
# Affine mapping aligns the reference cylinder with the existing game model.
def profile_point(px,py): return ((px-584)*.01381+7.75, (430-py)*.01405+9.95)
contour=[]
for py,left,right in reference_profile:
    xb,y=profile_point(left,py);xf,_=profile_point(right,py);contour.append((y,xb,xf))
# Monotone cubic interpolation keeps rounded slopes without cubic overshoot.
def smooth_profile(y,column):
    xs=[c[0] for c in contour]; vs=[c[column] for c in contour]
    ds=[(vs[i+1]-vs[i])/(xs[i+1]-xs[i]) for i in range(len(xs)-1)]
    slopes=[ds[0]]+[0 if ds[i-1]*ds[i]<=0 else 2*ds[i-1]*ds[i]/(ds[i-1]+ds[i]) for i in range(1,len(xs)-1)]+[ds[-1]]
    i=max(0,min(len(xs)-2,int(np.searchsorted(xs,y))-1));h=xs[i+1]-xs[i];t=(y-xs[i])/h
    return (2*t**3-3*t*t+1)*vs[i]+(t**3-2*t*t+t)*h*slopes[i]+(-2*t**3+3*t*t)*vs[i+1]+(t**3-t*t)*h*slopes[i+1]
ys=np.linspace(contour[0][0],contour[-1][0],81)
for i,(y,y2) in enumerate(zip(ys,ys[1:])):
    xb,xf=[smooth_profile((y+y2)/2,j) for j in [1,2]]
    # Three stepped depth bands approximate a rounded cross section in native cuboids.
    depth=float(np.interp(y,[3.6,5.2,7.0,9.7],[1.45,1.62,1.52,1.27]))
    for j,(inset,zextra) in enumerate([(0,0),(.065,.085),(.16,.12)]):
        if xf-xb<=2*inset:continue
        lo,hi=xb+inset,xf-inset
        uv=[(lo-.3)/5.9*128,(9.8-y2)/6.5*128,(hi-.3)/5.9*128,(9.8-y)/6.5*128]
        if j==0:
            box(f'grip-heart-{i}',[lo,y,8-depth/2],[hi,y2,8+depth/2],'walnut',faceuv={'north':('walnut',uv),'south':('walnut',uv)})
        else:
            prev=0 if j==1 else .085
            box(f'grip-left-round-{i}-{j}',[lo,y,8-depth/2-zextra],[hi,y2,8-depth/2-prev],'walnut',faceuv={'north':('walnut',uv)})
            box(f'grip-right-round-{i}-{j}',[lo,y,8+depth/2+prev],[hi,y2,8+depth/2+zextra],'walnut',faceuv={'south':('walnut',uv)})
# Continuous narrow straps follow the same two curves, stopping at the wood/frame seam.
for i,(y,y2) in enumerate(zip(ys[::2],ys[2::2])):
    xb,xf=smooth_profile(y,1),smooth_profile(y,2)
    xb2,xf2=smooth_profile(y2,1),smooth_profile(y2,2)
    segment(f'backstrap-{i}',(xb-.02,y),(xb2-.02,y2),.105,1.16,'brass')
    if y<7.85:segment(f'frontstrap-{i}',(xf+.012,y),(xf2+.012,y2),.10,1.16,'brass')
segment('butt-strap',(contour[0][1],contour[0][0]),(contour[0][2],contour[0][0]),.12,1.34,'brass')
# Brass throat blends the grip into the lower frame and the rounded trigger guard.
segment('grip-throat',(5.97,7.86),(6.94,8.40),.36,1.22,'brass')
segment('grip-frame-strap',(5.97,8.34),(10.83,8.82),.20,1.22,'brass')
# Flush screws with distinct slots rather than painted dots.
for idx,(x,y,z) in enumerate([(6.96,10.50,6.98),(6.58,9.66,6.98),(12.12,11.62,7.14),(6.96,10.50,9.02),(6.58,9.66,9.02)]):
    box(f'screw-{idx}',[x-.16,y-.16,z-.05],[x+.16,y+.16,z+.05],'edge',[0,0,45],[x,y,z])
    box(f'screw-slot-{idx}',[x-.12,y-.024,z-.056],[x+.12,y+.024,z+.056],'black',[0,0,-20],[x,y,z])
shape={'editor':{'allAngles':True},'textureWidth':128,'textureHeight':128,'textureSizes':sizes,'textures':textures,'elements':elements}
(ASSETS/'shapes/item/navy1851.json').write_text(json.dumps(shape,indent=2)+'\n')
# A second genuine game model for the paper-wrapped ammunition bundle.
saved=elements;elements=[]
box('paper-packet',[5.6,4.0,6.6],[10.4,10.8,9.4],'paper')
box('packet-fold',[5.75,10.8,6.8],[10.25,11.3,9.2],'paper')
box('binding',[5.57,7.1,6.57],[10.43,7.5,9.43],'walnut')
box('label',[6.1,8.15,9.41],[9.9,10,9.44],'blue',faceuv={'south':('mark',[0,0,256,64])})
(ASSETS/'shapes/item/papercharge.json').write_text(json.dumps({**shape,'elements':elements},indent=2)+'\n')
elements=saved
item={
 'code':'revolver','class':'Navy1851Revolver','maxstacksize':1,
 'tags':['weapon','weapon-ranged'], 'creativeinventory':{'general':['*'],'items':['*'],'tools':['*']},
 'shape':{'base':'navy1851:item/navy1851'},'textures':{k:{'base':v} for k,v in textures.items()},
 'heldPriorityInteract':True,
 'attributes':{'damage': 10, 'damageTier': 2, 'range': 40, 'heldItemPitchFollow': 0.9, 'aimTransform': {'translation': {'x': -1.06, 'y': 0.1, 'z': -0.4}, 'rotation': {'x': -5, 'y': 15, 'z': 0}, 'origin': {'x': 0.203125, 'y': 0.39375, 'z': 0.5}, 'scale': 0.62}},
 'guiTransform':{'translation':{'x':-0.1,'y':0,'z':0},'rotation':{'x':0,'y':-22,'z':-28},'origin':{'x':.94,'y':.5,'z':.5},'scale':.65},
 'tpHandTransform':{'translation': {'x': -1.059585, 'y': -0.05, 'z': -0.623355}, 'rotation': {'x': -5, 'y': -2, 'z': 0}, 'origin': {'x': 0.203125, 'y': 0.39375, 'z': 0.5}, 'scale': 0.62},
 'groundTransform':{'translation':{'x':0,'y':0,'z':0},'rotation':{'x':90,'y':0,'z':0},'origin':{'x':.94,'y':.5,'z':.5},'scale':1.5}}
(ASSETS/'itemtypes/revolver.json').write_text(json.dumps(item,indent=2)+'\n')
(ASSETS/'itemtypes/papercharge.json').write_text(json.dumps({'code':'papercharge','maxstacksize':64,'creativeinventory':{'general':['*'],'items':['*']},'shape':{'base':'navy1851:item/papercharge'},'textures':item['textures'],'guiTransform':{'rotation':{'x':-15,'y':-25,'z':-10},'scale':1.6}},indent=2)+'\n')
lang={'item-revolver':'Colt 1851 Navy','item-papercharge':'Navy paper charge','rounds':'Loaded: {0} / {1}',
'description':'Hold right mouse to cock and keep firing until empty. Release to stop. Sneak + hold right mouse to load one charge at a time.',
'help-fire':'Hold to fire until empty; release to stop','help-reload':'Load paper charges',
'itemdesc-revolver':'An 1851 Navy-inspired revolver. Six chambers, blued octagonal barrel, engraved cylinder, walnut grip, and aged brass. Uses simplified paper charges.',
'itemdesc-papercharge':'One game ammunition charge for the Navy revolver. Keep these in your inventory, then sneak + hold right mouse to reload.'}
lang.update({'description': 'Hold left mouse to fire. Hold right mouse to aim down the sights for a tighter shot. Sneak + right mouse to reload.', 'help-fire': 'Hold to fire; release to stop', 'help-aim': 'Aim down sights'})
(ASSETS/'lang/en.json').write_text(json.dumps(lang,indent=2)+'\n')
# Abstract game recipes, using actual 1.22.7 resource identifiers.
recipes={
'revolver':{'ingredientPattern':'III\tBIB\t_P_','width':3,'height':3,'ingredients':{'I':{'type':'item','code':'game:ingot-iron'},'B':{'type':'item','code':'game:ingot-brass'},'P':{'type':'item','code':'game:plank-walnut'}},'output':{'type':'item','code':'navy1851:revolver'}},
'papercharge':{'ingredientPattern':'IFP','width':3,'height':1,'shapeless':True,'ingredients':{'I':{'type':'item','code':'game:ingot-copper'},'F':{'type':'item','code':'game:flaxfibers'},'P':{'type':'item','code':'game:blastingpowder'}},'output':{'type':'item','code':'navy1851:papercharge','quantity':12}}}
for name,data in recipes.items():(ASSETS/f'recipes/grid/{name}.json').write_text(json.dumps(data,indent=2)+'\n')
# Original synthetic game sound effects, mono PCM. Converted to Ogg by package step.
sr=44100
for kind,duration in [('shot',.8),('cock',.18),('load',.35),('empty',.09)]:
 t=np.arange(int(sr*duration))/sr;noise=rng.uniform(-1,1,len(t));sig=np.zeros(len(t))
 if kind=='shot':
  sig=noise*np.exp(-t*21)*.65+np.sin(2*np.pi*(95*t-22*t*t))*np.exp(-t*13)*.48
  sig+=np.convolve(noise,np.ones(14)/14,'same')*np.exp(-t*5)*.24
 else:
  for delay,amp in ([(0,.6),(.085,.35)] if kind=='cock' else [(0,.35),(.17,.6),(.24,.25)] if kind=='load' else [(0,.4)]):
   tt=np.maximum(0,t-delay);env=np.exp(-tt*130)*(t>=delay)
   sig+=(noise*.45+np.sin(2*np.pi*2100*tt)*.25)*env*amp
 sig=np.clip(sig,-.98,.98)
 with wave.open(str(ASSETS/f'sounds/{kind}.wav'),'wb') as w:
  w.setnchannels(1);w.setsampwidth(2);w.setframerate(sr);w.writeframes((sig*32767).astype('<i2').tobytes())
print(f'Built {len(elements)} cuboids; {len(elements)*12} triangles before hidden-face removal.')

# Mono Vorbis retains positional sound in-game. libsndfile is also used by soundfile.
import ctypes, ctypes.util
libpath=ctypes.util.find_library('sndfile')
if not libpath and Path('/opt/homebrew/lib/libsndfile.dylib').exists():libpath='/opt/homebrew/lib/libsndfile.dylib'
if not libpath:raise RuntimeError('Install libsndfile to encode the original mono sound effects.')
lib=ctypes.CDLL(libpath)
class SFInfo(ctypes.Structure):
    _fields_=[('frames',ctypes.c_longlong),('samplerate',ctypes.c_int),('channels',ctypes.c_int),('format',ctypes.c_int),('sections',ctypes.c_int),('seekable',ctypes.c_int)]
lib.sf_open.argtypes=[ctypes.c_char_p,ctypes.c_int,ctypes.POINTER(SFInfo)];lib.sf_open.restype=ctypes.c_void_p
lib.sf_write_short.argtypes=[ctypes.c_void_p,ctypes.POINTER(ctypes.c_short),ctypes.c_longlong];lib.sf_write_short.restype=ctypes.c_longlong
lib.sf_close.argtypes=[ctypes.c_void_p];lib.sf_close.restype=ctypes.c_int
for p in (ASSETS/'sounds').glob('*.wav'):
    with wave.open(str(p),'rb') as w: samples=np.frombuffer(w.readframes(w.getnframes()),dtype='<i2').copy();rate=w.getframerate()
    info=SFInfo(0,rate,1,0x200060,0,0)
    handle=lib.sf_open(str(p.with_suffix('.ogg')).encode(),0x20,ctypes.byref(info))
    if not handle:raise RuntimeError('Vorbis encoder failed to open '+str(p))
    written=lib.sf_write_short(handle,samples.ctypes.data_as(ctypes.POINTER(ctypes.c_short)),len(samples))
    closed=lib.sf_close(handle)
    assert written==len(samples) and closed==0
    p.unlink()
