using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using static APX_Viewer.FileBuffer;
using System.Diagnostics;

namespace APX_Viewer
{
    class CollisionMap
    {
        struct CollPoly
        {
            public uint flags;
            public float x1;
            public float x2;
            public float x3;
            public float y1;
            public float y2;
            public float y3;
            public float z1;
            public float z2;
            public float z3;
            public float nx;
            public float ny;
            public float nz;
            public float unk;
        }

        List<CollPoly> WallPolys;
        List<CollPoly> GroundPolys;

        public void ParseWalls()
        {
            ParsePolys(ref WallPolys);
        }

        public void ParseGrounds()
        {
            ParsePolys(ref GroundPolys);
        }

        private void ParsePolys(ref List<CollPoly> polys)
        {
            polys = new List<CollPoly>();
            int startpos = filePos;
            readString(4); //HITS
            uint filesize = readInt(); //filesize
            //start of pointers block
            readInt();//cell width
            readInt();//cell height
            uint c1 = readInt();//cells x count
            uint c2 = readInt();//cells y count
            readInt();//blank?
            readInt();//blank?
            readInt();//always 0x20?
            readInt();//total size of pointers block
            //cell list pointers!
            List<uint> ptrs = new List<uint>();
            for (int i = 0; i < c1 * c2; i++)
            {
                ptrs.Add(readInt()); //point from start of pointers block, into the below section
            }
            //cell poly lists! each list here is all the polygons to check for each given cell
            for (int i = 0; i < c1 * c2; i++)
            {
                while (true)
                {
                    uint val = readInt(); //pointer into the polygons block
                    if (val == 0xFFFFFFFF) //add pointers to this entry until we hit -1
                        break;
                }
            }
            //now the actual polygons! (start of polygons block)
            while (filePos < startpos + filesize)
            {
                //for floors: 0x00XX0000 is elevation ID? not necessarily ordered by height
                //            0x0000XX00 is step sound? 1 is grassy, 2 is stony, 3 is wooden? 4 is gravel, 5 is marshy, 6 is bushy
                //            0x000000XX is some sort of flag.. 01 roughly correlates to shadowed polygons? 02 is water
                //            0xXX000000 is some sort of avoidance setting? 80 is past warp, monsters avoid? 
                //for walls: it's trickier, see export section.
                CollPoly poly = new CollPoly
                {
                    flags = readInt(), //flags?
                    x1 = readFloat(),
                    y1 = readFloat(),
                    z1 = readFloat(),
                    x2 = readFloat(),
                    y2 = readFloat(),
                    z2 = readFloat(),
                    x3 = readFloat(),
                    y3 = readFloat(),
                    z3 = readFloat(),
                    nx = readFloat(),
                    ny = readFloat(),
                    nz = readFloat(),
                    unk = readFloat() //dunno
                };
                if (poly.flags != 0x01050000)
                    polys.Add(poly);
                Debug.WriteLine(poly.flags.ToString("X8"));
            }
        }

