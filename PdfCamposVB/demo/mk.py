import subprocess, textwrap
from PIL import Image, ImageDraw, ImageFont
W,H=1280,800; FPS=30
F='/usr/share/fonts/truetype/dejavu/'
def font(n,s): return ImageFont.truetype(F+n,s)
mono=font('DejaVuSansMono.ttf',22); monob=font('DejaVuSansMono-Bold.ttf',22)
sans=font('DejaVuSans.ttf',24); sansb=font('DejaVuSans-Bold.ttf',40); sansm=font('DejaVuSans.ttf',20); sansbm=font('DejaVuSans-Bold.ttf',26)
BG=(24,28,36)
ff=subprocess.Popen(['ffmpeg','-loglevel','error','-y','-f','rawvideo','-pix_fmt','rgb24','-s',f'{W}x{H}','-r',str(FPS),'-i','-',
 '-c:v','libx264','-pix_fmt','yuv420p','-crf','23','-preset','medium','-movflags','+faststart','/workspace/PdfCamposVB-demo.mp4'],stdin=subprocess.PIPE)
nframes=0
def emit(img,secs=None,n=None):
    global nframes
    n = n if n is not None else int(secs*FPS)
    b=img.convert('RGB').tobytes()
    for _ in range(n): ff.stdin.write(b)
    nframes+=n
