using System;
using System.Collections.Generic;
using static APX_Viewer.FileBuffer;
using System.Text;
using System.Drawing;
using System.Diagnostics;

namespace APX_Viewer
{
    class Quest
    {
        public struct EntList
        {
            public uint mapID;
            public uint unk;
            public uint typePtr;
            public uint dataPtr;
        }

        public enum GatherType { Normal, unk1, unk2, Pickaxe, Net}
        public enum Game { MH1 = 0x40, MHG = 0x48, MH2 = 0x74, MH2Arena = 0x64, MHFZZ = 0xC0, MHP = 0x3C}
        public enum DosObjective { Hunt = 0x01, Deliver = 0x02, Damage = 0x04 }

        public Game game;

        struct GatheringSpot
        {
            public int mapID;
            public float x;
            public float y;
            public float z;
            public float radius;
            public ushort itemPool;
            public ushort maxQuantity;
            public GatherType type;
            public ushort extra;
        }

        struct AreaOrigin
        {
            public float x;
            public float y;
        }

        List<GatheringSpot> gatheringSpots;
        List<AreaOrigin> areaOrigins;

        List<string> scriptCodes = new List<string> {
                    "X Wait until 3C7474 is 0", "Check Monster goal", "Set Monster goal", "X Check Item goal", "Set Item goal", "Set timer", "Wait for timer", "Display message",
                    "X Loop to 0B if item not complete/wait for arg item", "X null", "X Fetch quest supplies", "bookmark next op", "X null", "X set 3C7450 to arg", "X increment 3C7450 by arg", "X skip ahead to 0x12 conditionally",
                    "X do stuff with 0x11s until 0x12", "X increment 3C7444 then advance to 0x12", "X increment 3C7444", "X null", "X null", "X null", "Player Death label", "jump to bookmark",
                    "wait until action is finished", "X null", "Label", "Start quest timer", "jump to label", "X check item goal 2", "trigger victory END", "trigger failure END",
                    "change wave/quest state", "check player carrying item", "check delivery goal", "jump to label when delivery done", "Check Monster goal left", "Time Over message + packet", "X send packet", "X null",
                    "fatalis health check", "fatalis repel fail start", "lao health check", "lao repel fail start", "jump to label when Fort falls", "sync victory?", "G unknown (ykk, cphd, and rlos) ", "Change timeover label"
        };

        public List<ConstantsLocales.Pushpin> pins = new List<ConstantsLocales.Pushpin>();

        public uint locale;

