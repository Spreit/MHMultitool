using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace APX_Viewer
{
    public partial class Form1 : Form
    {

        int curimage;
        ImageCollection imgs;

        public Form1()
        {
            InitializeComponent();
            Text = "APX viewer";
            DoubleBuffered = true;

            FileBuffer.init(false);

            int filter = 0;
            string filename = "";
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "APX files|*.apx|Texture bin files|*_tex.bin;*.txb;|Manual Decompress|*.*|TM2|*.TM2;yn*tex.bin|Movement Table|*_tbl.bin|Quest file|*.mib|Wii fpack file|*.fpk|" +
                    "Compressed AFS|*.AFS|Uncompressed AFS|DATA.BIN;*.AFS|multitex package|*.tex|AMO model|*.amo;*_amh.bin|PAC archive|*.pac|PZZ recompress|*.bin|Collision Maps|lw0*.bin;lg0*.bin;lwg*.bin|" +
                    "DTX archive|*.dtx|bin unpack|*.bin|jkr decompress|*.tmp|Quest fun|*.mib|Animation Destruction|*_tbl.bin|gather tables|*.69;*.95|meat zones|*.95|psp databin|DATA.BIN|mhp quest dump|*.bin|" +
                    "animation parse|*.aan|mhfo exe decrypt|mhf.exe|memcard decrypt|*.bin|func decrypt|*.bin|memcard encrypt|*.sav|AI script dump|*.95;*.69|model prep|*_amh.bin|patch file|PATCH_*";
                openFileDialog.FilterIndex = 2; //to save me some clicks
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    //Get the path of specified file
                    filename = openFileDialog.FileName;
                    filter = openFileDialog.FilterIndex;
                }
            }
            if (filename == "")
            {
                return;
            }
            Thread loading = new Thread(() => MessageBox.Show("operating..."));
            loading.Start();
            FileBuffer.loadFile(filename);
            curimage = 0;
            if (filter == 1)
            {
                ImageAPX img = new ImageAPX();
                img.Parse();

                displayImage(img.img);
            }
            else if (filter == 2)
            {
                imgs = new ImageCollection();
                imgs.Parse();

                displayImage(imgs.images[0]);
            }
            else if (filter == 3)
            {
                List<byte> decomp = new List<byte>(FileBuffer.bufSize);
                FileBuffer.PzzDecompress(decomp);
                string dir = Directory.GetCurrentDirectory();
                if (!Directory.Exists(dir + "/decompress/"))
                    Directory.CreateDirectory(dir + "/decompress/");
                using (BinaryWriter bw = new BinaryWriter(File.Create(dir + "/decompress/" + Path.GetFileName(filename))))
                {
                    for (int i = 0; i < decomp.Count; i++)
                        bw.Write(decomp[i]);
                }
                Debug.WriteLine("Successfully decompressed!");
            }
            else if (filter == 4)
            {
                ImageTM2 img = new ImageTM2();
                img.Parse();

                displayImage(img.img);
            }
            else if (filter == 5)
            {
                MotionTable tbl = new MotionTable();
                tbl.Parse();
            }
            else if (filter == 6)
            {
                //MHG quest numbering scheme is 00XXX for low rank, 01XXX for high rank, 02XXX for G rank, 03XXX for online events, 10XXX for village, 20XXX for training
                //MH2 adds time of day (d or n) as well as season number, and uses 000XX for basic training, 001XX for advanced training, 002XX for arena training,
                //01XXX for offline, 03XXX for Arena, 05XXX for urgent requests, 06XXX for unlockables? yian garuga's in here
                //10XXX for online hunting exercises, 11XXX for low rank free quests, 12XXX for high rank free quests 
                //15XXX for Official Hunting Tests (online), 20XXX for repel missions, 25XXX for versus matches, lots of others i have yet to map out
                //coverage = new List<byte>(new byte[filebuf.Count]);
                //checkcoverage = true;
                Quest quest = new Quest();
                quest.Parse();

                //load up the map file
                //checkcoverage = false;
                filename = Path.GetDirectoryName(filename);
                if (quest.game != Quest.Game.MH2)
                {
                    filename += "\\map" + (quest.locale == 1 ? "" : quest.locale.ToString()) + ".apx";
                    if (File.Exists(filename))
                    {
                        FileBuffer.loadFile(filename);
                        ImageAPX map = new ImageAPX();
                        map.Parse();
                        displayImage(map.img);
                    }
                }
                else
                {
                    filename = Directory.GetParent(filename).FullName + "\\map" + (quest.locale == 1 ? "" : quest.locale.ToString()) + ".tm2";

                    if (File.Exists(filename))
                    {
                        FileBuffer.loadFile(filename);
                        ImageTM2 map = new ImageTM2();
                        map.Parse();
                        displayImage(map.img);
                    }
                }

                //draw gathering locations?
                List<Pen> pinPens = new List<Pen> { new Pen(new SolidBrush(Color.Red)), new Pen(Color.Transparent), new Pen(Color.Transparent), new Pen(new SolidBrush(Color.Yellow)), new Pen(new SolidBrush(Color.Blue)) };
                Pen monstpen = new Pen(new SolidBrush(Color.Red));
                if (BackgroundImage != null)
                {

                    Graphics g = Graphics.FromImage(BackgroundImage);
                    for (int i = 0; i < quest.pins.Count; i++)
                    {
                        g.DrawEllipse(monstpen, quest.pins[i].x / ConstantsLocales.MapScales[(int)quest.locale][0], quest.pins[i].y / ConstantsLocales.MapScales[(int)quest.locale][1], 3, 3);
                    }
                }
            }
            else if (filter == 7) //wii archive
            {
                FileBuffer.wii = true;
                ArchiveFPK archive = new ArchiveFPK();
                archive.Unpack();
            }
            else if (filter == 8) //compressed afs archive
            {
                ArchiveAFS archive = new ArchiveAFS();
                archive.Unpack(true);
            }
            else if (filter == 9) //uncompressed afs archive
            {
                ArchiveAFS archive = new ArchiveAFS();
                archive.Unpack(false);
            }
            else if (filter == 10) //tex packs
            {
                string magic = FileBuffer.readString(4);
                FileBuffer.filePos -= 4;
                if (magic == "AFS")
                {
                    ArchiveAFS archive = new ArchiveAFS();
                    archive.Unpack(true);
                }
                else if (magic == "MOMO")
                {
                    ArchiveMOMO archive = new ArchiveMOMO();
                    archive.Unpack();
                }
            }
            else if (filter == 11) //.amo 3d models
            {
                bool amh = false;
                if (Path.GetExtension(filename) == ".bin")
                    amh = true;
                Mesh mesh = new Mesh();
                mesh.Parse(amh);
                mesh.Export();
            }
            else if (filter == 12) //.pac archives
            {
                string magic = FileBuffer.readString(4);
                FileBuffer.filePos -= 4;
                if (magic == "ecd" + "\x1A")
                    FileBuffer.ecdDecrypt();
                ArchivePAC archive = new ArchivePAC();
                archive.Unpack();
            }
            else if (filter == 13)
            {
                //here we go
                List<byte> bytes = new List<byte>();
                FileBuffer.PzzCompress(ref bytes);
                string path = Directory.GetCurrentDirectory() + "\\export\\" + Path.GetFileName(filename);
                //filename = path + "/u" + file.ToString("000") + ".mib";
                using (BinaryWriter bw = new BinaryWriter(File.Create(path)))
                    for (int b = 0; b < bytes.Count; b++)
                        bw.Write(bytes[b]);
            }
            else if (filter == 14)
            {
                //wall and ground collision maps
                CollisionMap map = new CollisionMap();
                if (filename.Contains("lwg"))
                {
                    //conjoined table
                    FileBuffer.readInt();
                    uint o1 = FileBuffer.readInt();
                    FileBuffer.readInt();
                    uint o2 = FileBuffer.readInt();
                    FileBuffer.readInt();
                    FileBuffer.filePos = (int)o1;
                    map.ParseWalls();
                    FileBuffer.filePos = (int)o2;
                    map.ParseGrounds();
                }
                else
                {
                    //split table, load walls first
                    string mapnum = Path.GetFileNameWithoutExtension(filename).Substring(2);
                    FileBuffer.loadFile(Path.GetDirectoryName(filename) + "/lw" + mapnum + ".bin");
                    map.ParseWalls();
                    //then load grounds
                    FileBuffer.loadFile(Path.GetDirectoryName(filename) + "/lg" + mapnum + ".bin");
                    map.ParseGrounds();
                }
                map.Export();
            }
            else if (filter == 15)
            {
                ArchiveDTX archive = new ArchiveDTX();
                archive.Unpack();
            }
            else if (filter == 16)
            {

                //used to inspect frontier data bins
                //in mhf-360, 0, 1, 5, and 6 are dds bundles, 2 3 and 4 are jkr'd HLSL bundles, 7's an ogg, and 8's a jpg bundle
                //0 - 5, 11 - 13 in mhf.bin are pngs, 6 and 7 is png bundle, 9 and 10 are jkr'd D3DX8 shaders and ???.. 8 is a quest?!?
                //mytra.bin: 0-3 are jkr (two amo and two ahi), 4 and 5 are png bundles, 6-11 are motion tables!

                //in mhfinf, 0x2C is pointer to table of quest header pointers
                List<List<byte>> dat = new List<List<byte>>();
                FileBuffer.unBundle(ref dat);
                if (dat.Count == 0)
                    return;
                string path = Directory.GetCurrentDirectory() + "\\export\\" + Path.GetFileNameWithoutExtension(filename) + "\\";
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                for (int i = 0; i < dat.Count; i++)
                {
                    using (BinaryWriter bw = new BinaryWriter(File.Create(path + i + ".tmp")))
                    {
                        for (int b = 0; b < dat[i].Count; b++)
                            bw.Write(dat[i][b]);
                    }
                }
            }
            else if (filter == 17)
            {
                FileBuffer.jpkUncompress();
                using (BinaryWriter bw = new BinaryWriter(File.Create(Directory.GetParent(filename).FullName + "\\" + Path.GetFileNameWithoutExtension(filename) + ".bin")))
                    for (int i = 0; i < FileBuffer.bufSize; i++)
                        bw.Write(FileBuffer.readByte());
            }
            else if (filter == 18)
            {
                string path = Directory.GetParent(filename).FullName;
                string[] files = Directory.GetFiles(path, "*.mib");
                foreach (string f in files)
                {
                    //if (File.Exists(path + "\\m" + i.ToString("000") + ".mib"))
                    //{
                    FileBuffer.loadFile(f);
                    //that's about it?
                    Quest q = new Quest();
                    q.Parse();
                    //}
                }
            }
            else if (filter == 19)
            {
                //load the animation file
                //parse our lists
                //copy the data out
                //profit
                //uint animCount = FileBuffer.readInt();
                List<uint> ptrs = new List<uint>();
                List<uint> animCts = new List<uint>();
                List<uint> ptrs2 = new List<uint>();
                List<uint> animCts2 = new List<uint>();
                List<string> names = new List<string> { "Main", "Head", "Tail" };
                while (true)
                {
                    uint val = FileBuffer.readInt();
                    uint val2 = FileBuffer.readInt();
                    if (val == 0)
                        break;
                    ptrs.Add(val2);
                    animCts.Add(val);
                    animCts2.Add(FileBuffer.readInt());
                    ptrs2.Add(FileBuffer.readInt());
                }
                if (!Directory.Exists(Directory.GetCurrentDirectory() + "\\export\\" + Path.GetFileNameWithoutExtension(filename) + "\\"))
                    Directory.CreateDirectory(Directory.GetCurrentDirectory() + "\\export\\" + Path.GetFileNameWithoutExtension(filename) + "\\");
                for (int sub = 0; sub < ptrs.Count; sub++)
                {
                    FileBuffer.filePos = (int)ptrs[sub];
                    for (int p = 0; p < animCts[sub]; p++)
                    {
                        //grab the value
                        uint val = FileBuffer.readInt();
                        if (val == 0xFFFFFFFF)
                            continue;
                        int backup = FileBuffer.filePos;
                        FileBuffer.filePos = (int)val;
                        FileBuffer.readInt();
                        FileBuffer.readInt();
                        uint len = FileBuffer.readInt();
                        FileBuffer.filePos -= 12;
                        using (BinaryWriter bw = new BinaryWriter(File.Create(Directory.GetCurrentDirectory() + "\\export\\" + Path.GetFileNameWithoutExtension(filename) + "\\" + p + "_" + names[sub] + ".aan")))
                        {
                            for (int b = 0; b < len; b++)
                                bw.Write(FileBuffer.readByte());
                        }
                        FileBuffer.filePos = backup;
                    }
                }
                for (int sub = 0; sub < ptrs2.Count; sub++)
                {
                    FileBuffer.filePos = (int)ptrs2[sub];
                    for (int p = 0; p < animCts2[sub]; p++)
                    {
                        //grab the value
                        uint val = FileBuffer.readInt();
                        if (val == 0xFFFFFFFF)
                            continue;
                        int backup = FileBuffer.filePos;
                        FileBuffer.filePos = (int)val;
                        FileBuffer.readInt();
                        FileBuffer.readInt();
                        uint len = FileBuffer.readInt();
                        FileBuffer.filePos -= 12;
                        using (BinaryWriter bw = new BinaryWriter(File.Create(Directory.GetCurrentDirectory() + "\\export\\" + Path.GetFileNameWithoutExtension(filename) + "\\sub_" + p + "_" + names[sub] + ".aan")))
                        {
                            for (int b = 0; b < len; b++)
                                bw.Write(FileBuffer.readByte());
                        }
                        FileBuffer.filePos = backup;
                    }
                }
            }
            else if (filter == 20)
            {
                //export the gathering table!
                //0x1FCB70 for mh1j exe
                //0x191D50 for mhgp... 6D are in the file, but 2E is in the exe
                //exe needs offsets of +100000, then -180 for 1j and -200 for gp
                //game.bin needs -542600
                string f = Path.GetFileName(filename);
                if (f == "SLPM_654.95")
                {
                    //mh1j
                    FileBuffer.filePos = 0x1FCB70;
                    List<uint> ptrs = new List<uint>();
                    while (true)
                    {
                        uint val = FileBuffer.readInt();
                        if (val > 0x40000000)
                            break;
                        ptrs.Add(val);
                    }
                    string path = Directory.GetCurrentDirectory() + "\\export\\g_mh1j";
                    BinaryWriter bw = new BinaryWriter(File.Create(path));
                    for (int p = 0; p < ptrs.Count; p++)
                    {
                        FileBuffer.filePos = (int)(ptrs[p] - 0x100000 + 0x180);
                        //read the line, write it out somewhere
                        while (true)
                        {
                            ushort val1 = FileBuffer.readShort();
                            ushort val2 = FileBuffer.readShort();
                            bw.Write(val1);
                            bw.Write(val2);
                            if (val1 == 0xFFFF)
                                break;
                        }
                        //advance to the next line in the output, for clean parsing
                        while ((bw.BaseStream.Position % 0x10) != 0)
                            bw.Write((byte)0);
                    }
                }
                else if (f == "SLPM_658.69")
                {
                    //mhgp
                    //open up game.bin
                    BinaryReader br = new BinaryReader(File.OpenRead(Path.GetDirectoryName(filename) + "\\extract\\game.bin"));
                    br.BaseStream.Position = 0x191D50;
                    List<uint> ptrs = new List<uint>();
                    while (true)
                    {
                        uint val = br.ReadUInt32();
                        if (val < 0x30000)
                            break;
                        ptrs.Add(val);
                    }
                    string path = Directory.GetCurrentDirectory() + "\\export\\g_mhgp";
                    BinaryWriter bw = new BinaryWriter(File.Create(path));
                    for (int p = 0; p < ptrs.Count; p++)
                    {
                        if (ptrs[p] > 0x542600)
                        {
                            //in the bin
                            br.BaseStream.Position = ptrs[p] - 0x542600;
                            //read the line, write it out somewhere
                            while (true)
                            {
                                ushort val1 = br.ReadUInt16();
                                ushort val2 = br.ReadUInt16();
                                bw.Write(val1);
                                bw.Write(val2);
                                if (val1 == 0xFFFF)
                                    break;
                            }
                        }
                        else
                        {
                            //exe
                            FileBuffer.filePos = (int)ptrs[p] - 0x100000 + 0x200;
                            //read the line, write it out somewhere
                            while (true)
                            {
                                ushort val1 = FileBuffer.readShort();
                                ushort val2 = FileBuffer.readShort();
                                bw.Write(val1);
                                bw.Write(val2);
                                if (val1 == 0xFFFF)
                                    break;
                            }
                        }
                        //advance to the next line in the output, for clean parsing
                        while ((bw.BaseStream.Position % 0x10) != 0)
                            bw.Write((byte)0);
                    }
                }
            }
            else if (filter == 21)
            {
                //go to the master table then parse the entries
                List<int> ptrs = new List<int>();
                FileBuffer.filePos = (0x357580 - 0x100000 + 0x180);
                for (int i = 0; i < 36; i++)
                {
                    ptrs.Add((int)FileBuffer.readInt());
                }
                List<string> mons = new List<string> {"null", "rathian", "fatalis", "kelbi", "mosswine", "bullfango", "yian kut ku", "laoshan lung", "cephadrome", "felyne", "veggie elder", "rathalos", "aptonoth", "genprey", "diablos",
                "khezu", "velociprey", "gravios", "cart", "vespoid", "gypceros", "plesioth", "basarios", "melynx", "hornetaur", "apceros", "monoblos", "velocidrome", "gendrome", "rock", "ioprey", "iodrome", "pugi", "kirin", "cephalos"};
                for (int i = 0; i < 35; i++)
                {
                    FileBuffer.filePos = (ptrs[i] - 0x100000 + 0x180);
                    Debug.WriteLine(mons[i]);
                    for (int p = 0; p < 8; p++)
                    {
                        byte unk = FileBuffer.readByte();
                        byte cut = FileBuffer.readByte();
                        byte blt = FileBuffer.readByte();
                        byte gun = FileBuffer.readByte();
                        byte fire = FileBuffer.readByte();
                        byte water = FileBuffer.readByte();
                        byte thunder = FileBuffer.readByte();
                        byte dragon = FileBuffer.readByte();
                        Debug.WriteLine("unk: " + unk + "% cutting: " + cut + "% blunt: " + blt + "% bullet: " + gun + "% fire: " + fire + "% water: " + water + "% thunder: " + thunder + "% dragon: " + dragon + "%");
                    }
                }
            }
            else if (filter == 22)
            {
                //psp data.bin
                //each int here is a file start??
                //multiply by 2048, scale of 2KB
                List<int> ptrs = new List<int>();
                while (true)
                {
                    uint p = FileBuffer.readInt() * 0x800;
                    ptrs.Add((int)p);
                    if (p == FileBuffer.bufSize)
                        break;
                }
                string path = Path.GetDirectoryName(filename) + "\\extract\\";
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                for (int i = 0; i < ptrs.Count - 1; i++)
                {
                    FileBuffer.filePos = ptrs[i];
                    using (BinaryWriter br = new BinaryWriter(File.Create(path + i + ".bin")))
                        for (int b = 0; b < ptrs[i + 1] - ptrs[i]; b++)
                            br.Write(FileBuffer.readByte());

                }
            }
            else if (filter == 23)
            {
                string path = Directory.GetParent(filename).FullName;
                string[] files = Directory.GetFiles(path, "*.bin");
                foreach (string f in files)
                {
                    //if (File.Exists(path + "\\m" + i.ToString("000") + ".mib"))
                    //{
                    FileBuffer.loadFile(f);
                    //that's about it?
                    FileBuffer.filePos = 0x5C;
                    Debug.WriteLine(FileBuffer.readJISString());
                    //}
                }
            }
            else if (filter == 24)
            {
                MotionTable mt = new MotionTable();
                mt.Parse();
            }
            else if (filter == 25)
            {
                FileBuffer.filePos = 0xBC15E;
                byte[] dc = new byte[0x7A8];
                for (int i = 0; i <= 0x7A4 / 4; i++)
                {
                    uint v = FileBuffer.readInt();
                    v += 0x9a61adf1;
                    v ^= 0x15651e22;
                    dc[i * 4] = (byte)(v & 0xFF);
                    dc[i * 4 + 1] = (byte)(v >> 8);
                    dc[i * 4 + 2] = (byte)(v >> 16);
                    dc[i * 4 + 3] = (byte)(v >> 24);
                }
                //now the second pass!
                for (int i = 0; i <= 0x1B3; i++)
                {
                    uint v = (uint)(dc[0x7A3 - i * 4] + (dc[0x7A3 - i * 4 + 1] << 8) + (dc[0x7A3 - i * 4 + 2] << 16) + (dc[0x7A3 - i * 4 + 3] << 24));
                    v ^= 0x29786713;
                    v -= 0x635ddd50;
                    v ^= 0x6445cb49;
                    dc[0x7A3 - i * 4] = (byte)(v & 0xFF);
                    dc[0x7A3 - i * 4 + 1] = (byte)(v >> 8);
                    dc[0x7A3 - i * 4 + 2] = (byte)(v >> 16);
                    dc[0x7A3 - i * 4 + 3] = (byte)(v >> 24);
                }
                //and the third pass!
                for (int i = 0; i <= 0x5cc / 4; i++)
                {
                    uint v = (uint)(dc[0x7A3 - i * 4] + (dc[0x7A3 - i * 4 + 1] << 8) + (dc[0x7A3 - i * 4 + 2] << 16) + (dc[0x7A3 - i * 4 + 3] << 24));
                    v ^= 0x04423016;
                    v -= 0x41d95d97;
                    v -= 0x7ddb0b84;
                    dc[0x7A3 - i * 4] = (byte)(v & 0xFF);
                    dc[0x7A3 - i * 4 + 1] = (byte)(v >> 8);
                    dc[0x7A3 - i * 4 + 2] = (byte)(v >> 16);
                    dc[0x7A3 - i * 4 + 3] = (byte)(v >> 24);
                }
                //fourth pass!
                for (int i = 0; i <= 0x144; i++)
                {
                    uint v = (uint)(dc[0x7A4 - i * 4] + (dc[0x7A4 - i * 4 + 1] << 8) + (dc[0x7A4 - i * 4 + 2] << 16) + (dc[0x7A4 - i * 4 + 3] << 24));
                    v += 0x36af1f7d;
                    v += 0x3bd6ae72;
                    v ^= 0x41bb32c3;
                    dc[0x7A4 - i * 4] = (byte)(v & 0xFF);
                    dc[0x7A4 - i * 4 + 1] = (byte)(v >> 8);
                    dc[0x7A4 - i * 4 + 2] = (byte)(v >> 16);
                    dc[0x7A4 - i * 4 + 3] = (byte)(v >> 24);
                }

                //string thing
                /*{
                    uint key = 0x9c3b248e;
                    FileBuffer.filePos = 0x16db0;//todo, fill this
                    while (true)
                    {
                        byte v = FileBuffer.readByte();
                        if (v == 0)
                            break;
                        key ^= v;
                        for(int i = 0; i < 8; i++)
                        {
                            bool f = (key & 1) == 1;
                            key >>= 1;
                            if (f) key ^= 0xc1a7f39a;
                            Debug.WriteLine(key.ToString("X8"));
                        }
                    }
                    if (key == 0xB72551A7)
                        Debug.WriteLine("MATCH");

                }*/

                //decompression of data at 0x4e3bd4 (bcbd4)
                {
                    FileBuffer.fexedecompress();

                }


                //write results
                /*string path = Directory.GetCurrentDirectory() + "\\export\\";
                using (BinaryWriter bw = new BinaryWriter(File.Create(path + "exe4.bin")))
                {
                    for (int i = 0; i < dc.Length; i++)
                        bw.Write(dc[i]);
                }

                FileBuffer.filePos = 0x198;*/
            }
            else if (filter == 26)
            {
                const ushort majic = 0x001; //mh2
                //const ushort majic = 0x100; //mh1 or mhg
                if (FileBuffer.readShort() != majic)
                    Debug.WriteLine("not valid");
                else
                {
                    ushort v1 = FileBuffer.readShort();
                    ushort csum = FileBuffer.readShort();
                    //FileBuffer.readShort();
                    FileBuffer.filePos = 0x40; //mh2
                    //FileBuffer.filePos = 0x08; //mh1 or mhg

                    ushort v2 = 0;
                    ushort sum = 0;
                    string path = Directory.GetCurrentDirectory() + "\\export\\";
                    using (BinaryWriter bw = new BinaryWriter(File.Create(path + "memdecrypted_mh2patch.sav")))

                        for (int i = 0; FileBuffer.filePos != FileBuffer.bufSize; i++)
                        {
                            v2 = FileBuffer.readShort();
                            //if (FileBuffer.filePos == 0xc002)
                            //    v1 = v2;
                            v2 ^= v1;
                            sum += v2;
                            bw.Write(v2);
                            if (v1 == 0)
                                v1 = 1;
                            v1 = (ushort)((v1 * 0xb0) % 0xff53);
                        }
                    Debug.WriteLine(csum.ToString("X4"));
                    Debug.WriteLine(sum.ToString("X4"));
                }
            }
            else if (filter == 27)
            {

                uint a1 = 0xa06994;
                uint a2 = 0x798318ae;
                uint a3 = 0x79831896;
                uint a4 = 0x1404cd56;

                uint type = 2;

                List<uint> keys = new List<uint> { 
                    0x1018e096, 0x1018E394, 0x1018E490, 0x1018E55E, 0x1018E61C, 0x1018E6D6, 0x1018E792, 0x1018E850, 0x1018EA48, 
                    0x1018EB16, 0x1018EBDE, 0x1018ED08, 0x1018EDCE, 0x1018EE8A, 0x110FBF22, 0x110FC088, 0x110FC162, 0x110FC224,
                    0x110FC2E2, 0
                };
                List<uint> inits = new List<uint>
                {
                    0x98, 0x1A8, 0x378, 0x54C, 0x7F8, 0x9F4, 0xb14, 0xccc, 0x1628,
                    0x17d4, 0x1a2c, 0x1b58, 0x221c, 0x271c, 0x7c, 0x1e4, 0x5ac, 0x77c,
                    0x944, 0
                };

                List<List<uint>> offs = new List<List<uint>>();
                offs.Add(new List<uint> { 0xa4 });
                offs.Add(new List<uint> { 0x1b8, 0x220 });
                offs.Add(new List<uint> { 0x388, 0x3F0 });
                offs.Add(new List<uint> { 0x54c, 0x56c, 0x608, 0x680 });
                offs.Add(new List<uint> { 0x80c, 0x814, 0x834, 0x85C, 0x88c, 0x8B4, 0x8eC });
                offs.Add(new List<uint> { 0x9F4, 0x9f8, 0xa08, 0xa18, 0xa28 });
                offs.Add(new List<uint> { 0xb14, 0xb18, 0xb2c, 0xb3c });
                offs.Add(new List<uint> { 0xccc, 0xcd0, 0xd08, 0xd2c, 0xd54, 0xf10, 0xf30, 0xfb0, 0x1030, 0x10a8, 0x1120, 0x1198, 0x1210 });
                offs.Add(new List<uint> { 0x1628, 0x1630, 0x1640, 0x1650, 0x1670, 0x167c, 0x1690, 0x16a8, 0x16b8, 0x16d0 });
                offs.Add(new List<uint> { 0x17d4, 0x17d8, 0x17e4, 0x17f8, 0x1808, 0x1810, 0x180C, 0x1818, 0x182c, 0x186c, 0x1878, 0x17f0, 0x187c, 0x188c, 0x1894, 0x189c, 0x18b8, 0x18e8, 0x193c });
                offs.Add(new List<uint> { 0x1a54, 0x1a58, 0x1a5c });
                offs.Add(new List<uint> { 0x1b58, 0x1b5c, 0x1b8c,  0x1c08});
                offs.Add(new List<uint> { 0x2240, 0x221c, 0x2248, 0x2258, 0x22ac, 0x22d4, 0x22dc, 0x22d8, 0x22e0, 0x2310, 0x2318, 0x2324, 0x232c, 0x2340, 0x2344, 0x234c, 0x2368, 0x238c, 0x23a0, 0x23b0, 0x23c0, 0x23cc, 0x23f8, 0x2400, 0x2444, 0x2450, 0x24d0, 0x2548, 0x25c0 });
                offs.Add(new List<uint> { 0x2744, 0x2748, 0x274c, 0x276c, 0x277c, 0x2788, 0x2790 });
                offs.Add(new List<uint> { 0x80, 0x9c, 0xb0, 0xd0 });
                offs.Add(new List<uint> { 0x238, 0x264, 0x280, 0x324, 0x338, 0x36c, 0x3b8, 0x430, 0x48c });
                offs.Add(new List<uint> { 0x5ac, 0x5b4, 0x5b0, 0x5b8, 0x5c4, 0x5e0, 0x618 });
                offs.Add(new List<uint> { 0x77c, 0x794, 0x7ac, 0x7c0, 0x7d8, 0x7ec, 0x800, 0x820 });
                offs.Add(new List<uint> { 0x944, 0x970, 0x990, 0x9a4, 0x9b4, 0x9bc, 0xa0c, 0xa10, 0xa3c, 0xa44, 0xa50, 0xa78, 0xb48, 0xbc0, 0xc38, 0xcb8, 0xd30 });
                offs.Add(new List<uint>());

                if(type == 2)
                {
                    keys = new List<uint> //key values to choose which table to use, @a2f8b0
                    {
                        0x1404cd56, 0x1404d024, 0x1404d1d0, 0x1404d370, 0x1404d512, 0x1404d6b2, 0x1404d854, 0x1404d9f8, 0x1404db9c, 
                        0x1404dd3e, 0x1404dee6, 0x1404e090, 0x1404e242, 0x1404e3ee, 0x1404e598, 0x1404e740, 0x1404e8f2, 0x1404eaa6, 
                        0
                    };

                    inits = new List<uint> //values at the start of each table, starting @a2f934, these are offsets from start of segment's code (a06280)
                    {
                        0x714, 0x898, 0xa2c, 0xb9c, 0xcb0, 0xdc0, 0xf48, 0x11b0, 0x1294, 0x1380, 0x16a8, 0x17c8, 0x18d4, 0x1ed8, 0x20f0, 0x2208, 0x23b8, 0x2660, 0
                    };

                    offs = new List<List<uint>>(); //the tables themselves, following on from each init val above
                    offs.Add(new List<uint> { 0x73c, 0x740, 0x744 });
                    offs.Add(new List<uint> { 0x8e4, 0x8e8, 0x8f8, 0x944, 0x948, 0x94c, 0x8a0 });
                    offs.Add(new List<uint> { 0xa5c, 0xa60, 0xa64 });
                    offs.Add(new List<uint> { 0xbcc, 0xbd0, 0xbd4 });
                    offs.Add(new List<uint> { 0xcdc, 0xce0, 0xce4 });
                    offs.Add(new List<uint> { 0xdec, 0xdf0, 0xdf4 });
                    offs.Add(new List<uint> { 0xfd8, 0xfd0, 0xff8, 0xf48, 0xf4c, 0xf8c, 0xf90, 0xfb8, 0xfc0, 0xfcc, 0xfd4, 0xfdc, 0xfe4, 0x103c, 0x1040, 0x1068, 0x1070, 0x10bc, 0x10c0, 0x10e8, 0xf54 });
                    offs.Add(new List<uint> { 0x11dc, 0x11e0, 0x11e4 });
                    offs.Add(new List<uint> { 0x12c4, 0x12c8, 0x12cc });
                    offs.Add(new List<uint> { 0x13b4, 0x13b8, 0x13bc });
                    offs.Add(new List<uint> { 0x16d4, 0x16d8, 0x16dc });
                    offs.Add(new List<uint> { 0x17fc, 0x1800, 0x1804 });
                    offs.Add(new List<uint> { 0x1904, 0x1908, 0x190c });
                    offs.Add(new List<uint> { 0x1edc, 0x1ee0, 0x1ee4, 0x1ee8, 0x1f24, 0x1f28, 0x1f2c, 0x1f34, 0x1f40, 0x1f74, 0x1f78, 0x1f7c, 0x1f90, 0x1f94, 0x1fdc, 0x1fe0, 0x1fe4, 0x1ed8, 0x1f30 });
                    offs.Add(new List<uint> { 0x2114, 0x2118, 0x211c });
                    offs.Add(new List<uint> { 0x2208, 0x220c, 0x2210, 0x2214, 0x2218, 0x2254, 0x2258, 0x225c, 0x2264, 0x2268, 0x2274, 0x22ac, 0x22b0, 0x22b4, 0x22c8 });
                    offs.Add(new List<uint> { 0x23b8, 0x2418, 0x23bc, 0x23c0, 0x23c4, 0x23c8, 0x240c, 0x2410, 0x2414, 0x241c, 0x2428, 0x245c, 0x2460, 0x2464, 0x247c, 0x2480, 0x24c4, 0x24c8, 0x24cc, 0x24d8, 0x24e0, 0x2524, 0x2528, 0x252c });
                    offs.Add(new List<uint> { 0x2684, 0x2688, 0x268c });
                    offs.Add(new List<uint>());
                }
                else if (type == 3)
                {
                    keys = new List<uint>
                    {
                        0x2F065C30, 0x2F065E88, 0x2f065f10, 0x2f065f88, 0x2f066006, 0x2f066080, 0x2f066100, 0x2F06617E, 0x2F066290, 0x2F066316, 
                        0x2F066396, 0x2F066416, 0x2F0665A0, 0x2F066624, 0x2F0666A8, 0x2F1AC4D2, 0x2F1AC6A0, 0x2F1AC902, 0x2F1AC9CE, 0x2F1ACA7A, 
                        0x2F1ACB12, 0x2F1ACB98, 0x2F1ACC1E, 0x2F1ACCD0, 0x2F1ACDC2, 0x2F1ACE48, 0x2F1ACED6, 0x2F1ACF66, 0x2F1AD094, 0x2F1AD12C,
                        0
                    };

                    inits = new List<uint>
                    {
                        0x124, 0x2b4, 0x464, 0x810, 0xa90, 0xc48, 0xe08, 0xfdc, 0x1434, 0x187c, 0x19f8, 0x1bf4, 0x1ed0, 0x2014, 0x2114, 
                        0x114, 0x384, 0x860, 0xab0, 0xba0, 0xddc, 0xfb4, 0x13ec, 0x158c, 0x1950, 0x1c0c, 0x20f0, 0x254c, 0x2d60, 0x3204, 
                        0
                    };

                    offs = new List<List<uint>>();
                    offs.Add(new List<uint> { 0x144, 0x14c, 0x150, 0x1b0 });
                    offs.Add(new List<uint> { 0x2f4, 0x2fc, 0x300, 0x340 });
                    offs.Add(new List<uint> { 0x464, 0x46c, 0x478, 0x4d8, 0x5a0, 0x5a8, 0x5ac, 0x610, 0x688, 0x708 });
                    offs.Add(new List<uint> { 0x838, 0x8b0, 0x908, 0x910, 0x918 });
                    offs.Add(new List<uint> { 0xa90, 0xac4, 0xacc, 0xad0, 0xb38 });
                    offs.Add(new List<uint> { 0xc58, 0xc6c, 0xc74, 0xc78, 0xd00 });
                    offs.Add(new List<uint> { 0xe34, 0xe70, 0xec8, 0xecc, 0xed4 });
                    offs.Add(new List<uint> { 0xffc, 0x1004, 0x1008, 0x1060, 0x1158, 0x11ac, 0x11b4, 0x11b8, 0x1218 });
                    offs.Add(new List<uint> { 0x1440, 0x1458, 0x1460, 0x1478, 0x14b0, 0x1528, 0x1530, 0x1538, 0x1598, 0x1610, 0x1684, 0x168c, 0x1690, 0x16f0, 0x1768 });
                    offs.Add(new List<uint> { 0x1884, 0x18d8, 0x192c, 0x1934, 0x1938 });
                    offs.Add(new List<uint> { 0x1a00, 0x1a38, 0x1ab0, 0x1b24, 0x1b2c, 0x1b34 });
                    offs.Add(new List<uint> { 0x1c40, 0x1cc0, 0x1d24 });
                    offs.Add(new List<uint> { 0x1edc, 0x1f10 });
                    offs.Add(new List<uint> { 0x2014, 0x201c, 0x2034 });
                    offs.Add(new List<uint> { 0x2124, 0x2148, 0x2150, 0x2158, 0x2168, 0x21e8, 0x2260, 0x22e0, 0x2358, 0x23d8, 0x2458, 0x24d8, 0x2558, 0x25d8, 0x2658, 0x26d8, 0x2750, 0x27d0, 0x2848 });
                    offs.Add(new List<uint> { 0x13c, 0x144, 0x148, 0x1a0, 0x234, 0x23c, 0x240 });
                    offs.Add(new List<uint> { 0x3a0, 0x3a8, 0x3ac, 0x470, 0x4f0, 0x568 });
                    offs.Add(new List<uint> { 0x888, 0x908 });
                    offs.Add(new List<uint> { 0xab4, 0xab8, 0xac0, 0xac8 });
                    offs.Add(new List<uint> { 0xcd8 });
                    offs.Add(new List<uint> { 0xde0, 0xe1c, 0xe24, 0xe58, 0xeb0 });
                    offs.Add(new List<uint> { 0x1034, 0x1070, 0x1188, 0x11c8, 0x121c, 0x1224, 0x1228, 0x1280 });
                    offs.Add(new List<uint> { 0x13f8, 0x142c, 0x1434, 0x1438, 0x1488 });
                    offs.Add(new List<uint> { 0x1598, 0x15ac, 0x15b4, 0x15b8, 0x1610, 0x16c8, 0x1708, 0x175c, 0x1764, 0x1768 });
                    offs.Add(new List<uint> { 0x19a0, 0x1a08, 0x1a48, 0x1ac0, 0x1b18, 0x1b24, 0x1b28 });
                    offs.Add(new List<uint> { 0x1c90, 0x1d08, 0x1d88, 0x1e00, 0x1e38, 0x1ec0 });
                    offs.Add(new List<uint> { 0x2108, 0x2140, 0x21ac, 0x21c0, 0x21c8, 0x21cc, 0x2200, 0x2210, 0x223c, 0x2270 });
                    offs.Add(new List<uint> { 0x25e4, 0x261c });
                    offs.Add(new List<uint> { 0x2d6c, 0x2d64, 0x2d70 });
                    offs.Add(new List<uint> { 0x3210, 0x3218, 0x321c });
                    offs.Add(new List<uint>());
                }


                int keyIDX = keys.IndexOf(a4);
                uint length = a2 ^ a3;
                uint sequence = a3 & 0x77777777;
                uint position = a1;

                if (keyIDX == -1)
                    throw new System.Exception("wrong keyset?");

                uint seqLength = (sequence >> 0x1C) & 0x0F;
                if (seqLength == 0)
                    seqLength = 5;
                for(int s = 0; s < seqLength; s++)
                {
                    uint func = (sequence >> (0x4 * s)) & 0x0F;
                    for(int op = 0; op*4 < length; op++)
                    {
                        if(!offs[keyIDX].Contains((uint)(op*4 + inits[keyIDX])))
                        {
                            uint v;
                            switch(func)
                            {
                                case 0:
                                    FileBuffer.filePos = op * 4;
                                    v = FileBuffer.readInt();
                                    v ^= 0x9F8Ed17;
                                    FileBuffer.buffers[0][op * 4 + 3] = (byte)((v >> 24) & 0xFF);
                                    FileBuffer.buffers[0][op * 4 + 2] = (byte)((v >> 16) & 0xFF);
                                    FileBuffer.buffers[0][op * 4 + 1] = (byte)((v >> 8) & 0xFF);
                                    FileBuffer.buffers[0][op * 4] = (byte)((v >> 0) & 0xFF);
                                    break;
                                case 1:
                                    FileBuffer.filePos = op * 4;
                                    v = FileBuffer.readInt();
                                    v ^= 0xa95128c3;
                                    FileBuffer.buffers[0][op * 4 + 3] = (byte)((v >> 24) & 0xFF);
                                    FileBuffer.buffers[0][op * 4 + 2] = (byte)((v >> 16) & 0xFF);
                                    FileBuffer.buffers[0][op * 4 + 1] = (byte)((v >> 8) & 0xFF);
                                    FileBuffer.buffers[0][op * 4] = (byte)((v >> 0) & 0xFF);
                                    break;
                                case 2:
                                    FileBuffer.filePos = op * 4;
                                    v = FileBuffer.readInt();
                                    v = ((v >> 0xd) & 0x7FFFF) | (v << 0x13);
                                    FileBuffer.buffers[0][op * 4 + 3] = (byte)((v >> 24) & 0xFF);
                                    FileBuffer.buffers[0][op * 4 + 2] = (byte)((v >> 16) & 0xFF);
                                    FileBuffer.buffers[0][op * 4 + 1] = (byte)((v >> 8) & 0xFF);
                                    FileBuffer.buffers[0][op * 4] = (byte)((v >> 0) & 0xFF);
                                    break;
                                case 3:
                                    FileBuffer.filePos = op * 4;
                                    v = FileBuffer.readInt();
                                    v = ((v >> 0x8) & 0xFFFFFF) | (v << 0x18);
                                    FileBuffer.buffers[0][op * 4 + 3] = (byte)((v >> 24) & 0xFF);
                                    FileBuffer.buffers[0][op * 4 + 2] = (byte)((v >> 16) & 0xFF);
                                    FileBuffer.buffers[0][op * 4 + 1] = (byte)((v >> 8) & 0xFF);
                                    FileBuffer.buffers[0][op * 4] = (byte)((v >> 0) & 0xFF);
                                    break;
                                case 4:
                                    FileBuffer.filePos = op * 4;
                                    v = FileBuffer.readInt();
                                    v = ((v & 0xFF) << 8) | ((v >> 0x8) & 0xFF0000) | ((v >> 0x10) & 0xFF) | ((v & 0xFF00) << 0x10);
                                    FileBuffer.buffers[0][op * 4 + 3] = (byte)((v >> 24) & 0xFF);
                                    FileBuffer.buffers[0][op * 4 + 2] = (byte)((v >> 16) & 0xFF);
                                    FileBuffer.buffers[0][op * 4 + 1] = (byte)((v >> 8) & 0xFF);
                                    FileBuffer.buffers[0][op * 4] = (byte)((v >> 0) & 0xFF);
                                    break;
                                case 5:
                                    FileBuffer.filePos = op * 4;
                                    v = FileBuffer.readInt();
                                    v = (((v ^ 0x9f8ed17) >> 0xd) & 0x7FFFF) | ((v ^ 0x9f8ed17) << 0x13);
                                    FileBuffer.buffers[0][op * 4 + 3] = (byte)((v >> 24) & 0xFF);
                                    FileBuffer.buffers[0][op * 4 + 2] = (byte)((v >> 16) & 0xFF);
                                    FileBuffer.buffers[0][op * 4 + 1] = (byte)((v >> 8) & 0xFF);
                                    FileBuffer.buffers[0][op * 4] = (byte)((v >> 0) & 0xFF);
                                    break;
                                case 6:
                                    FileBuffer.filePos = op * 4;
                                    v = FileBuffer.readInt();
                                    v = ((v & 0xFF) << 8) | ((v >> 0x8) & 0xFF0000) | ((v >> 0x10) & 0xFF) | ((v & 0xFF00) << 0x10);
                                    v ^= 0xa95128c3;
                                    v = ((v >> 0x8) & 0xFFFFFF) | (v << 0x18);
                                    FileBuffer.buffers[0][op * 4 + 3] = (byte)((v >> 24) & 0xFF);
                                    FileBuffer.buffers[0][op * 4 + 2] = (byte)((v >> 16) & 0xFF);
                                    FileBuffer.buffers[0][op * 4 + 1] = (byte)((v >> 8) & 0xFF);
                                    FileBuffer.buffers[0][op * 4] = (byte)((v >> 0) & 0xFF);
                                    break;
                                case 7:
                                    FileBuffer.filePos = op * 4;
                                    v = FileBuffer.readInt();
                                    v ^= 0x9F8Ed17;
                                    v = ((v >> 0x8) & 0xFFFFFF) | (v << 0x18);
                                    v = ((v >> 0xd) & 0x7FFFF) | (v << 0x13);
                                    v = ((v & 0xFF) << 8) | ((v >> 0x8) & 0xFF0000) | ((v >> 0x10) & 0xFF) | ((v & 0xFF00) << 0x10);
                                    FileBuffer.buffers[0][op * 4 + 3] = (byte)((v >> 24) & 0xFF);
                                    FileBuffer.buffers[0][op * 4 + 2] = (byte)((v >> 16) & 0xFF);
                                    FileBuffer.buffers[0][op * 4 + 1] = (byte)((v >> 8) & 0xFF);
                                    FileBuffer.buffers[0][op * 4] = (byte)((v >> 0) & 0xFF);
                                    break;
                            }
                        }
                    }
                }
                string path = Directory.GetCurrentDirectory() + "\\export\\";
                using (BinaryWriter bw = new BinaryWriter(File.Create(path + "decryptedfunc.bin")))
                {
                    FileBuffer.filePos = 0;
                    for (int b = 0; b < FileBuffer.bufSize; b++)
                        bw.Write(FileBuffer.readByte());
                }
            }
            else if (filter == 28)
            {
                //const ushort majic = 0x001; //mh2
                const ushort majic = 0x100; //mh1 or mhg
                //if (FileBuffer.readShort() != majic)
                //    Debug.WriteLine("not valid");
                //else
                {
                    ushort v1 = 0x6b38;
                    ushort csum = 0;
                    //FileBuffer.readShort();
                    //FileBuffer.filePos = 0x40; //mh2
                    FileBuffer.filePos = 0x00; //mh1 or mhg

                    ushort v2 = 0;
                    ushort sum = 0;
                    string path = Directory.GetCurrentDirectory() + "\\export\\";
                    using (BinaryWriter bw = new BinaryWriter(File.Create(path + "memencrypted.bin")))
                    {
                        bw.Write(majic);
                        bw.Write(v1);
                        bw.Write((ushort)0);
                        bw.Write((ushort)0x5963);
                        for (int i = 0; FileBuffer.filePos != FileBuffer.bufSize; i++)
                        {
                            v2 = FileBuffer.readShort();
                            //if (FileBuffer.filePos == 0xc002)
                            //    v1 = v2;
                            sum += v2;
                            v2 ^= v1;
                            bw.Write(v2);
                            if (v1 == 0)
                                v1 = 1;
                            v1 = (ushort)((v1 * 0xb0) % 0xff53);
                        }
                        bw.BaseStream.Position = 0x04;
                        bw.Write(sum);
                    }
                    Debug.WriteLine(csum.ToString("X4"));
                    Debug.WriteLine(sum.ToString("X4"));
                }
            }
            else if (filter == 29)
            {
                //dump the AI scripts
                BehaviourScript script = new BehaviourScript();


                
            }

            else if(filter == 30)
            {
                //export all data related to a model! to prep for blender import
                if (Path.GetFileName(filename).StartsWith("em"))
                {
                    //monster!
                    //copy the amh!
                    string[] group = Directory.GetFiles(Path.GetDirectoryName(filename), "em??_amh.bin");
                    for (int mod = 0; mod < group.Length; mod++)
                    {
                        FileBuffer.loadFile(group[mod]);

                        string path = Directory.GetCurrentDirectory() + "\\export\\" + "\\m_";
                        string name = Path.GetFileName(group[mod]).Substring(0, 4);
                        if (!Directory.Exists(path + name + "\\"))
                            Directory.CreateDirectory(path + name + "\\");
                        File.Copy(group[mod], path + name + "\\" + Path.GetFileName(group[mod]), true);
                        File.Copy(Path.GetDirectoryName(group[mod]) + "\\" + name + "_tbl.bin", path + name + "\\" + name + "_tbl.bin", true);
                        ImageCollection ic = new ImageCollection();
                        FileBuffer.loadFile(Path.GetDirectoryName(group[mod]) + "\\" + name + "_tex.bin");
                        ic.Parse();
                        for (int img = 0; img < ic.images.Count; img++)
                            ic.images[img].Save(path + name + "\\" + img.ToString() + ".png");
                    }
                }
                else if(Path.GetFileName(filename).StartsWith("m_") || Path.GetFileName(filename).StartsWith("f_"))
                {
                    //armor!
                }
                else if(Path.GetFileName(filename).StartsWith("st"))
                {
                    //stage!
                }
                else if(Path.GetFileName(filename).StartsWith("we"))
                {
                    //weapon!
                }
            }
            else if (filter == 31)
            {
                //print out patch information
                string game = "";
                for(int i = 0; i < 8; i++) 
                    game += (char)FileBuffer.readByte();
                Debug.WriteLine(game);
                uint pnum = FileBuffer.readInt();
                Debug.WriteLine("num of patches: " + pnum);
                for(int p = 0; p < pnum; p++)
                {
                    Debug.WriteLine("patch " + p);
                    uint slot = FileBuffer.readInt();
                    Debug.WriteLine(" slot: " + slot.ToString("X"));
                    uint ptr = FileBuffer.readInt();
                    Debug.WriteLine(" address: " + ptr.ToString("X"));
                    uint dsize = FileBuffer.readInt();
                    if((dsize & 0x80000000) != 0)
                    {
                        dsize &= 0x7FFFFFFF;
                        Debug.WriteLine(" patch size: " + dsize + " (pointer)");
                        uint data = FileBuffer.readInt();
                        Debug.WriteLine(" data pointer: " + data.ToString("X"));
                        int bmark = FileBuffer.filePos;
                        FileBuffer.filePos = (int)(data - 0xab6780);
                        Debug.Write(" data: ");
                        for (int b = 0; b < dsize; b++)
                            Debug.Write(FileBuffer.readByte().ToString("X2") + " ");
                        Debug.Write("\n");
                        FileBuffer.filePos = bmark;
                    }
                    else
                    {
                        Debug.WriteLine(" patch size: " + dsize);
                        Debug.Write(" data: ");
                        for (int b = 0; b < dsize; b++)
                            Debug.Write(FileBuffer.readByte().ToString("X2") + " ");
                        Debug.Write("\n");
                    }
                }
                Debug.WriteLine("done reading patch! addr " + FileBuffer.filePos.ToString("X"));
            }
            //loading.Abort();
            Text = Path.GetFileName(filename);

            Visible = true; //needed for some reason
            Activate();
        }

        void displayImage(Bitmap img)
        {
            ClientSize = new Size(img.Width, img.Height);
            BackgroundImage = img;
            Invalidate();
            Update();
        }
        
        
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (imgs != null && imgs.images.Count > 0)
            {
                //capture left arrow key
                if (keyData == Keys.Left)
                {
                    curimage = ((curimage + ((int)imgs.images.Count - 1)) % (int)imgs.images.Count);
                    displayImage(imgs.images[curimage]);
                    return true;
                }
                //capture right arrow key
                if (keyData == Keys.Right)
                {
                    curimage = ((curimage+1) % (int)imgs.images.Count);
                    displayImage(imgs.images[curimage]);
                    return true;
                }
            }
            if(keyData == Keys.Enter)
            {
                string dir = Directory.GetCurrentDirectory();
                if (!Directory.Exists(dir + "/export/"))
                    Directory.CreateDirectory(dir + "/export/");
                BackgroundImage.Save(dir + "/export/" + Path.GetFileName(FileBuffer.filename) + "_" + curimage + ".png");
                Debug.WriteLine("Saved image!");
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        
    }
}
