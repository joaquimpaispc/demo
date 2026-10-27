import sys, ezdxf, matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from ezdxf.addons.drawing import Frontend, RenderContext
from ezdxf.addons.drawing.matplotlib import MatplotlibBackend
doc = ezdxf.readfile(sys.argv[1])
fig = plt.figure(figsize=(30,21), dpi=200)
ax = fig.add_axes([0,0,1,1])
ctx = RenderContext(doc)
from ezdxf.addons.drawing.config import Configuration, BackgroundPolicy, ColorPolicy
Frontend(ctx, MatplotlibBackend(ax), config=Configuration(background_policy=BackgroundPolicy.WHITE,color_policy=ColorPolicy.BLACK)).draw_layout(doc.modelspace(), finalize=True)
fig.set_size_inches(30,21)
fig.savefig(sys.argv[2], dpi=200, facecolor="white")
