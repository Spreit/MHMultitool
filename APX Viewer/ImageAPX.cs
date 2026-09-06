using System;
using System.Collections.Generic;
using static APX_Viewer.FileBuffer;
using System.Text;
using System.Diagnostics;
using System.Drawing;

namespace APX_Viewer
{
    class ImageAPX
    {
        public Bitmap img;

        public void Parse()
        {
            ushort pixelBits;
            ushort width;
            ushort height;
            ushort mipmaps;
            ushort palBits;
            ushort palCount;

            int filestart = filePos;
            filePos += 0xC;
            
            pixelBits = readShort();
            width = readShort();
            height = readShort();
            mipmaps = readShort();
            palBits = readShort();
            palCount = readShort();

            Debug.WriteLine("Palette bits: " + palBits);
            Debug.WriteLine("Pixel bits: " + pixelBits);
            Debug.WriteLine("Image dimensions: " + width + " X " + height);
            Debug.WriteLine("Number of mipmaps: " + mipmaps);
            
            filePos += (int)(0x8 + width * height * pixelBits / 8);

            // Load palette
            int palEntries = (int)Math.Pow(2, pixelBits);

            List<Color> pal = LoadColorPalette(filePos, palEntries, palBits);

            // Make palette brushes
            List<SolidBrush> palBrushes = new List<SolidBrush>();

            for (int i = 0; i < pal.Count; i++)
                palBrushes.Add(new SolidBrush(pal[i]));
            //palette loaded, now draw the map!

            // Draw the image using palette brushes
            filePos = filestart + 0x20;

            img = new Bitmap(width, height);
            Graphics gfx = Graphics.FromImage(img);
            gfx.Clear(Color.Transparent);

            byte brushIndex;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
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


        public List<Color> LoadColorPalette(int palOffset, int palEntries, int palBits)
        {
            filePos = palOffset;

            List<Color> palette = new List<Color>();

            for (int c = 0; c < palEntries; c++)
            {
                Color palColor = Color.Empty;

                byte a = 0x0;
                byte r = 0x0;
                byte g = 0x0;
                byte b = 0x0;

                if (palBits == 4)
                {
                    byte arByte = readByte();
                    byte gbByte = readByte();

                    a = (byte)(arByte & 0xF0);
                    r = (byte)((arByte << 4) & 0xF0);
                    g = (byte)(gbByte & 0xF0);
                    b = (byte)((gbByte << 4) & 0xF0);
                }
                else if (palBits == 16) //ABGR1555 format, two bytes per entry
                {
                    ushort s = readShort();
                    a = (byte)(((s & 0x8000) == 0x8000) ? 0xFF : 0x00);
                    b = (byte)((s >> 7) & 0xF1);
                    g = (byte)((s >> 2) & 0xF1);
                    r = (byte)((s << 3) & 0xF1);
                }
                else if (palBits == 32)
                {
                    if (wii) //wii order
                    {
                        a = readByte();
                        b = readByte();
                        g = readByte();
                        r = readByte();
                    }
                    else //ps2 order
                    {
                        r = readByte();
                        g = readByte();
                        b = readByte();
                        a = readByte();
                    }
                }

                palColor = Color.FromArgb(a, r, g, b);
                palette.Add(palColor);
                //g.FillRectangle(new SolidBrush(pal[c]), 0+c*20, 0, 20, 50);
            }

            return palette;
        }

    }
}