        public void Parse()
        {
            List<Point> Mpins = new List<Point>();

            uint foliageptr = 0;
            uint gatherpoolsptr = 0;
            uint fishingptr = 0;
            uint fishpoolsptr = 0;

            gatheringSpots = new List<GatheringSpot>();
            areaOrigins = new List<AreaOrigin>();

            uint dataptr = readInt(); //pointer to quest data (this gets copied into lobby.bin): 0x40 for base Mh1, 0x48 for MHG, 0x74 for MH2 (except arena matches use 0x64?), 0xC0 for MHFZZ, 0x3C for mhp1
            game = (Game)dataptr;
            uint playerptr = readInt(); //player block, always 0x70?
            uint boxptr = readInt(); //box block
            uint rewardsptr = readInt(); //reward block

            uint goalptr = readInt(); //goal block; @ 0x10
            uint entsptr = readInt(); //entity block
            uint monptr = readInt(); //monster block
            uint linkptr = 0;//link block

            uint originsptr = 0; //origins block; @ 0x20
            uint campptr = 0; //camp block
            uint gatherptr = 0; //gathering points block
            uint mapobjptr = 0; //map objects block

            if (game != Game.MHP)
            {
                linkptr = readInt(); //link block
                originsptr = readInt(); //origins block; @ 0x20
                campptr = readInt(); //camp block
                gatherptr = readInt(); //gathering points block
                mapobjptr = readInt(); //map objects block
            }
            else
            {
                uint camp = readInt(); //? camp map?
                gatherptr = readInt(); //gathering block
            }
            
            uint resultptr = readInt(); //result text block; @ 0x30
            uint pattern;
            uint online;
            uint gextra = 0;
            uint size = 0;
            if (game != Game.MH2 && game != Game.MH2Arena)
            {
                pattern = readInt(); //which HP set to use for monsters
                readInt(); //HRP
                online = readInt(); //online quest if set to 0xF
                if (game == Game.MHG) //g quest
                {
                    size = readShort(); //base monster size
                    readByte(); //what monster carves
                    readByte(); //what spawn to use
                    gextra = readByte(); //monster size class
                    readByte();//supply delivery type:
                    readByte();//0 is all available at start :)
                    readByte();//1 is random supply delivery
                    //2 is after arg3 monster is slain arg4 times!
                }
            }
            else
            {
                foliageptr = readInt(); //foliage pointer? maybe mosswine waypoints?
                gatherpoolsptr = readInt(); //gather pools pointer
                if (game != Game.MH2Arena)
                {
                    fishingptr = readInt(); //fishing pointer
                    fishpoolsptr = readInt(); //fishing results pointer @ 0x40
                }
                size = readShort(); //base monster size percent
                readByte(); //size group
                readByte(); //need further info
                pattern = readInt(); //high rank flag? HP pattern set?
                readInt(); //HRP
                online = readInt(); //online quest flag @ 0x50

                readInt(); //sub A HRP
                readInt(); //sub B HRP
                readShort();
                readShort();

                readInt(); // @ 0x60
                readInt();
                readShort();
                readShort(); //alwasy 0x100?

                readInt();
                readInt(); //always 0x200? @ 0x70
                //data pointer points to here!
                readShort();
                readShort(); //81A, quest availability?
            }
            ushort qtype = readByte(); //quest type
            ushort fulu = readByte(); //0x01 modifies the endless delivery quests to list how many items were delivered by each player (ONLY says well-done steak)
                                      //0x02 is the battle silence bit
                                      //0x04 overrides room music with urgent tune (unused?)
                                      //0x08 plays catless Cat Scat on room load (unused?)
                                      //0x10 (Modori_dama_ck in mh1)... disables farcaster
                                      //0x20 maxes the timer on quest clear (used in kulu huntathon, lao, and fata)
                                      //0x40 disables quest difficulty roll (most low offline, offline subs, training, and fatas+lao)
                                      //0x80 disables incrementing of quest difficulty (32d15e) (checked on new monster spawn, watchpoint 4b4868) (unused?)
            ushort stars = readShort(); //star level
            readInt(); //posting fee
            readInt(); //reward amount
            readInt(); //cart fee
            if(game == Game.MH2)
            {
                readInt(); //subquest 1 reward
                readInt(); //subquest 2 reward
            }
            readInt(); //time limit, in ticks (30hz)
            locale = readInt(); //locale 1-indexed (fort, fnh, desert, swamp, volcano, jungle, castle)
            uint descriptionptr = readInt(); //pointer to description block?
            readShort(); //quest's restriction
            readShort(); //quest's internal ID
            if(game == Game.MH2)
            {
                //more to say, have you?
                readInt(); //main objective type - 2 is deliver
                readShort(); //objective ID - item ID
                readShort(); //objective quantity
                readInt(); //sub a type
                readShort(); //target
                readShort(); //quantity
                readInt(); //sub b type
                readShort(); //target
                readShort(); //quantity
                readInt(); //don't know, seems to be 2 in both deliver and slay missions
            }

            //description block
            filePos = (int)descriptionptr;
            {
                uint p1 = readInt(); //quest name / category
                uint p2 = readInt(); //goal text / quest name
                uint p3 = readInt(); //fail condition text / subquest A goal
                uint p4 = readInt(); //quest description text / subquest B goal
                if(game == Game.MH2)
                {
                    uint p5 = readInt(); //success condition
                    uint p6 = readInt(); //fail conditions
                    uint p7 = readInt(); //requestor
                    uint p8 = readInt(); //description?
                }
                filePos = (int)p1;
                /*while (true)
                {
                    uint v = readInt(); //ez alignment this way
                    if ((v & 0xFF) == 0 | (v & 0xFF00) == 0 | (v & 0xFF0000) == 0 | (v & 0xFF000000) == 0)
                        break;
                }
                */
                string qname = readJISString();
                Debug.WriteLine(qname);
                filePos = (int)p2;
                while (true)
                {
                    uint v = readInt(); //ez alignment this way
                    if ((v & 0xFF) == 0 | (v & 0xFF00) == 0 | (v & 0xFF0000) == 0 | (v & 0xFF000000) == 0)
                        break;
                }
                filePos = (int)p3;
                while (true)
                {
                    uint v = readInt(); //ez alignment this way
                    if ((v & 0xFF) == 0 | (v & 0xFF00) == 0 | (v & 0xFF0000) == 0 | (v & 0xFF000000) == 0)
                        break;
                }
                filePos = (int)p4;
                while (true)
                {
                    uint v = readInt(); //ez alignment this way
                    if ((v & 0xFF) == 0 | (v & 0xFF00) == 0 | (v & 0xFF0000) == 0 | (v & 0xFF000000) == 0)
                        break;
                }
            }

            //player block
            loadPlayerBlock(playerptr);

            //entities block
            filePos = (int)entsptr;
            List<uint> entroomlists = new List<uint>();
            while (true)
            {
                uint val = readInt();
                if (val != 0)
                    entroomlists.Add(val);
                else
                    break;
            }
            for (int i = 0; i < entroomlists.Count; i++)
            {
                //read list
                filePos = (int)entroomlists[i];
                List<EntList> els = new List<EntList>();
                while (true)
                {
                    uint v1 = readInt(); //room ID
                    uint v2 = readInt(); //blank? 0x9999 if only wave is empty
                    uint v3 = readInt(); //types pointer
                    uint v4 = readInt(); //data pointer
                    if (v1 == 0)
                        break;
                    els.Add(new EntList { mapID = v1, unk = v2, typePtr = v3, dataPtr = v4 });
                }
                for (int l = 0; l < els.Count; l++)
                {
                    //read types in the room
                    filePos = (int)els[l].typePtr;
                    readInt(); //first type
                    readInt(); //second type
                    readInt(); //third type
                    readInt(); //fourth type; FFFF is a blank entry

                    //now read the entity entries for the room!
                    filePos = (int)els[l].dataPtr;
                    while (true)
                    {
                        Point p = new Point();
                        ushort v1 = readShort(); //ent type
                        readShort(); //model variation
                        readInt(); //the amount of lives this entity has, 0x63 is infinite. highest byte spawn area, sometimes second byte has a value too - respawn after corpse despawn?
                        readInt(); //0, two shorts? both set to -1 on load
                        readInt(); //0
                        readInt(); //0
                        readInt(); //0
                        readInt(); //0
                        readInt(); //z rotation spawn
                        p.X = (int)readFloat(); //X spawn 10294.1, 7789.4
                        readFloat(); //Y spawn
                        p.Y = (int)readFloat(); //Z spawn 11287.8, 9561.399
                        readInt(); //0
                        readInt(); //0
                        readInt(); //0
                        readInt(); //0
                        if (v1 == 0xFFFF)
                            break;
                        //if (els[l].mapID == 0x27)
                        //    pins.Add(p);
                    }
                }
            }

            //monster block
            filePos = (int)monptr;
            List<EntList> waves = new List<EntList>();
            List<uint> montypes = new List<uint>();
            while (true)
            {
                uint v1 = readInt(); //wave?
                uint v2 = readInt(); //0?
                uint v3 = readInt(); //types pointer
                uint v4 = readInt(); //data pointer
                if (v1 == 0)
                    break;
                waves.Add(new EntList { mapID = v1, unk = v2, typePtr = v3, dataPtr = v4 });
            }
            //and do the same list reads
            for (int w = 0; w < waves.Count; w++)
            {
                //read types in the room
                filePos = (int)waves[w].typePtr;
                uint m1 = readInt(); //first type
                uint m2 = readInt(); //second type
                uint m3 = readInt(); //third type
                uint m4 = readInt(); //fourth type; FFFF is a blank entry
                if (m1 != 0xFFFFFFFF)
                    montypes.Add(m1);
                if (m2 != 0xFFFFFFFF)
                    montypes.Add(m2);
                if (m3 != 0xFFFFFFFF)
                    montypes.Add(m3);
                if (m4 != 0xFFFFFFFF)
                    montypes.Add(m4);
                //now read the entity entries for the room!
                filePos = (int)waves[w].dataPtr;
                while (true)
                {
                    ushort v1 = readShort(); //ent type
                    readShort(); //variation? copy?
                    readInt(); //the amount of lives this entity has, 0x63 is infinite. highest byte spawn area, sometimes second byte has a value too - respawn after corpse despawn? make corpse linger?
                    readInt(); //0
                    readInt(); //0
                    readInt(); //0
                    readInt(); //0
                    readInt(); //0
                    readInt(); //some short value, dunno what it signifies - maybe despawn timer?
                    readFloat(); //X spawn
                    readFloat(); //Y spawn
                    readFloat(); //Z spawn
                    readInt(); //0
                    readInt(); //0
                    readInt(); //0
                    readInt(); //0
                    if (v1 == 0xFFFF)
                        break;
                }
            }

            //goal block, MH1 and MHG use this while MH2 and MHF instead seems to have a placeholder/dummy block.
            filePos = (int)goalptr;
            {
                int conditions = 0;
                //Debug.WriteLine("----------------------");
                while (true)
                {
                    ushort v1 = readShort(); //goal type
					//need to document: 2D, 16
					//00 waits until 3C7474 is 0 (decremented on monster slay?)
					//01 waits until eall enemy goals are clear
					//02 sets enemy species and qty
					//03 waits until item quantity check is true
					//04 sets item ID and qty for delivery goal
					//05 sets 3C744C (set timer)
					//06 decrements 3C744C until zero (wait for timer)
					//07 is display ID message (3 is first aux)
					//08 loops back to 0B (bookmark) if 03 fails, else it waits until player is in map arg1
					//09 is null?
					//0A seems to fetch quest supplies? -1 is always, -3 is if gs or bg, else arg is class
					//0B bookmark
					//0D sets 3C7450 to arg (set time left, in ticks)
					//0E increases 3C7450 by arg (increment time left, in ticks)
					//0F conditional start, look for playernum or -4 as arg, ends at 0x12 (use 0x11)
					//10 conditional start, look for 0x11s, if arg is -3, match bg (class 1 or 5)
					//   if arg is -2, if class 0, 2, 3, 4, do
					//   if arg is -4, or arg is class, do
					//11 start of condition branch
					//12 end of condition block!
					//16 is searched for on Quest_restart, when player dies
					//17 stores 3C7478 to 3C7476, then jumps to it (return to bookmark)
					//18 waits until action is no longer ocurring
					//1A is label
					//1B shows/starts timer. arg is label to jump to on time over
					//1C jumps to the specified label
					//1D checks the item quantity?
					//1E is trigger victory, end parse
					//1F is trigger failure, end parse
					//20 sets and syncs state arg1
					//21 waits for specified item in inventory? or does it have to be carried like egg?
					//22 is a "share item check" (checks if all items are delivered)
					//23 sets the label to jump to when items are delivered
					//24 checks the enemy quantity
					//25 shows a message and sends packet (time over?? failed arg != 0)
					//26 sends a packet (alternate for win?? presuccess arg != 0)
					//28 skips to 29 unless ??? (argument is repel damage)
					//2A checks fatalis health, skips until 2B unless dead?
					//2C sets a variable (3C758F), the label to jump to on fort falling
					//2D waits for all players to report they've achieved victory
					//2E sets state arg3 after arg2 arg1s have been slain
					//2F changes the time over label
					//FFFE sends a packet (camerafail, arg 0)
					//FFFF does the wyvern kill cam, sends packet (camerasuccess, arg 0)
                    ushort v2 = readShort(); //goal ID (cap quests use "wyvern" item with deliver goal here)
                    uint v3 = readInt(); //goal quantity
                    /*if (v1 == 0xFFFF)
                        Debug.WriteLine("-1: Victory Camera");
                    else if (v1 == 0xFFFE)
                        Debug.WriteLine("-2: Loss Camera");
                    else if(v1 < scriptCodes.Count)
                        Debug.WriteLine(v1.ToString("X2") + ": " + scriptCodes[v1] + "; " + v2.ToString("X") + "; " + v3.ToString("X"));
                    else
                    {
                        Debug.WriteLine(v1.ToString("X4") + ": UNKNOWN; " + v2.ToString("X") + "; " + v3.ToString("X"));
                    }*/
                    if (v1 == 0xFFFF || v1 == 0xFFFE || v1 == 0x1F) //0xFFFF marks the end of a success condition, FFFE is the end of a fail condition maybe? 1F seems to be null section
                        conditions++;
                    if (conditions == 4)
                        break;
                }
                //Debug.WriteLine("----------------------");
            }

            //result text block
            filePos = (int)resultptr;
            {
                uint success = readInt();
                uint fail = readInt();
                //THERE CAN BE TWO MORE FOR PROGRESS! like on quests where you hunt 30 monsters, for the quantity remaining
                filePos = (int)success;
                //read it
                //readShort();
                while (true)
                {
                    uint v = readInt();
                    if ((v & 0xFF) == 0 | (v & 0xFF00) == 0 | (v & 0xFF0000) == 0 | (v & 0xFF000000) == 0)
                        break;
                }
                filePos = (int)fail;
                //read it
                while (true)
                {
                    ushort v = readByte();
                    if (v == 0)
                        break;
                }
            }

            //camp block? does this influence anything?
            filePos = (int)campptr;
            readInt(); //battle music
            readInt(); //map ID
            readInt(); //0?
            readInt(); //0?


            //map links block
            //has an int per room, only has pointers for rooms in the quest's area
            filePos = (int)linkptr;
            int roomsqty = (int)((originsptr - linkptr) / 0x4);
            {
                //the pointer table has entries for areas not in the locale until G
                List<uint> entries = new List<uint>();
                for (int i = 0; i < roomsqty; i++)
                {
                    uint val = readInt();
                    if (val != 0)
                        entries.Add(val);
                }
                for (int i = 0; i < entries.Count; i++)
                {
                    filePos = (int)entries[i];
                    while (true)
                    {
                        uint v = readInt();//map this links to
                        float x = readFloat(); //portal x
                        float y = readFloat(); //portal y
                        float z = readFloat(); //portal z
                        float w = readFloat(); //width
                        float h = readFloat(); //height
                        uint rx = readInt(); //unknown, rotation x?
                        uint ry = readInt(); //unknown, rotation y?
                        uint rz = readInt(); //unknown, rotation z?
                        float dx = readFloat(); //destination X
                        float dy = readFloat(); //destination Y
                        float dz = readFloat(); //destination Z
                        ushort orient = readShort(); //ending rotation
                        ushort unk = readShort(); //unk
                        if (v == 0xFFFFFFFF && x == -1)
                            break;
                        //Debug.WriteLine("Map link:" + i + " st" + v + ": " + x + ", " + y + ", " + z + ", " + w + " x " + h + ", " + rx + ", " + ry + ", " + rz + ", to " + dx + ", " + dy + ", " + dz + ", " + orient + ", " + unk);
                    }
                }
            }

            //origins block
            //these contain data about room positions and size
            //may contain data for invalid rooms, rooms not used in quest, and rooms in quest may not have an offset. all valid.
            filePos = (int)originsptr;
            {
                //may contain more entries than needed until G
                int length = (int)((boxptr - originsptr) / 0x20); //does NOT work for dos; should be the same quantity as map links block anyhow in 2
                if (game == Game.MH2)
                    length = roomsqty;
                for (int i = 0; i < length; i++)
                {
                    AreaOrigin origin = new AreaOrigin();
                    origin.x = readFloat(); //origin x
                    origin.y = readFloat(); //origin y
                    float x2 = readFloat();
                    float y2 = readFloat();
                    float w = readFloat(); //width
                    float l = readFloat(); //length
                    float h = readFloat(); //unknown, usually 0? sometimes 100
                    float unk = readFloat();
                    areaOrigins.Add(origin);
                    /*if (i == 33)
                    {
                        Debug.WriteLine("Cell " + i + " coords: " + ox + " " + oy + " " + x2 + " " + y2 + " | " + w + " " + l + " " + h + " " + unk);
                    }*/
                }
            }

            //box block
            loadBoxBlock(boxptr);

            if(game == Game.MH2)
            {
                //unknown block
                filePos = (int)foliageptr;
                {
                    List<ushort> counts = new List<ushort>();
                    List<uint> ptrs = new List<uint>();
                    while (true)
                    {
                        ushort map = readShort();//map ID
                        counts.Add(readShort()); //quantity
                        ptrs.Add(readInt());
                        if (map == 0xFFFF)
                            break;
                    }
                    for (int i = 0; i < ptrs.Count - 1; i++)
                    {
                        filePos = (int)ptrs[i];
                        for(int e = 0; e < counts[i]; e++)
                        {
                            readInt(); //???
                            readFloat(); //X
                            readFloat(); //Y
                            readFloat(); //Z
                        }
                    }
                }
                //gather pools block
                filePos = (int)gatherpoolsptr;
                {
                    List<uint> ptrs = new List<uint>();
                    while (true)
                    {
                        uint val = readInt();
                        if (val > 0x100000)
                            break;
                        ptrs.Add(val);
                    }
                    for(int i = 0; i < ptrs.Count; i++)
                    {
                        filePos = (int)ptrs[i];
                        while(true)
                        {
                            ushort chance = readShort();
                            if (chance == 0xFFFF)
                                break;
                            readShort();//the item ID! an ID of FFFF represents a "you got nothing" entry
                        }
                    }
                }

                //fishing block
                filePos = (int)fishingptr;
                {
                    List<uint> ptrs = new List<uint>();
                    while (true)
                    {
                        uint map = readInt();
                        uint ptr = readInt();
                        if (map == 0)
                            break;
                        ptrs.Add(ptr);
                    }
                    for(int i = 0; i < ptrs.Count; i++)
                    {
                        filePos = (int)ptrs[i];
                        readFloat(); //X
                        readFloat(); //Y
                        readFloat(); //Z
                        readFloat(); //radius?
                        readInt(); //no idea, which fish pool (heh) to draw from?
                        readInt(); //seems to be 5 here
                        readInt();
                        readInt();
                        readInt();
                        readInt(); //these be zeroes
                        readInt(); //FFFF FFFF
                        readInt(); //zero
                    }
                }
                //fish pools pointer
                filePos = (int)fishpoolsptr;
                {
                    List<uint> ptrs = new List<uint>();
                    for (int i = 0; i < 4; i++)
                        ptrs.Add(readInt());
                    for (int i = 0; i < 4; i++)
                    {
                        filePos = (int)ptrs[i];
                        List<uint> subptrs = new List<uint>();
                        for (int e = 0; e < 6; e++)
                        {
                            subptrs.Add(readInt()); //pointer to the fish chances
                            readInt(); //max fish in the pool
                        }
                        for(int e = 0; e < 6; e++)
                        {
                            filePos = (int)subptrs[e];
                            while(true)
                            {
                                byte val = readByte(); //chance
                                if (val == 0xFF)
                                    break;
                                readByte(); //Fishie ID, these do NOT match item IDs!
                                            //00 is nothing, 01 is whetfish, 02 is sushifish, 03 is pin tuna, 04 is popfish, 05 is goldenfish, 06 is sleepyfish, 07 is burst arowana,
                                            //08 is bomb arowana, 09 is scatterfish, 0A is speartuna, 0B is gast. tuna, 0C is ancient fish, 0D is onpuuo (11C), 0E is snakesalmon, 0F is queen shrimp,
                                            //10 is g whetfish, 11 is small goldenfish, 12 is silverfish, 13 is g sleepyfish, 14 is g pin tuna, 15 is g popfish, 16 is g arowana, 17 is g gast. tuna. 18 is ---------
                            }
                        }
                    }
                }
            }

            //gathering block
            filePos = (int)gatherptr;
            {
                uint p1 = readInt(); //special entry
                List<uint> ptrs = new List<uint>();
                int length = 0x5A;
                if (game == Game.MH2)
                    length = roomsqty - 1; //for now
                for (int i = 0; i < length; i++) //always has the maximum amount... in dos this reuses the quantity of one of the earlier tables? links?
                {
                    uint p = readInt();
                    ptrs.Add(p);
                }
                if (game != Game.MH2)
                {
                    filePos = (int)p1;
                    filePos = (int)readInt();
                    while (true)
                    {
                        //for MH1J, this is always          0019 FFFF 004B 004E FFFF 0000
                        //for MH1U, this is always          0019 FFFF 004B 007D FFFF 0000
                        //for MHGP and MHGW, this is always 0019 FFFF 004B 008D FFFF 0000
						//basically, 25% nothing, 75% nitroshroom
                        //doesn't exist in MH2
                        ushort v = readShort();
                        readShort();
                        if (v == 0xFFFF)
                            break;
                    }
                }
                for (int i = 0; i < ptrs.Count; i++)
                {
                    if (ptrs[i] != 0)
                    {
                        filePos = (int)ptrs[i];
                        while (true)
                        {
                            GatheringSpot spot = new GatheringSpot();
                            spot.mapID = i;
                            spot.x = readFloat(); //X
                            spot.y = readFloat(); //Y
                            spot.z = readFloat(); //Z
                            spot.radius = readFloat(); //radius
                            spot.itemPool = readShort(); //7F ---- A0 is Ballista ammo? item pools..... different between G and MH1
                            spot.maxQuantity = readShort(); //max gather quantity - finding nothing doesn't decrement this!!
                            spot.type = (GatherType)readShort(); //requirement - 3 is pickaxe, 4 is bug net
                            spot.extra = readShort(); //always zero?
                            if (spot.x == -1)
                                break;
                            /*if (ConstantsLocales.LocaleRooms[(int)locale].Contains(i))
                            {
                                int index = i;
                                if (dataptr == 0x48)
                                    index = ConstantsLocales.LocaleRooms[(int)locale].IndexOf(i);
                                Debug.WriteLine("gather entry map " + i + ": " + (spot.x + areaOrigins[index].x) + "f, " + spot.y + "f, " + (spot.z + areaOrigins[index].y) + "f, radius " + spot.radius + "f, item pool " + spot.itemPool.ToString("X") + ", quantity " + spot.maxQuantity.ToString("X") + ", type " + spot.type.ToString() + " extra " + spot.extra);
                                pins.Add(new ConstantsLocales.Pushpin { x = spot.x + areaOrigins[index].x, y = spot.z + areaOrigins[index].y, type = (int)spot.type });
                            }*/
                            //Debug.WriteLine("gather entry map " + i + ": " + (spot.x) + "f, " + spot.y + "f, " + (spot.z) + "f, radius " + spot.radius + "f, item pool " + spot.itemPool.ToString("X") + ", quantity " + spot.maxQuantity.ToString("X") + ", type " + spot.type.ToString() + " extra " + spot.extra);
                            gatheringSpots.Add(spot);
                            
                        }
                    }
                }
            }
            //static map objects block
            //items are: 1 - bbq spit, 2 - fishing spot, 3 - item box, 4 - stolen items, 10 - bed, 11 - ballista, 15 - delivery box, 18 - cannon, 19 - dragonator
            filePos = (int)mapobjptr;
            {
                List<uint> ptrs = new List<uint>();
                int length = 0x5A;
                if (game == Game.MH2)
                    length = roomsqty;
                for (int i = 0; i < length; i++) //check entries for each map; table size is static through G
                    ptrs.Add(readInt());
                for (int i = 0; i < ptrs.Count; i++)
                {
                    if (ptrs[i] != 0)
                    {
                        filePos = (int)ptrs[i]; //navigate to the room
                        while (true)
                        {
                            ushort s1 = readShort(); //locale state condition?
                            ushort s2 = readShort(); //object ID
                            float f1 = readFloat(); //X
                            float f2 = readFloat(); //Y
                            float f3 = readFloat(); //Z
                            float f4 = readFloat(); //radius
                            ushort s3 = readShort(); //rotation
                            ushort s4 = readShort(); //unknown
                            if (s2 == 0 && s4 == 0)
                                break;
                            //Debug.WriteLine("MapObjects entry, room " + i + ": " + s1.ToString("X") + " " + s2.ToString("X") + ", " + f1 + ", " + f2 + ", " + f3 + ", " + f4 + ", " + s3.ToString("X") + " " + s4.ToString("X"));
                        }
                    }
                }
            }

            //rewards block
            loadRewardBlock(rewardsptr);

            //done reading file?
            //Debug.WriteLine("done reading file, now checking coverage");
            uint gap = 0;
            if (checkcoverage)
                for (int i = 0; i < coverage.Count; i++)
                {
                    if (coverage[i] == 0)
                        gap++;
                    else if (gap != 0)
                    {
                        Debug.WriteLine("gap in coverage from " + (i - gap).ToString("X") + " to " + i.ToString("X") + " (size " + gap.ToString("X") + ")");
                        gap = 0;
                    }
                    //commenting this out because some quests reuse ent tables
                    if (coverage[i] > 1)
                        Debug.WriteLine("read address " + i.ToString("X") + " " + coverage[i] + " times!");
                }

            /*Pen entpen = new Pen(new SolidBrush(Color.Yellow));
            Pen monstpen = new Pen(new SolidBrush(Color.Red));
            for (int i = 0; i < pins.Count; i++)
            {
                g.DrawEllipse(monstpen, pins[i].X / 100 + 100, pins[i].Y / 100 + 100, 3, 3);
                Debug.WriteLine("entity position: " + pins[i].X + ", " + pins[i].Y);
            }*/


            Debug.WriteLine("Online: " + online.ToString("X") + " stars: " + stars + " diff: " + pattern + " roll? " + ((fulu&0b01000000) == 0b01000000?"n":"y") + " base scale: " + size + " mons: ");
            string[] mons = new string[] { "rathian", "fatalis", "kelbi", "mosswine", "bullfango", "yian kut ku", "lao shan lung", "cephadrome", "felyne", "mountain herb ojiisan", "rathalos", "aptonoth", "genprey", "diablos", "khezu", 
                "velociprey", "gravios", "???", "vespoid", "gary", "plesioth", "basarios", "melynx", "hornetaur", "apceros", "monoblos", "velocidrome", "gendrome", "ROCK", "ioprey", "iodrome", "pugi", "kirin", "cephalos",
            "giaprey", "c.fatalis", "p.rathian", "b.yian kut ku", "p.gary", "uhhhh???", "s.rathalos", "g.rathian", "b.diablos", "w.monoblos", "r.khezu", "g.plesioth", "b.gravios", "w.basarios", "a.rathalos", "a.lao shan lung"};
            for (int m = 0; m < montypes.Count; m++)
                Debug.Write(mons[montypes[m]-1] + " ");
            Debug.Write("\n");
            //if (qtype != 1 && qtype != 2 && qtype != 4 && qtype != 9)
            //Debug.WriteLine("quest type: " + qtype);
            //if (fulu != 0)
            //    Debug.WriteLine("fulu: " + fulu.ToString("X"));
        }

