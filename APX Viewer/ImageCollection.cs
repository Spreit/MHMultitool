using System;
using System.Collections.Generic;
using static APX_Viewer.FileBuffer;
using System.Text;
using System.Diagnostics;
using System.Drawing;
using System.IO;

namespace APX_Viewer
{
    class ImageCollection
    {
        public List<Bitmap> images;

        public void Parse()
        {

            images = new List<Bitmap>();

            List<uint> imgsstart = new List<uint>();
            List<uint> imgssize = new List<uint>();

            bool tim2 = false;

            filePos = 0;

            uint imgCount = readInt();

            //verify not ecd or whatever here
            if(imgCount == 0x1A646365)
            {
                filePos -= 4;
                ecdDecrypt();
                imgCount = readInt();
            }

            // Read start offset and sizes of each image
            for (int i = 0; i < imgCount; i++)
            {
                imgsstart.Add(readInt());
                imgssize.Add(readInt());
            }
            
            //load header pointers
            for (int i = 0; i < imgCount; i++)
            {
                filePos = (int)imgsstart[i];

                if (!filename.EndsWith("_tex.bin"))
                {
                    string ogfile = Path.GetFileNameWithoutExtension(filename);
                    string path = Directory.GetCurrentDirectory() + "/export/" + ogfile + "/";
                    if (!Directory.Exists(path))
                        Directory.CreateDirectory(path);
                    using (BinaryWriter bw = new BinaryWriter(File.OpenWrite(path + i + ".png")))
                    {
                        for (int b = 0; b < imgssize[i]; b++)
                            bw.Write(readByte());
                    }
                    Bitmap png = (Bitmap)Image.FromFile(path + i + ".png");
                    images.Add(png);
                }
                else
                {
                    string test = readString(4);
                    filePos -= 4;

                    if (test == "TIM2")
                        tim2 = true;

                    if (tim2)
                    {
                        Debug.WriteLine("TIM2 Image");
                        ImageTM2 img = new ImageTM2();
                        img.Parse();
                        images.Add(img.img);
                    }
                    else
                    {
                        Debug.WriteLine("APX Image");
                        ImageAPX img = new ImageAPX();
                        img.Parse();
                        images.Add(img.img);
                    }

                    /*imgsdata[i] = new imgdata
                    {
                        start = imgsdata[i].start,
                        size = readInt(),
                        palOffset = readInt(),
                        palSize = readInt(),
                        pixelBits = readShort(),
                        width = readShort(),
                        height = readShort(),
                        mipmaps = readShort()
                    };*/
                }
            }
            //then always 0x20 and 0x1, shorts?
            Debug.WriteLine("Images: " + imgCount);
        }

    }
}