        public void Export()
        {
            //get the stage number right - minimum two digits
            string stage = Path.GetFileNameWithoutExtension(filename).Substring(2);
            if (stage.StartsWith('g')) //trim for lwg files
                stage = stage.Substring(1);
            if (stage.StartsWith('0'))
                stage = stage.Substring(1);
            string path = Directory.GetCurrentDirectory() + "/export/m_st" + stage + "_amh/"; //use same folder as stages
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            using (StreamWriter sw = new StreamWriter(File.Create(path + "collision.obj"))) //lowercase for MeshLab
            {
                sw.WriteLine("# MH model exporter");
                int handled = 0; //1 indexed
                //group the mesh data together
                List<string> cols = new List<string> { " 1 0 0", " 0 1 0", " 0 0 1", " 1 1 0", " 1 0 1", " 0 1 1", " 0 0 0" };
                sw.WriteLine("o walls");
                for(int i = 0; i < WallPolys.Count; i++)
                {
                    //verts
                    string colour = "";
                    switch(WallPolys[i].flags)
                    {
                        case 0x01030000: //red - player only barriers?
                        case 0x01010000:
                        case 0x01050000: //??? covers a region, doesn't block players
                            colour = cols[0];
                            break;
                        case 0x00020000: //green - monster area changes? 
                        case 0x40000000: //gate! 
                            colour = cols[5];
                            break;
                        case 0x010E0000: //blue - unknown? monster only barriers?
                        case 0x00010000: //ballistae and dragonator wall? got some random huge vertical strips too
                            colour = cols[2];
                            break;
                        case 0x02050000: //yellow - monster blockers? ledge?
                        case 0x02050001:
                            colour = cols[3];
                            break;
                        case 0x00000A01: //magenta - scalable surface
                            colour = cols[4];
                            break;
                        case 0x81000000: //cyan - unknown? placed around climbing surfaces?
                        case 0x80000000:
                            colour = cols[1];
                            break;
                    }
                    //if ((WallPolys[i].flags & 0xFF000000) == 0x80000000)
                    //    colour = cols[6];
                    sw.WriteLine("v " + WallPolys[i].x1 + " " + WallPolys[i].y1 + " " + WallPolys[i].z1 + colour);
                    sw.WriteLine("v " + WallPolys[i].x2 + " " + WallPolys[i].y2 + " " + WallPolys[i].z2 + colour);
                    sw.WriteLine("v " + WallPolys[i].x3 + " " + WallPolys[i].y3 + " " + WallPolys[i].z3 + colour);
                    //normal
                    sw.WriteLine("vn " + WallPolys[i].nx + " " + WallPolys[i].ny + " " + WallPolys[i].nz);
                    //face
                    sw.WriteLine("f " + (handled * 3 + 1) + "//" + handled + " " + (handled * 3 + 2) + "//" + handled + " " + (handled * 3 + 3) + "//" + handled);
                    handled++;
                }
                sw.WriteLine("o grounds");
                for (int i = 0; i < GroundPolys.Count; i++)
                {
                    //verts
                    string colour = "";
                    if(false) //true to check flag, false to check sound
                        switch (GroundPolys[i].flags & 0x000000FF)
                        {
                            case 0x00000000: //red - grassy?
                                colour = cols[0];
                                break;
                            case 0x00000001: //green - shadowed?
                                colour = cols[1];
                                break;
                            case 0x00000002: //blue - water?
                                colour = cols[2];
                                break;
                            case 0x00000003: //yellow - bushy?
                                colour = cols[3];
                                break;
                            //case 0x00000100: //magenta
                            //    colour = cols[4];
                            //    break;
                            //case 0x00000101: //cyan
                            //    colour = cols[5];
                            //    break;
                            default:
                                break;
                        }
                    else if (true)
                        switch (GroundPolys[i].flags & 0x0000FF00)
                        {
                            case 0x00000100: //red grass/soft soil
                                colour = cols[0];
                                break;
                            case 0x00000200: //green stony
                                colour = cols[1];
                                break;
                            case 0x00000300: //blue wooden
                                colour = cols[2];
                                break;
                            case 0x00000400: //yellow gravel
                                colour = cols[3];
                                break;
                            case 0x00000500: //magenta marshy/sandy
                                colour = cols[4];
                                break;
                            case 0x00000600: //cyan bushy
                                colour = cols[5];
                                break;
                            case 0x00000000: //blank
                                break;
                            default: //otherwise, black
                                colour = cols[6];
                                break;
                        }
                    sw.WriteLine("v " + GroundPolys[i].x1 + " " + GroundPolys[i].y1 + " " + GroundPolys[i].z1 + colour);
                    sw.WriteLine("v " + GroundPolys[i].x2 + " " + GroundPolys[i].y2 + " " + GroundPolys[i].z2 + colour);
                    sw.WriteLine("v " + GroundPolys[i].x3 + " " + GroundPolys[i].y3 + " " + GroundPolys[i].z3 + colour);
                    //normal
                    sw.WriteLine("vn " + GroundPolys[i].nx + " " + GroundPolys[i].ny + " " + GroundPolys[i].nz);
                    //face
                    sw.WriteLine("f " + (handled * 3 + 1) + "//" + handled + " " + (handled * 3 + 2) + "//" + handled + " " + (handled * 3 + 3) + "//" + handled);
                    handled++;
                }
            }
        }
    }
}