def caption(d,text,sub=None):
    d.rectangle([0,H-90,W,H],fill=(10,12,18))
    d.text((W//2,H-62 if sub else H-45),text,font=sansbm,fill=(255,255,255),anchor='mm')
    if sub: d.text((W//2,H-28),sub,font=sansm,fill=(170,180,200),anchor='mm')
def card(lines,secs,fade=True):
    img=Image.new('RGB',(W,H),BG); d=ImageDraw.Draw(img)
    y=250
    for t,f,c in lines:
        d.text((W//2,y),t,font=f,fill=c,anchor='mm'); y+=62
    emit(img,secs)
WH=(255,255,255); GR=(170,180,200); OR=(255,170,60); GN=(110,220,140)
card([("PdfCampos",sansb,WH),("Alterar campos em desenhos PDF e DWG",sans,GR),("Lista · Obra · código de barras · descrições",sans,GR),
      ("",sans,GR),("Demonstração com os ficheiros reais de exemplo (in.pdf, in.dwg)",sansm,OR)],4)

def wrapout(s):
    parts=s.split('; ')
    out=[parts[0]]+['  '+p for p in parts[1:]]
    res=[]
    for l in out:
        res+=textwrap.wrap(l,width=118,subsequent_indent='    ',drop_whitespace=False) or ['']
    return res
def logl(name):
    return open('/workspace/demo/'+name,encoding='utf-8').read().rstrip('\n').split('\n')
def blk(lines):
    r=[]
    for l in lines:
        if l.startswith('Antes') or l.startswith('Depois'):
            h,rest=l.split(' : ',1) if ' : ' in l else l.split(':',1)
            parts=rest.strip().split('; ')
            r.append((h.strip()+':','h'))
            for p in parts: r.append(('    '+p,'o'))
        else: r.append((l,'o'))
    return r
def terminal(cmds,caps,title):
    # cmds: list of (cmdtext, outputlines[(text,kind)], caption)
    lines=[]  # (text,kind)
    def render(cur=None,cap=None,cursor=True):
        img=Image.new('RGB',(W,H),(14,16,22)); d=ImageDraw.Draw(img)
        d.rectangle([0,0,W,38],fill=(40,44,56))
        for i,c in enumerate([(255,95,86),(255,189,46),(39,201,63)]): d.ellipse([14+i*24,12,28+i*24,26],fill=c)
        d.text((W//2,19),title,font=sansm,fill=GR,anchor='mm')
        allv=lines+([(cur,'c')] if cur is not None else [])
        rows=[]
        for t,k in allv:
            if k in ('c','cd'):
                ws=textwrap.wrap(t,width=66,subsequent_indent='  ',break_long_words=False) or ['']
                for j,w in enumerate(ws): rows.append((w,k,j==0,j==len(ws)-1))
            else: rows.append((t,k,False,False))
        maxl=(H-38-90-20)//33
        vis=rows[-maxl:]
        y=52
        for t,k,first,last in vis:
            if k in ('c','cd'):
                if first: d.text((16,y),'$ ',font=monob,fill=GN)
                d.text((16+2*13.25,y),t+('█' if (k=='c' and cursor and last) else ''),font=mono,fill=WH)
            elif k=='h': d.text((16,y),t,font=monob,fill=OR if t.startswith('Antes') else GN)
            else: d.text((16,y),t,font=mono,fill=(200,205,215))
            y+=33
        if cap: caption(d,*cap) if isinstance(cap,tuple) else caption(d,cap)
        return img
    for cmd,outl,cap in cmds:
        for i in range(len(cmd)+1):
            emit(render(cmd[:i],cap),n=max(1,int(FPS/32)) if True else 1)
        emit(render(cmd,cap),secs=0.6)
        lines.append((cmd,'cd'))
        for o in outl:
            lines.append(o); emit(render(None,cap),secs=0.22)
        emit(render(None,cap),secs=2.0)
PDF_READ=[(l,'o') for l in blk(logl('log_pdf_verify.txt'))] if False else None
import os
def readlog(c): return blk(c)
# original read of in.pdf/in.dwg (real output captured earlier)
pdf_read=["Lista=7874; Obra Poço=3066.05; Obra Periplast=2514.02; Código barras=*3066.05-5432-0* (Marcação=5432, Revisão=0)"]
dwg_read=["Lista=7874; Obra Poço=3066.05; Obra Periplast=2514.02; Código barras=*3066.05-5432-0* (Marcação=5432, Revisão=0); Descrição 1=Pá Superior c/ Carboneto de Crómio; Descrição 2=Pá Superior",
 "   Peça: 1 | 1 | 5432-1 | Peça da Pá Superior","   Peça: 2 | 1 | 5432-2 | Peça da Pá Superior"]
def readblk(ls):
    r=[]
    for i,l in enumerate(ls):
        if i==0:
            for p in l.split('; '): r.append((p,'o'))
        else: r.append((l,'o'))
    return r
# --- PDF terminal
terminal([
 ("dotnet PdfCampos.Consola.dll in.pdf", readblk(pdf_read), ("1. Ler o PDF: os campos são encontrados pelo texto, não por posições fixas",)),
 ("dotnet PdfCampos.Consola.dll in.pdf out-demo.pdf --lista 1234 --obra 4077.10", blk(logl('log_pdf_edit.txt')), ("2. Alterar Lista e Obra e gravar com outro nome (out-demo.pdf)","O código de barras acompanha a Obra automaticamente")),
],None,"Terminal — PdfCampos.Consola (saída real)")
def slide(img_path,label,col,cap,boxes=None,secs=4.2):
    im=Image.open(img_path).convert('RGB'); sc=1180/im.width; im=im.resize((1180,int(im.height*sc)),Image.LANCZOS)
    img=Image.new('RGB',(W,H),(238,240,244)); d=ImageDraw.Draw(img)
    d.rectangle([0,0,W,70],fill=(24,28,36)); d.text((40,35),label,font=sansbm,fill=col,anchor='lm')
    ox,oy=50,110; img.paste(im,(ox,oy))
    if boxes:
        for (x0,y0,x1,y1) in boxes: d.rectangle([ox+x0*sc,oy+y0*sc,ox+x1*sc,oy+y1*sc],outline=(230,80,30),width=4)
    caption(d,*cap)
    return img
def xfade(a,b):
    for i in range(1,13): emit(Image.blend(a,b,i/12),n=1)
pb=slide('pdf_before_crop.png','PDF — ANTES (in.pdf)',OR,("Lista 7874 · Obra Poço 3066.05 · código de barras *3066.05-5432-0*",))
pa=slide('pdf_after_crop.png','PDF — DEPOIS (out-demo.pdf)',GN,("Lista 1234 · Obra Poço 4077.10 · código de barras *4077.10-5432-0*","Campos alterados assinalados; o texto antigo foi mesmo removido do PDF"),
   boxes=[tuple(int(v*1.0625) for v in b) for b in [(78,383,185,418),(220,370,552,436),(884,234,972,270)]])
emit(pb,3.5); xfade(pb,pa); emit(pa,4.5)
# --- DWG terminal
terminal([
 ("dotnet PdfCampos.Consola.dll in.dwg", readblk(dwg_read), ("3. O mesmo para DWG: campos e lista de peças (MTEXT) detetados",)),
 ('dotnet PdfCampos.Consola.dll in.dwg out-demo.dwg --lista 1234 --obra 4077.10 --peca 5432-1="Chapa reforçada"', blk(logl('log_dwg_edit.txt')), ("4. Alterar Lista, Obra e a descrição da peça 5432-1","Gravado como out-demo.dwg")),
],None,"Terminal — PdfCampos.Consola (saída real)")
db=slide('dwg_b_crop.png','DWG — ANTES (in.dwg)',OR,("Lista 7874 · Obra Poço 3066.05 · peça 5432-1: «Peça da Pá Superior»",))
da=slide('dwg_a_crop.png','DWG — DEPOIS (out-demo.dwg)',GN,("Lista 1234 · Obra Poço 4077.10 · peça 5432-1: «Chapa reforçada»","Pré-visualização do DWG gerada a partir do ficheiro gravado"),
   boxes=[tuple(int(v*1.64) for v in b) for b in [(58,383,178,427),(192,378,492,432),(768,248,952,282),(288,63,432,94)]])

emit(db,3.5); xfade(db,da); emit(da,4.5)
card([("Resumo",sansb,WH),("",sans,GR),("PDF e DWG: Lista, Obra, código de barras e descrições alterados",sans,WH),
      ("Campos encontrados pelo conteúdo, mesmo que mudem de posição",sans,GR),("Ficheiro gravado sempre com outro nome",sans,GR),("",sans,GR),
      ("Gravado a partir da consola (mesma biblioteca da janela WinForms)",sansm,OR),("A janela Windows Forms não foi executada nesta gravação (máquina Linux)",sansm,OR)],5)
ff.stdin.close(); ff.wait(); print('frames',nframes, nframes/FPS)
