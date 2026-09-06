using System;
using System.Collections.Generic;
using static APX_Viewer.FileBuffer;
using System.Text;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace APX_Viewer
{
    class ImageTM2
    {
        public Bitmap img;

        public struct imgdata
        {
            public uint start;
            public uint size;
            public uint palOffset;
            public uint palSize;
            public ushort pixelBits;
            public ushort width;
            public ushort height;
            public ushort mipmaps;
        }

        public void Parse()
        {
            imgdata dat = new imgdata();

            int startpos = filePos;
            filePos += 4;
            
            readByte(); //version
            byte spacer = readByte(); //alignment
            dat.mipmaps = readShort(); //number of images
            filePos += 0x8 + (spacer * 0x70); //keep in alignment
            readInt(); //size of image + this subheader?
            dat.start = (uint)(0x40 + (spacer * 0xC0)); //0x20 to align with apx
            dat.palSize = readInt(); //pallete size
            dat.size = readInt(); //image data size
            readShort(); //header size?
            dat.palOffset = dat.size;
            uint palCount = readShort(); //number of entries
            if (palCount == 0x100)
                dat.pixelBits = 8;
            else if (palCount == 0x10)
                dat.pixelBits = 4;
            //dat.pixelBits = (ushort)(dat.palSize / palCount * 8);//this is currently bits per palette entry
            readByte(); //format
            readByte(); //mipmaps
            readByte(); //clut format
            readByte(); //depth?
            dat.width = readShort();//width?
            dat.height = readShort();//height?

            Debug.WriteLine("Palette size: " + dat.palSize);
            Debug.WriteLine("Pixel bits: " + dat.pixelBits);
            Debug.WriteLine("Image dimensions: " + dat.width + " X " + dat.height);
            Debug.WriteLine("Number of mipmaps: " + dat.mipmaps);
            //palettes
            filePos = (int)(startpos + 0x40 + dat.palOffset + spacer * 0xC0);
            int palEntries;
            //if (palCount == 0)
            palEntries = (int)Math.Pow(2, dat.pixelBits);
            //else
            //    palEntries = palCount;
            List<Color> pal = new List<Color>();
            List<SolidBrush> palBrushes = new List<SolidBrush>();
            for (int c = 0; c < palEntries; c++)
            {
                if (dat.palSize / palEntries == 2) //two bytes per entry
                {
                    //1555 format
                    ushort s = readShort();
                    byte a = (byte)(((s & 0x8000) == 0x8000) ? 0xFF : 0x00);
                    //byte b = (byte)((s >> 7) | (s >> 12)); //too intense!
                    //byte g = (byte)((s >> 2) | (s >> 7));
                    //byte r = (byte)((s << 3) | (s >> 2));
                    byte b = (byte)((s >> 7) & 0xF1);
                    byte g = (byte)((s >> 2) & 0xF1);
                    byte r = (byte)((s << 3) & 0xF1);
                    //pal.Add(Color.FromArgb(b2 & 0xF0, (b1 << 4) & 0xF0, b1 & 0xF0, (b2 << 4) & 0xF0));
                    pal.Add(Color.FromArgb(a, r, g, b));
                    //Debug.WriteLine("entry " + c + ": " + b1.ToString("X") + ", " + b2.ToString("X"));
                }
                else if (dat.palSize / palEntries == 4) //four bytes per entry
                {

                    byte r = readByte();
                    byte g = readByte();
                    byte b = readByte();
                    byte a = readByte();
                    pal.Add(Color.FromArgb(a, r, g, b));
                }
                //g.FillRectangle(new SolidBrush(pal[c]), 0+c*20, 0, 20, 50);
            }
            //unfilter palettes
            List<Color> pal2 = new List<Color>();
            int parts = pal.Count / 32;//part
            int blocks = 2;//block
            int stripes = 2;//stripe
            int colours = 8;//swatches
            for (int part = 0; part < parts; part++)
                for (int block = 0; block < blocks; block++)
                    for (int stripe = 0; stripe < stripes; stripe++)
                        for (int colour = 0; colour < colours; colour++)
                            pal2.Add(pal[part * colours * stripes * blocks + block * colours + stripe * stripes * colours + colour]);
            if (parts > 0)
                pal = pal2;
            for (int i = 0; i < pal.Count; i++)
                palBrushes.Add(new SolidBrush(pal[i]));
            //data

            // Draw the image 
            filePos = (int)(startpos + dat.start);

            img = new Bitmap(dat.width, dat.height);

            if (dat.pixelBits == 0)  // Raw pixels
            {
                DrawImageWithRGBABytes(img, filePos);
            }
            else  // With Palette Brushes
            {
                DrawImageWithPaletteBrushes(img, dat.pixelBits, filePos, palBrushes);
            }


            if (false)
            {
                Graphics gfx = Graphics.FromImage(img);
                gfx.Clear(Color.Transparent);
                for (int c = 0; c < palBrushes.Count; c++)
                    gfx.FillRectangle(palBrushes[c], img.Width / 16 * (c % 16), (int)(img.Height / 16 * Math.Floor(c / 16.0)), img.Width / 16, img.Height / 16);
            }
        }


        void DrawImageWithRGBABytes(Bitmap img, int rawPixelsStartOffset)
        {
            filePos = rawPixelsStartOffset;

            Graphics gfx = Graphics.FromImage(img);
            gfx.Clear(Color.Transparent);

            for (int y = 0; y < img.Height; y++)
            {
                for (int x = 0; x < img.Width; x++)
                {
                    byte rb = readByte();
                    byte gb = readByte();
                    byte bb = readByte();
                    byte ab = readByte();
                    gfx.FillRectangle(new SolidBrush(Color.FromArgb(ab, rb, gb, bb)), x, y, 1, 1);
                }
            }
        }


        void DrawImageWithPaletteBrushes(Bitmap img, int pixelBits, int brushIndexStartOffset, List<SolidBrush> palBrushes)
        {
            filePos = brushIndexStartOffset;

            Graphics gfx = Graphics.FromImage(img);
            gfx.Clear(Color.Transparent);

            byte brushIndex;

            for (int y = 0; y < img.Height; y++)
            {
                for (int x = 0; x < img.Width; x++)
                {
                    brushIndex = readByte();

                    if (pixelBits == 4)  // Two brushes per byte
                    {
                        gfx.FillRectangle(palBrushes[brushIndex >> 4], x + 1, y, 1, 1);
                        gfx.FillRectangle(palBrushes[brushIndex & 0x0F], x, y, 1, 1);
                        x++;
                    }
                    if (pixelBits == 8)
                    {
                        gfx.FillRectangle(palBrushes[brushIndex], x, y, 1, 1);
                    }
                }
            }
        }

    }
}