        void loadPlayerBlock(uint offset)
        {
            filePos = (int)offset;

            for (int i = 0; i < 4; i++)
            {
                readInt(); //starting room ID
                readFloat(); //X
                readFloat(); //Y
                readFloat(); //Z
            }
        }

        void loadBoxBlock(uint offset)
        {
            filePos = (int)offset;

            if (game != Game.MH2)
                while (true)
                {
                    ushort v = readShort(); //item ID
                    readShort(); //item quantity
                    if (v == 0)
                        break;
                }
            else
            {
                for (int i = 0; i < 24; i++) //starting items
                {
                    readShort(); //item ID
                    readShort(); //quantity
                }
                for (int i = 0; i < 8; i++) //subquest A rewards
                {
                    readShort(); //item ID
                    readShort(); //quantity
                }
                for (int i = 0; i < 8; i++) //subquest B rewards
                {
                    readShort(); //Item ID
                    readShort(); //quantity
                }
            }
        }


        public struct QuestRewardItem
        {
            public ushort chance;
            public ushort itemID;
            public ushort amount;
        }


        void loadRewardBlock(uint offset)
        {
            filePos = (int)offset;

            List<QuestRewardItem> questRewardItems = new List<QuestRewardItem>();

            {
                // Reward pointers
                List<uint> ptrs = new List<uint>();
                while (true)
                {
                    uint v1 = readInt(); //reward type; 0x8000 mh1 quest complete, 1 head break? 4 wing/back break? 5 claw break? 21-25 delivery rewards (multiples of 5 starting at 0.).
                                         //11 - 15 are repel rewards? 2 - 20 are 3C758C bit (#-2). 0 is guaranteed drop, 1 is any part break?
                                         //so for specific part breaks i need to figure out how the bits are set
                                         //MH2: 8001 for main objective, 8002 for sub a, 8003 for sub b
                    uint v2 = readInt(); //reward pointer
                    if (v1 == 0xFFFF)
                        break;
                    ptrs.Add(v2);
                }

                // Get all possible quest reward items
                for (int i = 0; i < ptrs.Count; i++)
                {
                    filePos = (int)ptrs[i];
                    while (true)
                    {
                        QuestRewardItem questRewardItem = new QuestRewardItem();

                        ushort chance = readShort();
                        if (chance == 0xFF) // Is it 0xFFFF in MH2 or other game?
                            break;

                        questRewardItem.chance = chance;
                        questRewardItem.itemID = readShort();
                        questRewardItem.amount = readShort();

                        questRewardItems.Add(questRewardItem);
                    }
                    if ((filePos % 4) == 2)
                        readShort(); //align, only happens sometimes?
                }
            }
        }
    }
}
