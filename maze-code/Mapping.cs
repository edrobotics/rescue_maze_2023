using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SerialConsole
{
    enum RampStorage
    {
        XCoord,
        ZCoord,
        RampDirection,
        RampIndex,
        ConnectedMap
    }

    enum BitLocation
    {
        frontWall,
        leftWall,
        backWall,
        rightWall,
        explored,
        mapSearched,
        victim,
        ramp,
        blackTile,
        checkPointTile,
        blueTile,
        inDriveWay
    }

    internal partial class Program
    {
        //******** Mapping & Checkpoints ********

        //static readonly ushort[,] mainMap = new ushort[50, 50]; //W = 0, A = 1, S = 2, D = 3, explored = 4, mapSearched = 5, victim  = 6, ramp = 7, black tile = 8, checkp = 9, blue = 10, crossroad = 11 -more? obstacles?
        static readonly List<byte[]> sinceCheckpoint = new();
        static readonly List<Map> maps = new();
        //static readonly List<int[]> mapInfo = new(); //For storing info of where the ramp is
        static int currentMap = 0;
        static int rampCount = 0;
        static int savedRampCount = 0;

        static bool reset = false;
        static int savedMapCount = 1;


        // ********************************** Map And Localization Updates ********************************** 

        static void UpdateDirection(int _leftTurns)
        {
            direction += _leftTurns;

            while (direction > 3)
            {
                direction -= 4;
            }
            while (direction < 0)
            {
                direction += 4;
            }
        }

        static int FixDirection(int dir)
        {
            while (dir > 3)
            {
                dir -= 4;
            }
            while (dir < 0)
            {
                dir += 4;
            }
            return dir;
        }

        static int TileToDirection(byte[] cell)
        {
            if (cell[0] == posX && cell[1] == posZ - 1)
            {
                return 0;
            }
            if (cell[0] == posX - 1 && cell[1] == posZ)
            {
                return 1;
            }
            if (cell[0] == posX && cell[1] == posZ + 1)
            {
                return 2;
            }
            if (cell[0] == posX + 1 && cell[1] == posZ)
            {
                return 3;
            }

            for (int i = 0; i < 5; i++) Log("********* ACHTUNG ACHTUNG, DAS IST NICHT GUT ************", true);
            return 0;
        }

        static byte[] DirToTile(int _globalDir, byte _x, byte _z)
        {
            _globalDir = FixDirection(_globalDir);
            if (_globalDir == 0)
            {
                _z -= 1;
            }
            if (_globalDir == 1)
            {
                _x -= 1;
            }
            if (_globalDir == 2)
            {
                _z += 1;
            }
            if (_globalDir == 3)
            {
                _x += 1;
            }
            return new byte[] { _x, _z };
        }

        static bool ShouldGoTo(int _posX, int _posZ)
        {
            return !maps[currentMap].ReadBit(_posX, _posZ, BitLocation.explored) && !maps[currentMap].ReadBit(_posX, _posZ, BitLocation.blackTile);
        }

        static void UpdateLocation() //ONLY USE AFTER YOU KNOW THERE IS A NEW CELL
        {
            if (locationUpdated)
            {
                return;
            }
            locationUpdated = true;

            if (direction == 0)
            {
                posZ--;
                if (posZ < lowestZ)
                    lowestZ = posZ;
            }
            else if (direction == 1)
            {
                posX--;
                if (posX < lowestX)
                    lowestX = posX;
            }
            else if (direction == 2)
            {
                posZ++;
                if (posZ > highestZ)
                    highestZ = posZ;
            }
            else if (direction == 3)
            {
                posX++;
                if (posX > highestX)
                    highestX = posX;
            }
            else
            {
                for(int i = 0; i < 5; i++) Log("Something wrong with direction in UpdateLocation!!!", true);
            }

            if (posX > maps[currentMap].Length)
            {
                posX = maps[currentMap].Length;
                Log("Error PosX", true);
            }
            if (posX < 0)
            {
                posX = 0;
                Log("Error posX", true);
            }
            if (posZ > maps[currentMap].Length)
            {
                posZ = maps[currentMap].Length;
                Log("Error PosZ", true);
            }
            if (posZ < 0)
            {
                posZ = 0;
                Log("Error posZ", true);
            }

            if (posX == maps[currentMap].StartPosX && posZ == maps[currentMap].StartPosZ && currentMap == 0 && timer.ElapsedMilliseconds > (7 * 60 + 30) * 1000)
            {
                Thread.Sleep(30_000);
            }
        }

        static void UpdateMap()//Behind - Always false when driving normally
        {
            SensorCheck();

            bool wallNZ;
            bool wallNX;
            bool wallPZ;
            bool wallPX;
            if (direction == 0)
            {
                //SensorCheck();
                wallNZ = frontPresent; //front is front
                wallNX = leftPresent;
                wallPZ = false;
                wallPX = rightPresent;
            }
            else if (direction == 1)
            {
                //SensorCheck();
                wallNZ = rightPresent; //right is front
                wallNX = frontPresent;
                wallPZ = leftPresent;
                wallPX = false;
            }
            else if (direction == 2)
            {
                //SensorCheck();
                wallNZ = false; //back is front
                wallNX = rightPresent;
                wallPZ = frontPresent;
                wallPX = leftPresent;
            }
            else if (direction == 3)
            {
                //SensorCheck();
                wallNZ = leftPresent; //left is front
                wallNX = false;
                wallPZ = rightPresent;
                wallPX = frontPresent;
            }
            else
            {
                wallNZ = false;
                wallNX = false;
                wallPZ = false;
                wallPX = false;
                Log("ERROR", true);
            }

            if (ReadHere(BitLocation.explored))
            {
                if (!(wallNZ == ReadHere(BitLocation.frontWall) &&
                    wallNX == ReadHere(BitLocation.leftWall) &&
                    wallPZ == ReadHere(BitLocation.backWall) &&
                    wallPX == ReadHere(BitLocation.rightWall)))
                {
                    for (int i = 0; i < 5; i++) Log("!!! Error in mapping, probably !!!", true);
                }
            }

            WriteHere(BitLocation.frontWall, wallNZ);
            WriteHere(BitLocation.leftWall, wallNX);
            WriteHere(BitLocation.backWall, wallPZ);
            WriteHere(BitLocation.rightWall, wallPX);
            WriteHere(BitLocation.explored, true);

            /*if (posZ - 1 >= 0 && !ReadMapBit(posX, posZ - 1, explored)) //If there is an unexplored position one +Z
            {
                SetMapBit(posX, posZ - 1, 0b00000100, wallPZ);
            }
            if (posX - 1 >= 0 && !ReadMapBit(posX - 1, posZ, explored)) //If there is an unexplored position one step -X
            {
                SetMapBit(posX - 1, posZ, 0b00001000, wallNX);
            }
            if (posZ + 1 < byteMap.GetLength(1) && !ReadMapBit(posX, posZ + 1, explored)) //If there is an unexplored position one step -Z
            {
                SetMapBit(posX, posZ + 1, 0b00000001, wallNZ);
            }
            if (posX + 1 < byteMap.GetLength(0) && !ReadMapBit(posX + 1, posZ, explored)) //If there is an unexplored position one step forward
            {
                SetMapBit(posX + 1, posZ, 0b00000010, wallPX);
            }*/

            Log(posX + " , " + posZ + ": ", true);

            string _log = "";
            for (int i = 15; i >= 0; i--)
            {
                _log += ReadHere((BitLocation)i) ? "1" : "0";
            }
            Log(_log, false);
        }

        static void UpdateMapFull()
        {
            UpdateMap();
            Turn('l');
            maps[0].WriteBit(posX, posZ, BitLocation.backWall, leftPresent);
            Turn('r');
        }


        // ********************************** Checkpoints ********************************** 

        static void Reset()
        {
            Log("Reset", true);
            reset = true;
            try
            {
                serialPort1.DiscardInBuffer();
                serialPort1.DiscardOutBuffer();
                driveWay.Clear();

                rampCount = savedRampCount;
                while (savedMapCount < maps.Count)
                {
                    maps.RemoveAt(maps.Count - 1);
                }
                foreach (Map map in maps)
                {
                    map.ResetInfo();
                }

                if (sinceCheckpoint.Count > 0)
                {
                    byte[] _checkpointXZ = sinceCheckpoint[0];
                    posX = _checkpointXZ[0];
                    posZ = _checkpointXZ[1];
                    currentMap = _checkpointXZ[2];
                    direction = _checkpointXZ[3];

                    Log("Started reset", true);
                    for (int i = 0; i < sinceCheckpoint.Count; i++)
                    {
                        byte[] _coords = sinceCheckpoint[0];
                        maps[_coords[2]].ClearTile(_coords[0], _coords[1], BitLocation.victim);
                        sinceCheckpoint.RemoveAt(0);
                    }
                }
            }
            catch (Exception e)
            {
                Log($"Reset error, {e.Message}", true);
            }

            Thread.Sleep(100);
            serialPort1.DiscardInBuffer();
            serialPort1.DiscardOutBuffer();

            dropKits = false;
            //SerialComm("!w");
            Thread.Sleep(100);
        }

        static void AddSinceCheckPoint()
        {
            if (sinceCheckpoint.Count == 0)
            {
                sinceCheckpoint.Add(new byte[] { (byte)posX, (byte)posZ, (byte)currentMap, (byte)direction });
            }
            else if (!ReadHere(BitLocation.explored))
            {
                sinceCheckpoint.Add(new byte[] { (byte)posX, (byte)posZ, (byte)currentMap });
            }
        }

        static void ResetSinceCheckpoint()
        {
            sinceCheckpoint.Clear();
            savedMapCount = maps.Count;
            savedRampCount = rampCount;
            foreach (Map map in maps)
            {
                map.SaveInfo();
            }
        }
        
        // ********************************** Read And Write Map ********************************** 

        static void WriteNextTo(BitLocation _write, bool value, Directions _toDirection)
        {
            switch (FixDirection(direction + (int)_toDirection))
            {
                case 0:
                    maps[currentMap].WriteBit(posX, posZ - 1, _write, value);
                    break;
                case 1:
                    maps[currentMap].WriteBit(posX - 1, posZ, _write, value);
                    break;
                case 2:
                    maps[currentMap].WriteBit(posX, posZ + 1, _write, value);
                    break;
                case 3:
                    maps[currentMap].WriteBit(posX + 1, posZ, _write, value);
                    break;
                default:
                    Log("WriteNextTo Direction error ", true);
                    break;
            }
            
            //if (FixDirection(direction + (int)_toDirection) == 0/*(direction == 0 && _toDirection == Directions.front) || (direction == 1 && _toDirection == Directions.right) || (direction == 2 && _toDirection == Directions.back) || (direction == 3 && _toDirection == Directions.left)*/)
            //{
            //    maps[currentMap].WriteBit(posX, posZ - 1, _write, value);
            //}
            //else if (FixDirection(direction + (int)_toDirection) == 1/*(direction == 0 && _toDirection == Directions.left) || (direction == 1 && _toDirection == Directions.front) || (direction == 2 && _toDirection == Directions.right) || (direction == 3 && _toDirection == Directions.back)*/)
            //{
            //    maps[currentMap].WriteBit(posX - 1, posZ, _write, value);
            //}
            //else if (FixDirection(direction + (int)_toDirection) == 2/*(direction == 0 && _toDirection == Directions.back) || (direction == 1 && _toDirection == Directions.left) || (direction == 2 && _toDirection == Directions.front) || (direction == 3 && _toDirection == Directions.right)*/)
            //{
            //    maps[currentMap].WriteBit(posX, posZ + 1, _write, value);
            //}
            //else if (FixDirection(direction + (int)_toDirection) == 3/*(direction == 0 && _toDirection == Directions.right) || (direction == 1 && _toDirection == Directions.back) || (direction == 2 && _toDirection == Directions.left) || (direction == 3 && _toDirection == Directions.front)*/)
            //{
            //    maps[currentMap].WriteBit(posX + 1, posZ, _write, value);
            //}
            //else
            //{
            //    Console.WriteLine("ReadNextTo Direction error ");
            //}
        }

        static bool ReadNextTo(BitLocation _read, Directions _toDirection)
        {
            switch (FixDirection(direction + (int)_toDirection))
            {
                case 0:
                    return maps[currentMap].ReadBit(posX, posZ - 1, _read);
                case 1:
                    return maps[currentMap].ReadBit(posX - 1, posZ, _read);
                case 2:
                    return maps[currentMap].ReadBit(posX, posZ + 1, _read);
                case 3:
                    return maps[currentMap].ReadBit(posX + 1, posZ, _read);
                default:
                    Log("ReadNextTo Direction error ", true);
                    return false;
            }
        }
        /*if ((direction == 0 && _toDirection == Directions.front) || (direction == 1 && _toDirection == Directions.right) || (direction == 2 && _toDirection == Directions.back) || (direction == 3 && _toDirection == Directions.left))
        {
            return ReadMapBit(_x, _z - 1, _read);
        }
        else if ((direction == 0 && _toDirection == Directions.left) || (direction == 1 && _toDirection == Directions.front) || (direction == 2 && _toDirection == Directions.right) || (direction == 3 && _toDirection == Directions.back))
        {
            return ReadMapBit(_x - 1, _z, _read);
        }
        else if ((direction == 0 && _toDirection == Directions.back) || (direction == 1 && _toDirection == Directions.left) || (direction == 2 && _toDirection == Directions.front) || (direction == 3 && _toDirection == Directions.right))
        {
            return ReadMapBit(_x, _z + 1, _read);
        }
        else if ((direction == 0 && _toDirection == Directions.right) || (direction == 1 && _toDirection == Directions.back) || (direction == 2 && _toDirection == Directions.left) || (direction == 3 && _toDirection == Directions.front))
        {
            return ReadMapBit(_x + 1, _z, _read);
        }
        else
        {
            Console.WriteLine("ReadNextTo Direction error ");
        }*/

        /// <summary>Writes a bit on the current map and current position </summary>
        static void WriteHere(BitLocation _write, bool _value)
        {
            maps[currentMap].WriteBit(posX, posZ, _write, _value);
        }

        /// <summary>Reads a bit on the current map and current position </summary>
        static bool ReadHere(BitLocation _read)
        {
            return maps[currentMap].ReadBit(posX, posZ, _read);
        }
    }

    class Map
    {
        // ********************************** Data - Variables And Objects ********************************** 
        public Map(int _mazeLength)
        {
            Length = _mazeLength;
            map = new ushort[_mazeLength, _mazeLength];
            StartPosX = _mazeLength / 2;
            StartPosZ = _mazeLength / 2;
        }
        public int Length;
        public int StartPosX;
        public int StartPosZ;

        ushort[,] map;
        List<byte[]> reachedFrom = new();
        List<byte[]> saveReached = new();
        List<byte[]> crossTiles = new(); //byte[0].Length = 2; 0 posX, 1 posZ 
        List<byte[]> saveCross = new();

        public ref ushort[,] GetMap
        {
            get => ref map;
        }

        public ref List<byte[]> CrossTiles
        {
            get => ref crossTiles;
        }

        // ********************************** Data - Methods ********************************** 

        public void AddRamp(byte _x, byte _z, byte _direction, byte _rampIndex, byte _connectedMap)
        {
            byte[] _info = new byte[5];
            _info[(int)RampStorage.XCoord] = _x; _info[(int)RampStorage.ZCoord] = _z; _info[(int)RampStorage.RampDirection] = _direction; 
            _info[(int)RampStorage.RampIndex] = _rampIndex; _info[(int)RampStorage.ConnectedMap] = _connectedMap;
            reachedFrom.Add(_info);
        }

        public byte[] GetRampAt(byte _x, byte _z)
        {
            for (int i = 0; i < reachedFrom.Count; i++)
            {
                if (reachedFrom[i][0] == _x && reachedFrom[i][1] == _z)
                {
                    return reachedFrom[i];
                }
            }
            return new byte[] { 250, 250, 250, 250 };
        }

        public byte[] GetRampAt(byte _index)
        {
            for (int i = 0; i < reachedFrom.Count; i++)
            {
                if (reachedFrom[i][3] == _index)
                {
                    return reachedFrom[i];
                }
            }
            return new byte[] { 250, 250, 250, 250, 250 };
        }

        public bool FindRamp(byte _x, byte _z)
        {
            for (int i = 0; i < reachedFrom.Count; i++)
            {
                if (reachedFrom[i][0] == _x && reachedFrom[i][1] == _z)
                {
                    return true;
                }
            }
            return false;
        }

        public void SaveInfo()
        {
            saveCross = new List<byte[]>(crossTiles);
            saveReached = new List<byte[]>(reachedFrom);
        }

        public void ResetInfo()
        {
            crossTiles = new List<byte[]>(saveCross);
            reachedFrom = new List<byte[]>(saveReached);
        }

        // ********************************** Read And Write Map ********************************** 

        public bool ReadBit(int _x, int _z, BitLocation _read)
        {
            return ((map[_x, _z] >> (int)_read) & 0b1) == 1;
        }

        public void WriteBit(int _x, int _z, BitLocation _write, bool _value)
        {
            ushort _t = (ushort)(0b1 << (int)_write);
            if (_value)
            {
                map[_x, _z] = (ushort)(map[_x, _z] | (_t));
            }
            else
            {
                map[_x, _z] = (ushort)(map[_x, _z] & ~_t);
            }
        }

        public void Clear()
        {
            for (int i = 0; i < Length; i++)
            {
                for (int j = 0; j < Length; j++)
                {
                    map[i, j] = 0;
                }
            }
        }

        public void ClearBit(BitLocation _bit)
        {
            for (int i = 0; i < Length; i++)
            {
                for (int j = 0; j < Length; j++)
                {
                    WriteBit(i, j, _bit, false);
                }
            }
        }

        public void ClearTile(int _x, int _z)
        {
            map[_x, _z] = 0;
        }

        public void ClearTile(int _x, int _z, BitLocation _exception)
        {
            if (ReadBit(_x, _z, _exception))
            {
                map[_x, _z] = 0;
                WriteBit(_x, _z, _exception, true);
            }
            else
            {
                map[_x, _z] = 0;
            }
        }
    }
}
