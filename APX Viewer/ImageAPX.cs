using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using static APX_Viewer.FileBuffer;

namespace APX_Viewer
{
    class ImageAPX
    {
        public Bitmap img;

        public const int APX_TEXTURE_HEADER_SIZE = 0x20;  // 32 bytes

        public struct Header
        {
            public uint totalLen;
            public uint pixelDataLen;
            public ushort paletteLen;
            public ushort unk0;

            public ushort imageBitDepth;  // 8 or 4 BPP
            public ushort width;
            public ushort height;
            public ushort mipmaps;  // Always 1?
            public ushort paletteBitDepth;  
            public ushort paletteIndex; // Always 1?

            public ushort unk1;
            public ushort unk2;
            public ushort unk3;
            public ushort unk4;
        }

        public Header header;
        public byte[] pixelData;
        public byte[] paletteData;

        public void Parse()
        {
            int textureHeaderOffset = filePos;

            Debug.WriteLine("Texture Header Offset: " + textureHeaderOffset);

            LoadHeader(textureHeaderOffset);

            if (header.pixelDataLen + header.paletteLen > header.totalLen)
            {
                Debug.WriteLine("Pixel Data Length + Pallete length is larger that Total Length. Tried to fix it");
                header.pixelDataLen = header.pixelDataLen & 0xFF00;
            }

            {
                Debug.WriteLine("Image Offset: " + filePos);

                Debug.WriteLine("Total Length:" + header.totalLen);
                Debug.WriteLine("Image Length: " + header.pixelDataLen);
                Debug.WriteLine("Palette length: " + header.paletteLen);
                Debug.WriteLine("Unk0: " + header.unk0);

                Debug.WriteLine("Color Channel Depth: " + header.imageBitDepth);
                Debug.WriteLine("Image dimensions: " + header.width + " X " + header.height);
                Debug.WriteLine("Number of mipmaps: " + header.mipmaps);
                Debug.WriteLine("Palette bits: " + header.paletteBitDepth);

                Debug.WriteLine("Unknown 1: " + header.unk1);
                Debug.WriteLine("Unknown 2: " + header.unk2);
                Debug.WriteLine("Unknown 3: " + header.unk3);
                Debug.WriteLine("Unknown 4: " + header.unk4);
            }

            int pixelDataOffset = textureHeaderOffset + APX_TEXTURE_HEADER_SIZE;
            int paletteDataOffset = pixelDataOffset + (int)header.pixelDataLen;

            int paletteColorCount = (int)(header.paletteLen / (header.paletteBitDepth / 8));

            LoadPixelData(pixelDataOffset);
            LoadPaletteData(paletteDataOffset);

            // Load palette as drawing colors
            Debug.WriteLine("Palette Offset: {0:X}", paletteDataOffset);
            Debug.WriteLine("Palette Color Count: " + paletteColorCount);

            List<Color> pal = LoadColorPalette(paletteDataOffset, paletteColorCount, header.paletteBitDepth);

            // Make palette brushes
            List<SolidBrush> palBrushes = new List<SolidBrush>();

            for (int i = 0; i < pal.Count; i++)
                palBrushes.Add(new SolidBrush(pal[i]));

            // Draw the image using palette brushes
            img = new Bitmap(header.width, header.height);

            DrawImageWithPaletteBrushes(img, header.imageBitDepth, pixelDataOffset, palBrushes);

        }


        public void LoadHeader(int offset)
        {
            filePos = offset;

            header.totalLen = readInt();
            header.pixelDataLen = readInt();
            header.paletteLen = readShort();
            header.unk0 = readShort();

            header.imageBitDepth = readShort();
            header.width = readShort();
            header.height = readShort();
            header.mipmaps = readShort();
            header.paletteBitDepth = readShort();
            header.paletteIndex = readShort();

            header.unk1 = readShort();
            header.unk2 = readShort();
            header.unk3 = readShort();
            header.unk4 = readShort();
        }

        public void LoadPixelData(int offset)
        {
            filePos = offset;

            pixelData = new byte[header.pixelDataLen];

            for (int i = 0; i < header.pixelDataLen; i++)
            {
                pixelData[i] = readByte();
            }
        }

        public void LoadPaletteData(int offset)
        {
            filePos = offset;

            paletteData = new byte[header.paletteLen];

            for (int i = 0; i < header.paletteLen; i++)
            {
                paletteData[i] = readByte();
            }
        }

        public List<Color> LoadColorPalette(int palOffset, int palEntries, int paletteColorDepth)
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

                if (paletteColorDepth == 4)
                {
                    byte arByte = readByte();
                    byte gbByte = readByte();

                    a = (byte)(arByte & 0xF0);
                    r = (byte)((arByte << 4) & 0xF0);
                    g = (byte)(gbByte & 0xF0);
                    b = (byte)((gbByte << 4) & 0xF0);
                }
                else if (paletteColorDepth == 16) //ABGR1555 format, two bytes per entry
                {
                    ushort s = readShort();
                    a = (byte)(((s & 0x8000) == 0x8000) ? 0xFF : 0x00);
                    b = (byte)((s >> 7) & 0xF1);
                    g = (byte)((s >> 2) & 0xF1);
                    r = (byte)((s << 3) & 0xF1);
                }
                else if (paletteColorDepth == 32)
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

        void DrawImageWithPaletteBrushes(Bitmap img, int imageBitDepth, int brushIndexStartOffset, List<SolidBrush> palBrushes)
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

                    if (imageBitDepth == 4)  // Two brushes per byte
                    {
                        gfx.FillRectangle(palBrushes[brushIndex >> 4], x + 1, y, 1, 1);
                        gfx.FillRectangle(palBrushes[brushIndex & 0x0F], x, y, 1, 1);
                        x++;
                    }
                    if (imageBitDepth == 8)
                    {
                        gfx.FillRectangle(palBrushes[brushIndex], x, y, 1, 1);
                    }
                }
            }
        }
    }
}
