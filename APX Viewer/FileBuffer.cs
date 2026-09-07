using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace APX_Viewer
{
    static class FileBuffer
    {
        public static List<List<byte>> buffers;
        private static List<int> _filePos;
        private static int _curBuf;
        private static int curBuf { get { return _curBuf; } set { _curBuf = value; _filePos[_curBuf] = 0; } }
        public static int filePos { get { return _filePos[curBuf]; } set { _filePos[curBuf] = value; } }
        public static int bufSize { get { return buffers[curBuf].Count; } }

        public static bool wii = false;
        private static bool huff = false;
        public static string filename = "";

        static public List<byte> coverage;
        static public bool checkcoverage = false;

        public static void init(bool w)
        {
            //load up two buffers
            _filePos = new List<int>();
            _filePos.Add(0);
            _filePos.Add(0);
            buffers = new List<List<byte>>();
            buffers.Add(new List<byte>());
            buffers.Add(new List<byte>());
            wii = w;

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); //makes Shift-JIS encoding available
        }

        public static void loadFile(string file)
        {
            loadFile(file, curBuf);
        }

        public static void loadFile(string file, int buffer)
        {
            filename = file;
            curBuf = buffer;
            _filePos[curBuf] = 0;
            Debug.WriteLine(filename);
            //load file into buffer real quick
            using (BinaryReader br = new BinaryReader(File.OpenRead(filename)))
            {
                buffers[curBuf] = new List<byte>((int)br.BaseStream.Length);
                while (br.BaseStream.Position < br.BaseStream.Length)
                    buffers[curBuf].Add(br.ReadByte());
            }
            //Debug.WriteLine("file loaded");
        }

        public static void setBuffer(int buf)
        {
            curBuf = buf;
        }

        public static string readString(int length)
        {
            string val = "";
            for (int l = 0; l < length; l++)
            {
                byte letter = readByte();
                if (letter > 0x10)
                    val += (char)letter;
            }
            return val;
        }

        public static string readString()
        {
            string val = "";
            while(true)
            {
                byte letter = readByte();
                if (letter == 0)
                    break;
                val += (char)letter;
            }
            return val;
        }

        public static string readJISString()
        {
            string val = "";
            List<byte> bytes = new List<byte>();
            while(true)
            {
                byte letter = readByte();
                if (letter != 0)
                    bytes.Add(letter);
                else
                    break;
            }
            Encoding jis = Encoding.GetEncoding(932);
            val = jis.GetString(bytes.ToArray());
            return val;
        }

        public static ushort readShort()
        {
            filePos += 2;
            if (checkcoverage)
            {
                coverage[filePos - 2]++;
                coverage[filePos - 1]++;
            }
            if (wii)
                return (ushort)((buffers[curBuf][filePos - 1]) | (buffers[curBuf][filePos - 2] << 8));
            return (ushort)((buffers[curBuf][filePos - 2]) | (buffers[curBuf][filePos - 1] << 8));
        }

        public static uint readInt()
        {
            filePos += 4;
            if (checkcoverage)
            {
                coverage[filePos - 4]++;
                coverage[filePos - 3]++;
                coverage[filePos - 2]++;
                coverage[filePos - 1]++;
            }
            if (wii)
                return (uint)((buffers[curBuf][filePos - 1]) | ((buffers[curBuf][filePos - 2] << 8) & 0xFF00) | ((buffers[curBuf][filePos - 3] << 16) & 0xFF0000) | (buffers[curBuf][filePos - 4] << 24));
            return (uint)((buffers[curBuf][filePos - 4]) | ((buffers[curBuf][filePos - 3] << 8) & 0xFF00) | ((buffers[curBuf][filePos - 2] << 16) & 0xFF0000) | (buffers[curBuf][filePos - 1] << 24));
        }

        public static byte readByte()
        {
            filePos++;
            if (checkcoverage)
                coverage[filePos - 1]++;
            return buffers[curBuf][filePos - 1];
        }

        public static float readFloat()
        {
            filePos += 4;
            if (checkcoverage)
            {
                coverage[filePos - 4]++;
                coverage[filePos - 3]++;
                coverage[filePos - 2]++;
                coverage[filePos - 1]++;
            }
            return BitConverter.ToSingle(new byte[] { buffers[curBuf][filePos - 4], buffers[curBuf][filePos - 3], buffers[curBuf][filePos - 2], buffers[curBuf][filePos - 1] }, 0);
        }


        public static void unBundle(ref List<List<byte>> data)
        {
            uint magic = readInt();
            filePos -= 4;
            if (magic == 0x1A646365)
            {
                ecdDecrypt();
            }
            magic = readInt();
            filePos -= 4;
            if (magic == 0x1A524B4A)
            {
                jpkUncompress();
            }
            magic = readInt();
            filePos -= 4;
            if (magic == 0x1A66686D || magic == 0x1A666E69 || magic == 0x1A636170)
            {
                string suf = "";
                if (magic == 0x1A66686D)
                    suf = "dat"; //some other string data, maybe other data
                else if (magic == 0x1A666E69)
                    suf = "inf"; //quest info string tables
                else if (magic == 0x1A636170)
                    suf = "pac"; //npc dialogue tables
                if (!Directory.Exists(Directory.GetCurrentDirectory() + "\\decompress"))
                    Directory.CreateDirectory(Directory.GetCurrentDirectory() + "\\decompress");
                using (BinaryWriter bw = new BinaryWriter(File.Create(Directory.GetCurrentDirectory() + "\\decompress\\mhf" + suf + ".bin")))
                    for (int i = 0; i < bufSize; i++)
                        bw.Write(readByte());
                if (magic == 0x1A666E69)
                {
                    filePos = 0x14;
                    uint ptr = readInt();
                    List<ushort> lengths = new List<ushort>();
                    List<uint> ptrs = new List<uint>();
                    filePos = (int)ptr;
                    while(true)
                    {
                        
                        ushort t = readShort();
                        if (t == 0)
                            break;
                        lengths.Add(readShort());
                        ptrs.Add(readInt()); //has the table of ptrs to subtables now
                    }
                    uint q = 0;
                    using (StreamWriter tw = File.CreateText(Directory.GetCurrentDirectory() + "\\decompress\\qlist.txt"))
                        for(int i = 0; i < ptrs.Count; i++)
                        {
                            //for each table,
                            for(int e = 0; e < lengths[i]; e++)
                            {
                                filePos = (int)(ptrs[i] + e * 4);
                                uint entry = readInt();
                                if (entry == 0)
                                    continue;
                                filePos = (int)(entry + 0x28);
                                uint textBloc = readInt();
                                filePos = (int)(textBloc);
                                filePos = (int)(readInt());
                                tw.WriteLine(readJISString());
                            }
                        }
                }
                return;
            }
            data = new List<List<byte>>();
            List<uint> poss = new List<uint>();
            List<uint> counts = new List<uint>();
            uint count = readInt();
            for(int i = 0; i < count; i++)
            {
                poss.Add(readInt());
                counts.Add(readInt());
            }
            for (int i = 0; i < count; i++)
            {
                List<byte> entry = new List<byte>((int)counts[i]);
                filePos = (int)poss[i];
                for (int b = 0; b < counts[i]; b++)
                    entry.Add(readByte());
                data.Add(entry);
            }
        }



        static uint bitspack;
        public static int bitpos = 0;


        public static uint readPackedBits(int bits)
        {
            uint rval = 0;
            while (bits > 0)
            {
                if (bitpos == 0)
                {
                    bitspack = readByte();
                    bitpos = 8;
                }
                rval <<= 1;
                rval |= (uint)((bitspack & 0x80) == 0x80 ? 1 : 0);
                bitspack <<= 1;
                bits--;
                bitpos--;
            }

            return rval;
        }

        public static void PzzDecompress(List<byte> decomp) //sticky fingers! https://github.com/infval/pzzcompressor_jojo/blob/master/pzzcomp_jojo.c
        {
            //List<byte> decomp = new List<byte>();
            int startpos = filePos;
            try
            {
                ushort bitcounter = 0x8000;
                ushort run = readShort();
                while (filePos - startpos < decomp.Capacity)
                {
                    if ((run & bitcounter) != 0)
                    {
                        //matches, decompress
                        //read a short
                        ushort count = readShort();
                        if (count == 0)
                            break; //zero means we're at the end of the file!
                        ushort offset = (ushort)((count & 0x7FF) * 2);
                        count >>= 11;
                        if (count == 0)
                            count = readShort();
                        for (int c = 0; c < count; c++)
                        {
                            //copy a word
                            decomp.Add(decomp[decomp.Count - offset]);
                            decomp.Add(decomp[decomp.Count - offset]);
                        }
                    }
                    else
                    {
                        //copy a word
                        decomp.Add(readByte());
                        decomp.Add(readByte());
                    }
                    bitcounter >>= 1;
                    if (bitcounter == 0)
                    {
                        bitcounter = 0x8000;
                        run = readShort();
                    }
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                filePos = startpos;
                decomp.RemoveRange(0, decomp.Count);
                for (int b = 0; b < decomp.Capacity; b++)
                    decomp.Add(readByte());
            }
            //for (int i = 0; i < 0x20; i++)
            //    Debug.WriteLine(decomp[decomp.Count - 1 - i].ToString("X"));
            //filebuf = decomp; //hand over the decompressed file
            //filepos = 0;
        }

        public static void PzzCompress(ref List<byte> comp)
        {
            comp = new List<byte>();
            //start by writing a run length
            int length = buffers[curBuf].Count / 2 * 2; //multiple of two, ensures word parsing works
            int pos = 0; //read buffer position
            int flags_pos = 0; //position for flagword; starts at the start
            ushort flagword = 0; //the flagword itself
            int curflag = 0;
            comp.Add(0);
            comp.Add(0); //put a placeholder flagword

            while(pos < length)
            {
                int off = 0;
                int len = 0;
                for(int i = (pos >= 0x1000?pos-0xFFE:0); i < pos; i+=2) //for each word, scan each previous word in a range... 0x7FF too large to 1:1 match MHG???
                {
                    if(buffers[curBuf][i] == buffers[curBuf][pos] && buffers[curBuf][i+1] == buffers[curBuf][pos+1]) //if a matching word,
                    {
                        int run_length = 2;
                        while(true) //find the run length
                        {
                            if (pos + run_length >= length || buffers[curBuf][i + run_length] != buffers[curBuf][pos + run_length] || buffers[curBuf][i + 1 + run_length] != buffers[curBuf][pos + 1 + run_length])
                                break;
                            run_length+=2;
                        }
                        if(run_length > 2 && run_length / 2 > len) //if a better match than existing one,
                        {
                            off = pos - i; //update it!
                            len = run_length / 2;
                            if (len >= 0xFFFF) //if we hit the length limit,
                                break; //use it!
                        }
                    }
                }
                //now encode our run
                ushort bitflag = 0;
                if(len >= 2) //if it's worth decompressing, compress it!
                {
                    bitflag = 1;
                    off /= 2;
                    int c = off;
                    if(len <= 0x1F) //seven bit range
                    {
                        c |= (len << 11);
                        comp.Add((byte)(c & 0xFF));
                        comp.Add((byte)(c >> 8));
                    }
                    else //longer range!
                    {
                        comp.Add((byte)(c & 0xFF));
                        comp.Add((byte)(c >> 8)); //add offset
                        comp.Add((byte)(len & 0xFF));
                        comp.Add((byte)(len >> 8)); //and length!
                    }
                    pos += len * 2;//advance past our copied words
                }
                else
                {
                    //straight-up copy over a word
                    comp.Add(buffers[curBuf][pos++]);
                    comp.Add(buffers[curBuf][pos++]);
                }
                //now, update the compression flag
                flagword <<= 1;
                flagword |= bitflag;
                curflag++;
                if(curflag == 16)
                {
                    comp[flags_pos] = (byte)(flagword & 0xFF);
                    comp[flags_pos + 1] = (byte)(flagword >> 8); //update the short
                    flagword = 0;
                    curflag = 0; //reset
                    flags_pos = comp.Count;
                    comp.Add(0); //add new placeholder
                    comp.Add(0);
                }
            }
            //at the end, write the final entry
            flagword <<= 1;
            flagword |= 1;
            flagword <<= 15 - curflag;
            comp[flags_pos] = (byte)(flagword & 0xFF);
            comp[flags_pos + 1] = (byte)(flagword >> 8); //update the short

            comp.Add(0); //add new blank
            comp.Add(0);
        }

        static byte[] rndBufEcd = new byte[] { //0x30 total
            0x4A, 0x4B, 0x52, 0x2E, 0x00, 0x00, 0x00, 0x01,
            0x00, 0x01, 0x0D, 0xCD, 0x00, 0x00, 0x00, 0x01,
            0x00, 0x01, 0x0D, 0xCD, 0x00, 0x00, 0x00, 0x01,
            0x00, 0x01, 0x0D, 0xCD, 0x00, 0x00, 0x00, 0x01,
            0x00, 0x19, 0x66, 0x0D, 0x00, 0x00, 0x00, 0x03,
            0x7D, 0x2B, 0x89, 0xDD, 0x00, 0x00, 0x00, 0x01
        };

        public static void ecdDecrypt()
        {
            buffers[1] = new List<byte>();

            readInt();//magic
            ushort seed = readShort(); //this should always be 5 or lower, offset into the table
            readShort();
            uint filesize = readInt();
            uint crc = readInt();

            crc = (crc << 16) | (crc >> 16) | 1; //shift it around?

            seed *= 8; //get an offset chunk

            uint val = (uint)((rndBufEcd[seed] << 24) | (rndBufEcd[seed + 1] << 16) | (rndBufEcd[seed + 2] << 8) | rndBufEcd[seed + 3]);
            crc = crc * val + (uint)((rndBufEcd[seed + 4] << 24) | (rndBufEcd[seed + 5] << 16) | (rndBufEcd[seed + 6] << 8) | rndBufEcd[seed + 7]); //advance crc

            uint xor = crc;   //get a starting value
            byte prev = (byte)(xor & 0xFF);
            for (int i = 0; i < filesize; i++)
            {
                val = (uint)((rndBufEcd[seed] << 24) | (rndBufEcd[seed + 1] << 16) | (rndBufEcd[seed + 2] << 8) | rndBufEcd[seed + 3]);
                crc = crc * val + (uint)((rndBufEcd[seed + 4] << 24) | (rndBufEcd[seed + 5] << 16) | (rndBufEcd[seed + 6] << 8) | rndBufEcd[seed + 7]); //advance crc

                xor = crc;

                byte data = readByte();
                byte curnybble = (byte)(data ^ prev); //our byte XOR'd with last result
                byte nextnybble = (byte)(curnybble >> 4); //the high nybble
                for (int b = 0; b < 8; b++) //for each nybble in crc
                {
                    byte result = (byte)(xor ^ curnybble); //working byte XOR with crc
                    curnybble = nextnybble; //bring next nibble down
                    nextnybble ^= result; //update next with result
                    xor >>= 4; //shift next nybble in
                }
                prev = (byte)(((curnybble & 0xF) << 4) | (nextnybble & 0xF)); //use the final nybble results
                buffers[1].Add(prev);
            }
            buffers[0] = buffers[1]; //transfer it over, since we have no use for the encrypted file anymore
            filePos = 0;
        }



        static int jshift;
        static int jflag;

        static bool jpkBitflag()
        {
            jshift--;
            if (jshift < 0)
            {
                jshift = 7;
                jflag = getByte();
                //Debug.WriteLine("flag read from {0:X8}: {1:X}", filePos, jflag);
                //if (jflag == 0x87)
                //    Debug.WriteLine("pausing...");
            }
            return ((jflag >> jshift) & 1) == 1;
        }

        public static void jpkUncompress() //some of this don't quite work yet lol
        {
            buffers[1] = new List<byte>();

            jshift = 0;
            jflag = 0;
            huff = false;
            readInt(); //magic
            readShort();
            ushort type = readShort();
            uint start = readInt();
            uint endsize = readInt();
            //filepos = (int)start;
            //now decode
            if (type == 0x04) //LZ but with a huffman table? DEFLATEd?
            {
                huff = true;
                huffTableSize = readShort();
                huffTablePos = filePos; //the start of the huffman table
                //huffDataPos = (int)(filePos + huffTableSize * 2); //the bitflags to navigate the table whenever a new value is needed
                huffDataPos = (int)(filePos + huffTableSize * 4 - 0x3FC);
                hshift = 0;
                hflag = 0;
                type = 0x3; //start the LZ
            }
            if (type == 0x3) //LZ
            {
                while (buffers[1].Count < endsize)
                {
                    if (!jpkBitflag())
                        //raw byte
                        buffers[1].Add(getByte());
                    else if (!jpkBitflag())
                    {
                        //Debug.WriteLine("case 0");
                        //next two bitflags are length, copy between 3-7 bytes
                        byte length = (byte)((jpkBitflag() ? 0x2 : 0) | (jpkBitflag() ? 0x1 : 0));
                        byte offset = getByte();
                        for (int b = 0; b < length + 3; b++)
                            buffers[1].Add(buffers[1][buffers[1].Count - 1 - offset]); //copy recent bytes
                    }
                    else
                    {
                        //read a short
                        ushort val = (ushort)((getByte() << 8) | getByte()); //is this correct endianness?
                        //ushort val = (ushort)(readByte() | (readByte() << 8));
                        int length = (val >> 13) & 0x7;
                        int offset = val & 0x1FFF;
                        if (length > 0)
                        {
                            //Debug.WriteLine("case 1");
                            for (int b = 0; b < length + 2; b++) //copy at least three bytes
                                buffers[1].Add(buffers[1][buffers[1].Count - 1 - offset]);
                        }
                        else if (!jpkBitflag())
                        {
                            //Debug.WriteLine("case 2");
                            //read a nybble of bits for length
                            length = (jpkBitflag() ? 0x8 : 0) | (jpkBitflag() ? 0x4 : 0) | (jpkBitflag() ? 0x2 : 0) | (jpkBitflag() ? 0x1 : 0);
                            for (int b = 0; b < length + 2 + 8; b++) //read at least 0xA bytes
                                buffers[1].Add(buffers[1][buffers[1].Count - 1 - offset]);
                        }
                        else
                        {
                            val = getByte();
                            if (val == 0xFF) //straight read of length offset, minimum length 1B
                            {
                                //Debug.WriteLine("case 3");
                                for (int b = 0; b < offset + 0x1B; b++)
                                    buffers[1].Add(getByte());
                            }
                            else
                            {
                                //Debug.WriteLine("case 4");
                                for (int b = 0; b < val + 0x1A; b++) //copy of length val + 1A
                                    buffers[1].Add(buffers[1][buffers[1].Count - 1 - offset]);
                            }
                        }
                    }
                }
            }
            else
                throw new Exception("different compression: " + type);
            //Debug.WriteLine("finished decompressing");
            curBuf = 1;
            //return readString(4);
        }

        public static void fexedecompress()
        {
            List<byte> dcom = new List<byte>();
            filePos = 0xbcbd4;
            fexedecompress(ref dcom);
            Debug.WriteLine(dcom.Count.ToString("X"));
            /*using(BinaryWriter bw = new BinaryWriter(File.Create(Directory.GetCurrentDirectory() + "\\export\\" + "exedecomp.bin")))
            {
                for (int i = 0; i < dcom.Count; i++)
                    bw.Write(dcom[i]);
            }*/
            //now we could save the chunks out to the new buffer, what is this buffer even?
            //starting at 0x1f8, should be an int of 1, c past is destination, 10 past is sze in bytes, 14 past is source address
            //buffer is 0x1e000 total in size; we jump to 0x1c000 to start execution
            {
                buffers[1] = new List<byte>();
                for (int i = 0; i < dcom.Count; i++)
                    buffers[1].Add(dcom[i]);
                byte[] bf = new byte[0x1e000];
                curBuf = 1;
                int entry = 0;
                while(true)
                {
                    filePos = 0x1F8 + 0xC + 0x28*entry;
                    entry++;
                    uint dest = readInt();
                    uint size = readInt();
                    uint src = readInt();
                    if (dest == 0)
                        break;
                    if (size == 0)
                        continue;
                    filePos = (int)src;
                    for (int i = 0; i < size; i++)
                        bf[i + dest] = readByte();
                }
                //now that they're all copied out, let's save the file?
                /*using(BinaryWriter bw = new BinaryWriter(File.Create(Directory.GetCurrentDirectory() + "\\export\\" + "exephase2.bin")))
                {
                    for (int i = 0; i < bf.Length; i++)
                    {
                        if (bf[i] == null)
                            bw.Write((byte)0);
                        else
                            bw.Write(bf[i]);
                    }
                }*/
                filePos = 0;
                buffers[1] = new List<byte>();
                for (int i = 0; i < bf.Length; i++)
                    buffers[1].Add(bf[i]);
                //now we need to decompress the chunk from 0x1c101, and write it from 1c101 to 1c646?
                filePos = 0x1c101;
                dcom = new List<byte>();
                fexedecompress(ref dcom);
                Debug.WriteLine(dcom.Count.ToString("X"));//compare against 0x546?
                using (BinaryWriter bw = new BinaryWriter(File.Create(Directory.GetCurrentDirectory() + "\\export\\" + "exephase2expand.bin")))
                {
                    for (int i = 0; i < dcom.Count; i++)
                    {
                        bw.Write(dcom[i]);
                    }
                }
                filePos = 0x1c101;
                for (int i = 0; i < dcom.Count; i++)
                    buffers[1][filePos + i] = dcom[i]; //overwrite the old with the new
            }

            //now decompress the segments?
            filePos = 0x1c12d;
            while (true)
            {
                uint pos = readInt();
                uint size = readInt();
                if (pos == 0)
                    break;
                int bookmark = filePos;
                dcom = new List<byte>();
                filePos = (int)pos;
                fexedecompress(ref dcom);
                Debug.WriteLine("expected " + size.ToString("X") + ", got " + dcom.Count.ToString("X"));
                filePos = (int)pos;
                for (int i = 0; i < dcom.Count; i++)
                {
                    byte v = dcom[i];
                    if (v == 0xE8 || v == 0xE9) //call or jump
                    {
                        int ptr = dcom[i+1] | (dcom[i+2]<<8) | (dcom[i+3]<<16) | (dcom[i+4]<<24);
                        ptr -= i;
                        dcom[i + 1] = (byte)(ptr);
                        dcom[i + 2] = (byte)(ptr >> 8);
                        dcom[i + 3] = (byte)(ptr >> 16);
                        dcom[i + 4] = (byte)(ptr >> 24);
                    }
                    buffers[1][filePos + i] = v;
                }
                filePos = bookmark;
            }

            //and write out the final product!
            using (BinaryWriter bw = new BinaryWriter(File.Create(Directory.GetCurrentDirectory() + "\\export\\" + "exephase2final.bin")))
            {
                for (int i = 0; i < buffers[1].Count; i++)
                {
                    bw.Write(buffers[1][i]);
                }
            }
        }

        private static void fexedecompress(ref List<byte> dcom)
        {
            byte flags = 0x80; //this bit will loop indefinitely until end condition
            bool c = false;
            uint eax = 0;
            uint ecx = 0;
            uint ebp = 0;
            dcom.Add(readByte());
            while (true)
            {
                if (fexebit(ref flags, ref c))
                {
                    if (fexebit(ref flags, ref c))
                    {
                        eax = 0;
                        if (fexebit(ref flags, ref c))
                        {
                            //load a nybble into eax
                            for (int i = 0; i < 4; i++)
                            {
                                eax <<= 1;
                                eax += (uint)(fexebit(ref flags, ref c) ? 1 : 0);
                            }
                            if (eax == 0)
                                dcom.Add(0);
                            else
                                dcom.Add(dcom[dcom.Count - (int)eax]);
                        }
                        else
                        {
                            byte al = readByte();
                            ecx = 0;
                            c = (al & 0x1) == 1;
                            al >>= 1;
                            if (al == 0)
                                break;
                            else
                            {
                                ecx = (uint)(2 + (c ? 1 : 0));
                                eax &= 0xFFFFFF00;
                                eax |= (uint)al;
                                ebp = eax;
                                for (int i = 0; i < ecx; i++)
                                    dcom.Add(dcom[dcom.Count - (int)eax]);
                            }
                        }
                    }
                    else
                    {
                        eax = 1;
                        fexefeed(ref eax, ref flags, ref c);
                        eax -= 2;
                        if (eax == 0)
                        {
                            ecx = 1;
                            fexefeed(ref ecx, ref flags, ref c);
                            for (int i = 0; i < ecx; i++)
                                dcom.Add(dcom[dcom.Count - (int)ebp]);
                        }
                        else
                        {
                            eax -= 1;
                            eax <<= 8;
                            eax |= (uint)readByte();
                            ebp = eax;
                            ecx = 1;
                            fexefeed(ref ecx, ref flags, ref c);
                            if (eax >= 0x7D00)
                            {
                                ecx += 2;
                                for (int i = 0; i < ecx; i++)
                                    dcom.Add(dcom[dcom.Count - (int)eax]);
                            }
                            else
                            {
                                if (eax >= 0x500)
                                {
                                    ecx += 1;
                                    for (int i = 0; i < ecx; i++)
                                        dcom.Add(dcom[dcom.Count - (int)eax]);
                                }
                                else
                                {
                                    if (eax > 0x7F)
                                    {
                                        for (int i = 0; i < ecx; i++)
                                            dcom.Add(dcom[dcom.Count - (int)eax]);
                                    }
                                    else
                                    {
                                        ecx += 2;
                                        for (int i = 0; i < ecx; i++)
                                            dcom.Add(dcom[dcom.Count - (int)eax]);
                                    }
                                }
                            }
                        }
                    }
                }
                else
                    dcom.Add(readByte());

            }
        }

        private static bool fexebit(ref byte b, ref bool c)
        {
            c = (b & 0x80) == 0x80;
            b <<= 1;
            if (b == 0x00) //this will always have carry set!
            {
                b = readByte();
                c = (b & 0x80) == 0x80;
                b <<= 1; //so instead of adc b,b we just shift and OR/add 1
                b |= 0x01;
            }
            return c;
        }
        private static void fexefeed(ref uint reg, ref byte flags, ref bool c)
        {
            do
            {
                fexebit(ref flags, ref c);
                reg <<= 1;
                reg |= (uint)(c ? 1 : 0);
            } while (fexebit(ref flags, ref c));
        }

        private static uint huffTableSize;
        private static int huffTablePos;
        private static int huffDataPos;
        private static int hshift;
        private static int hflag;

        private static byte getByte()
        {
            if (huff)
            {
                ushort value = (ushort)huffTableSize;

                while ((value & 0xFF00) != 0) //bits set in the upper byte signifies a branch, not a value
                {
                    hshift--;
                    if (hshift < 0) //if out of bits
                    {
                        hshift = 7;
                        filePos = huffDataPos++; //grab the next set of bits from the huff data
                        hflag = readByte();
                    }
                    byte flag = (byte)((hflag >> hshift) & 1);
                    filePos = huffTablePos + ((value - 0x100) * 2 + flag) * 2; //turn it into a branch ID, branches are two values wide, select our value, values are two bytes in size
                    value = readShort(); //load our next value
                }
                //bits not set, we're now at our value!
                return (byte)value;
            }
            return readByte();
        }

    }
}
