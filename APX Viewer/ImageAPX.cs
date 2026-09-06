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

        public const int APX_TEXTURE_HEADER_SIZE = 0x20;  // 32 bytes

        public void Parse()
        {
            int filestart = filePos;
            Debug.WriteLine("Texture Header Offset: " + filestart);

            uint totalLen = readInt();
            uint imageLen = readInt();
            uint palLen = readInt();

            if (imageLen + palLen > totalLen)
            {
                Debug.WriteLine("Image Length + Pallet length is larger that Total Length. Tried to fix it");
                imageLen = imageLen & 0xFF00;
            }

            ushort colorBit = readShort();
            ushort width = readShort();
            ushort height = readShort();
            ushort mipmaps = readShort();
            ushort palBits = readShort();
            ushort palCount = readShort();

            ushort unk1 = readShort();
            ushort unk2 = readShort();
            ushort unk3 = readShort();
            ushort unk4 = readShort();

            Debug.WriteLine("Image Offset: " + filePos);

            Debug.WriteLine("Total Length:" + totalLen);
            Debug.WriteLine("Image Length: " + imageLen);
            Debug.WriteLine("Palette length: " + palLen);

            Debug.WriteLine("Color Channel Depth: " + colorBit);
            Debug.WriteLine("Image dimensions: " + width + " X " + height);
            Debug.WriteLine("Number of mipmaps: " + mipmaps);
            Debug.WriteLine("Palette bits: " + palBits);

            Debug.WriteLine("Unknown 1: " + unk1);
            Debug.WriteLine("Unknown 2: " + unk2);
            Debug.WriteLine("Unknown 3: " + unk3);
            Debug.WriteLine("Unknown 4: " + unk4);

            // Load palette
            filePos += (int)imageLen; //  Palette offset

            Debug.WriteLine("Palette Offset: {0:X}", filePos);

            int palEntries = (int)(palLen / (palBits / 8));

            Debug.WriteLine("Palette Entries: " + palEntries);

            List<Color> pal = LoadColorPalette(filePos, palEntries, palBits);

            // Make palette brushes
            List<SolidBrush> palBrushes = new List<SolidBrush>();

            for (int i = 0; i < pal.Count; i++)
                palBrushes.Add(new SolidBrush(pal[i]));

            // Draw the image using palette brushes
            filePos = filestart + APX_TEXTURE_HEADER_SIZE;  // Brush index offset

            img = new Bitmap(width, height);

            DrawImageWithPaletteBrushes(img, colorBit, filePos, palBrushes);

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
