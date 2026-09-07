using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace APX_Viewer
{
    class BehaviourScript
    {
        int indent = 0;
        string output = "";

        public void Parse()
        {
            FileBuffer.filePos = 0x22DB80; //this is for mh1j
            const int mons = 35;
            const int locales = 6;
            uint[,] ptrs = new uint[locales, mons];
            for (int L = 0; L < locales; L++) //seven locales
            {
                for (int M = 0; M < mons; M++) //34 monsters
                {
                    ptrs[L, M] = FileBuffer.readInt();
                }
                FileBuffer.readInt();//advance past the blank entry
            }

            //now open game.bin
            string filename = "";
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "game.bin|game.bin";
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    //Get the path of specified file
                    filename = openFileDialog.FileName;
                }
            }
            if (filename == "")
            {
                return;
            }
            FileBuffer.loadFile(filename, 1);

            List<int> list = new List<int>();

            for (int L = 0; L < locales; L++) //seven locales
            {
                for (int M = 0; M < mons; M++) //34 monsters
                {
                    int gamebinoffset = 0x533980;
                    FileBuffer.filePos = (int)ptrs[L, M] - gamebinoffset; //at the mon locale table
                    if (list.Contains(FileBuffer.filePos))
                        continue;
                    list.Add(FileBuffer.filePos); //save this table!
                    int behaveptr = (int)FileBuffer.readInt() - gamebinoffset;
                    int bookmark = FileBuffer.filePos;
                    int next = 0xFFFFFF;
                    for (int p = 0; p < 7; p++)
                    {
                        int nextt = (int)FileBuffer.readInt();
                        if (nextt > gamebinoffset && nextt < next)
                            next = nextt;
                    }
                    FileBuffer.filePos = behaveptr;
                    List<int> behaviours = new List<int>();
                    while (true)
                    {
                        if (FileBuffer.filePos == next - gamebinoffset)
                            break;
                        uint val = FileBuffer.readInt();
                        if (val == 0)
                            break;
                        behaviours.Add((int)val);
                    }

                    //now the "content"
                    FileBuffer.filePos = bookmark;
                    int contentptr = (int)FileBuffer.readInt();
                    bookmark = FileBuffer.filePos;
                    int nextcont = 0xFFFFFF;
                    for (int p = 0; p < 7; p++)
                    {
                        int nextt = (int)FileBuffer.readInt();
                        if (nextt > gamebinoffset && nextt < nextcont)
                            nextcont = nextt;
                    }
                    FileBuffer.filePos = contentptr;
                    List<int> contents = new List<int>();
                    while (true)
                    {
                        if (FileBuffer.filePos == nextcont - gamebinoffset)
                            break;
                        uint val = FileBuffer.readInt();
                        if (val == 0)
                            break;
                        contents.Add((int)val);
                    }

                    //now the "subcontents"
                    FileBuffer.filePos = bookmark;
                    FileBuffer.filePos += 0x34;
                    List<int> subcontentptrs = new List<int>();
                    while (true) //add the list of subs
                    {
                        uint val = FileBuffer.readInt();
                        if (val == 0)
                            break;
                        subcontentptrs.Add((int)val);
                    }
                    List<List<int>> subcontents = new List<List<int>>();
                    for (int p = 0; p < subcontentptrs.Count; p++)
                    {
                        FileBuffer.filePos = subcontentptrs[p];
                        //add for each table
                        FileBuffer.readInt();//starts whit a blank
                        List<int> tmplst = new List<int>();
                        while(true)
                        {
                            uint val = FileBuffer.readInt();
                            if (val == 0)
                                break;
                            tmplst.Add((int)val);
                        }
                        subcontents.Add(tmplst);
                    }

                    output = "";
                    string path = Directory.GetCurrentDirectory() + "\\export\\";
                    for (int b = 0; b < behaviours.Count; b++)
                    {
                        if (behaviours[b] < 0x533980)
                        {
                            //output += "++ SKIPPED BEHAVIOUR " + b.ToString("X2") + " DUE TO BEING IN MAIN FILE ++";
                            //continue;
                            gamebinoffset = 0x100000 - 0x180;
                            FileBuffer.setBuffer(0);
                        }
                        FileBuffer.filePos = behaviours[b] - gamebinoffset;
                        output += "== BEHAVIOUR " + b.ToString("X2") + " ==";

                        ParseScript();

                        FileBuffer.setBuffer(1);
                        gamebinoffset = 0x533980;
                    }
                    //mapped all the behaviours for this locale + monster combo, save it to a file!
                    using (StreamWriter sw = new StreamWriter(path + "Monster_Behaviours_M" + M.ToString("D2") + "L" + L.ToString("D2") + ".txt"))
                    {
                        sw.Write(output);
                    }
                }
            }
        }

        void ParseScript()
        {
            while (true)
            {
                output += "\n";
                byte v = FileBuffer.readByte();
                for (int i = 0; i < indent; i++)
                    output += " "; //indent as needed
                switch (v)
                {
                    case 0x01:
                        output += "kehai(hint) check ";
                        output += FileBuffer.readByte().ToString("X2");
                        break;
                    case 0x02:
                        output += "ninshiki(awareness) check ";
                        byte tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += "then";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x02)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x03:
                        output += "area move check ";
                        output += FileBuffer.readByte().ToString("X2");
                        break;
                    case 0x04:
                        output += "AI reset";
                        break;
                    case 0x05:
                        output += "act set ";
                        output += FileBuffer.readByte().ToString("X2") + " ";
                        output += FileBuffer.readByte().ToString("X2") + " ";
                        output += FileBuffer.readByte().ToString("X2");
                        break;
                    case 0x06:
                        output += "target set ";
                        output += FileBuffer.readByte().ToString("X2") + " ";
                        output += FileBuffer.readByte().ToString("X2") + " ";
                        output += FileBuffer.readByte().ToString("X2");
                        break;
                    case 0x07:
                        output += "change to behaviour ";
                        output += FileBuffer.readByte().ToString("X2");
                        break;
                    case 0x08:
                        output += "if standing ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += "then";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x02)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x09:
                        output += "if flying ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += "then";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x02)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x0a:
                        output += "set body status ";
                        output += FileBuffer.readByte().ToString("X2");
                        break;
                    case 0x0b:
                        output += "mode check ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += FileBuffer.readByte().ToString("X2");
                            output += " then";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x02)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x0c:
                        output += "flag set ";
                        output += FileBuffer.readByte().ToString("X2") + " ";
                        output += FileBuffer.readByte().ToString("X2");
                        break;
                    case 0x0d:
                        output += "flag cleared ";
                        output += FileBuffer.readByte().ToString("X2");
                        break;
                    case 0x0e:
                        output += "if stage ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += FileBuffer.readByte().ToString("X2");
                            output += " then";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x02)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x0f:
                        output += "set route ";
                        output += FileBuffer.readByte().ToString("X2");
                        output += ", ";
                        output += FileBuffer.readByte().ToString("X2");
                        output += ", ";
                        output += FileBuffer.readByte().ToString("X2");
                        output += " ";
                        output += FileBuffer.readByte().ToString("X2");
                        output += " ";
                        output += FileBuffer.readByte().ToString("X2");
                        break;
                    case 0x10:
                        output += "route check";
                        break;
                    case 0x11:
                        output += "kehai (hint) player set";
                        break;
                    case 0x12:
                        output += "find check";
                        break;
                    case 0x13:
                        output += "set player target";
                        break;
                    case 0x14:
                        output += "if angle ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += FileBuffer.readByte().ToString("X2");
                            output += " then";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x02)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x15:
                        output += "body status check ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += FileBuffer.readByte().ToString("X2");
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "case ";
                            output += FileBuffer.readByte().ToString("X2");
                        }
                        else if (tmp == 0x02)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x03)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x16:
                        output += "action set ";
                        output += FileBuffer.readByte().ToString("X2");
                        break;
                    case 0x17:
                        output += "area route set ";
                        output += FileBuffer.readByte().ToString("X2") + " ";
                        output += FileBuffer.readByte().ToString("X2") + " ";
                        output += FileBuffer.readByte().ToString("X2") + " ";
                        output += FileBuffer.readByte().ToString("X2");
                        break;
                    case 0x18:
                        output += "area route move";
                        break;
                    case 0x19:
                        output += "area route check";
                        break;
                    case 0x1a:
                        output += "escape area set ";
                        output += FileBuffer.readByte().ToString("X2");
                        break;
                    case 0x1b:
                        output += "if mind ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += FileBuffer.readByte().ToString("X2");
                            output += " then";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x02)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x1c:
                        output += "if mind no. ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += FileBuffer.readByte().ToString("X2");
                            output += " then";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x02)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x1d:
                        output += "mind move ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += FileBuffer.readByte().ToString("X2");
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "case ";
                            output += FileBuffer.readByte().ToString("X2");
                        }
                        else if (tmp == 0x02)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x03)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x1e:
                        output += "mind move reset";
                        break;
                    case 0x20:
                        output += "pl angle select ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += FileBuffer.readByte().ToString("X2");
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "case ";
                            output += FileBuffer.readByte().ToString("X2");
                        }
                        else if (tmp == 0x02)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x03)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x21:
                        output += "if thirsty ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += FileBuffer.readByte().ToString("X2");
                            output += "then";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x02)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x22:
                        output += "if near pos ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += "range ";
                            output += FileBuffer.readByte().ToString("X2");
                            output += " then";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x02)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x23:
                        output += "emtype monster ";
                        throw new System.Exception("not implemented");
                        break;
                    case 0x24:
                        output += "repeat count ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += "set ";
                            output += FileBuffer.readByte().ToString("X2");
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x25:
                        output += "repeat clear";
                        break;
                    case 0x26:
                        output += "demo flag set ";
                        output += FileBuffer.readByte().ToString("X2");
                        break;
                    case 0x27:
                        output += "type select ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += FileBuffer.readByte().ToString("X2");
                            output += " options";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "case ";
                            output += FileBuffer.readByte().ToString("X2");
                        }
                        else if (tmp == 0x02)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x03)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x28:
                        output += "if all players in same stage ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += "then";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x02)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x29:
                        output += "if stay timer ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += "then";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x02)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x2a:
                        output += "if runaway timer ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += "then";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x02)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x2b:
                        output += "if flag ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += FileBuffer.readByte().ToString("X2");
                            output += " is ";
                            output += FileBuffer.readByte().ToString("X2");
                            output += " then";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x02)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x2c:
                        output += "my em type select ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += FileBuffer.readByte().ToString("X2");
                            output += " entries";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "species ";
                            output += FileBuffer.readByte().ToString("X2");
                        }
                        else if (tmp == 0x02)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x03)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x2d:
                        output += "smell set";
                        break;
                    case 0x2e:
                        output += "search data set ";
                        output += FileBuffer.readByte().ToString("X2");
                        break;
                    case 0x2f:
                        output += "if egg carried ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += "then";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x02)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x30:
                        output += "egg cancel check";
                        break;
                    case 0x31:
                        output += "yobi pos set";
                        break;
                    case 0x32:
                        output += "body status check (2nd type) ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += "is ";
                            output += FileBuffer.readByte().ToString("X2");
                            output += " then";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x02)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x33:
                        output += "body status select ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += FileBuffer.readByte().ToString("X2");
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "case ";
                            output += FileBuffer.readByte().ToString("X2");
                        }
                        else if (tmp == 0x02)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x03)
                        {
                            output += "end";
                            indent--;
                        }
                        throw new System.Exception("verify this please");
                        break;
                    case 0x34:
                        output += "act st check ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += "main ";
                            output += FileBuffer.readByte().ToString("X2");
                            output += " sub ";
                            output += FileBuffer.readByte().ToString("X2");
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x02)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x35:
                        output += "if ikari ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += "then";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x02)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x38:
                        output += "if water ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += "then";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x02)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x39:
                        output += "seen check ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += "branch?";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "else?";
                        }
                        else if (tmp == 0x02)
                        {
                            output += "target?";
                            indent--;
                        }
                        break;
                    case 0x3a:
                        output += "sensor check ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += "then";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x02)
                        {
                            output += "end";
                            indent--;
                        }
                        //throw new System.Exception("verify");
                        break;
                    case 0x3b:
                        output += "if boss work ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += "then";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x02)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x3c:
                        output += "if boss attack ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += "then";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x02)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x3d:
                        output += "before stage check ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += FileBuffer.readByte().ToString("X2");
                            output += " then";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x02)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x3e:
                        output += "before stage select ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += FileBuffer.readByte().ToString("X2");
                            output += " entries";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "stage ";
                            output += FileBuffer.readByte().ToString("X2");
                        }
                        else if (tmp == 0x02)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x03)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x3f:
                        output += "ground move area ";
                        output += FileBuffer.readByte().ToString("X2");
                        output += " val ";
                        output += FileBuffer.readByte().ToString("X2");
                        break;
                    case 0x40:
                        output += "em mode change ";
                        output += FileBuffer.readByte().ToString("X2");
                        break;
                    case 0x41:
                        output += "em vital add ";
                        output += FileBuffer.readByte().ToString("X2");
                        break;
                    case 0x42:
                        output += "horm angle check ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += FileBuffer.readByte().ToString("X2");
                            output += " then";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x02)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x44:
                        output += "if boss same stage ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += "then";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x02)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x45:
                        output += "if player fishing ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += "then";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x02)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x46:
                        output += "target pl act check";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += FileBuffer.readByte().ToString("X2");
                            output += " then ";
                            output += FileBuffer.readByte().ToString("X2");
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x02)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x47:
                        output += "fish ok? ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += "then";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x02)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x48:
                        output += "timer set secs ";
                        output += FileBuffer.readByte().ToString("X2");
                        break;
                    case 0x49:
                        output += "player land target ";
                        output += FileBuffer.readByte().ToString("X2");
                        break;
                    case 0x4a:
                        output += "player look check ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += "then";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x02)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x4b:
                        output += "kehai clear";
                        break;
                    case 0x4c:
                        output += "hate clear";
                        break;
                    case 0x4d:
                        output += "horm pos set";
                        break;
                    case 0x4e:
                        output += "thirsty add (half max)";
                        output += FileBuffer.readByte().ToString("X2");
                        break;
                    case 0x4f:
                        output += "hunger add (half max)";
                        output += FileBuffer.readByte().ToString("X2");
                        break;
                    case 0x50:
                        output += "sleep add (half max";
                        output += FileBuffer.readByte().ToString("X2");
                        break;
                    case 0x51:
                        output += "if swim ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += "then";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x02)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x52:
                        output += "all pl target select";
                        break;
                    case 0x53:
                        output += "same stage pl target select";
                        break;
                    case 0x54:
                        output += "if target pl same stage ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += "then";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x02)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x55:
                        output += "if target player hate high ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += "then";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x02)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x56:
                        output += "if quest ID equals ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += FileBuffer.readByte().ToString("X2");
                            output += " then";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x02)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x57:
                        output += "quest number switch ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += FileBuffer.readByte().ToString("X2");
                            output += " entries";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "number ";
                            output += FileBuffer.readByte().ToString("X2");
                        }
                        else if (tmp == 0x02)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x03)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x58:
                        output += "boss player target";
                        break;
                    case 0x59:
                        output += "tenjo check";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += "then";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x02)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x5a:
                        output += "target land no check ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += FileBuffer.readByte().ToString("X2");
                            output += " then";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x02)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x5b:
                        output += "ninshiki timer sub";
                        break;
                    case 0x5c:
                        output += "tenjo stage check ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += "then";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x02)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x5d:
                        output += "smell set check ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += "then";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x02)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x5e:
                        output += "my floor check ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += FileBuffer.readByte().ToString("X2");
                            output += " then";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x02)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x5f:
                        output += "st25 (castle courtyard) pl target select";
                        break;
                    case 0x60:
                        output += "st25 (castle courtyard) gate check ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += "then";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x02)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x61:
                        output += "runaway timer reset";
                        break;
                    case 0x62:
                        output += "dansa select";
                        throw new System.Exception("unimplemented");
                        break;
                    case 0x63:
                        output += "male check ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += "then";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x02)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x64:
                        output += "target pl hate check ";
                        throw new System.Exception("unimplemented");
                        break;
                    case 0x65:
                        output += "all pl hate clear";
                        break;
                    case 0x66:
                        output += "pl ride check ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += "then";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x02)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x67:
                        output += "em master check ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += "then";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "else";
                        }
                        else if (tmp == 0x02)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x68:
                        output += "em cmd reset";
                        break;


                    case 0x80:
                        output += "rand select ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += FileBuffer.readByte().ToString("X2");
                            output += " outcomes";
                            indent++;
                        }
                        else if (tmp < 11)
                        {
                            output += "outcome " + tmp + ", odds ";
                            output += FileBuffer.readByte().ToString("X2");
                        }
                        else if (tmp == 0xff)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x81:
                        output += "contents ";
                        output += FileBuffer.readByte().ToString("X2");
                        break;
                    case 0x82:
                        output += "subcontents ";
                        output += FileBuffer.readByte().ToString("X2");
                        output += " ";
                        output += FileBuffer.readByte().ToString("X2");
                        break;
                    case 0x83:
                        output += "range check ";
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += FileBuffer.readByte().ToString("X2");
                            output += " cases?";
                            indent++;
                        }
                        else if (tmp < 6)
                        {
                            output += "case " + tmp;
                        }
                        else if (tmp == 0xff)
                        {
                            output += "end";
                            indent--;
                        }
                        break;
                    case 0x84:
                        output += "label?";
                        break;

                    case 0x90:
                        output += "position set ";
                        output += FileBuffer.readByte().ToString("X2");
                        break;
                    case 0x91:
                        output += "vector set ";
                        output += FileBuffer.readByte().ToString("X2");
                        break;
                    case 0x92:
                        output += "demo start (dummy command?)";
                        break;
                    case 0x93:
                        output += "wait set (dummy command?)";
                        break;
                    case 0x94:
                        output += "em attack bit ";
                        //has 0,1,2,3 - 3 is end, no additional args; 2 is else?
                        tmp = FileBuffer.readByte();
                        if (tmp == 0x00)
                        {
                            output += FileBuffer.readByte().ToString("X2");
                            output += " selections";
                            indent++;
                        }
                        else if (tmp == 0x01)
                        {
                            output += "case ";
                            output += FileBuffer.readByte().ToString("X2");
                        }
                        else if (tmp == 0x02)
                        {
                            output += "else";
                            indent--;
                        }
                        else if (tmp == 0x03)
                        {
                            output += "end";
                            indent--;
                        }
                        break;

                    case 0xFF:
                        output += "flow end\n";
                        tmp = FileBuffer.readByte();
                        break;
                    case 0x00:
                        break;

                    default:
                        throw new System.Exception("unhandled behaviour code!");
                }
                if ((v == 0xFF && indent == 0) || v == 0x00)
                    break;
            }
        }
    }
}
