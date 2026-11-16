import time, numpy as np, sys
from elliot import Elliot
from mesh import *
t=time.time()
e=Elliot()
fine=float(sys.argv[1]) if len(sys.argv)>1 else 0.004
V,F=polygonise(e.m,[-0.88,-0.01,-0.2],[0.88,2.58,0.32],fine=fine,coarse=0.012 if fine>0.003 else 0.01,log=lambda s: None)
print('mc',len(V),len(F),time.time()-t)
V,F=keep_largest(V,F); print('kept',len(F))
np.savez('/tmp/claude-0/elliot_dense.npz',V=V,F=F)
v,f=decimate(V,F,80000); print('dec',len(v),len(f),time.time()-t)
np.savez('/tmp/claude-0/elliot_low.npz',V=v,F=f)
