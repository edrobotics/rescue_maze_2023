// |||| NAVIGATION - Class which contains all the mapping stuff, as well as paths between tiles in the same area ||||
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Mapping
{
    #region Misc
    enum RampStorage
    {
        XCoord,
        ZCoord,
        RampDirection,
        RampIndex,
        ConnectedMap,
        RampLength
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
        inDriveWay,
        tileAdded
    }

    [Serializable]
    public class NonexistantRampException : Exception
    {
        public NonexistantRampException()
            : base("This ramp does not exist")
        { }

        public NonexistantRampException(string message)
            : base(message)
        { }

        public NonexistantRampException(string message, Exception innerException)
            : base(message, innerException)
        { }
    }
    #endregion

    /// <summary>
    /// Class for storing map data, ramp data, and methods that use the map
    /// </summary>
    class Map
    {
        #region Variables, objects, initializers and indexers
        //**Data, mapping**
        public Map (int _mazeLength, int _height, int _startPosX, int _startPosZ)
        {
            Length = _mazeLength;
            map = new ushort[_mazeLength, _mazeLength];
            StartPosX = _startPosX;
            StartPosZ = _startPosZ;
            height = _height;
        }

        public bool this[int _index1, int _index2, BitLocation _bit]
        {
            get => ReadBit(_index1, _index2, _bit);
            set => WriteBit(_index1, _index2, _bit, value);
        }

        /// <summary>The size of the map in tile amount</summary>
        public int Length { get;}

        private int height;
        /// <summary>The height of the map from the starting map in cm</summary>
        public int Height { get => height; }
        public bool[] HasStrangeRamps { get; set; } = new bool[] {false, false, false, false};
        public int StartPosX { get;}
        public int StartPosZ { get;}

        ushort[,] map;

        List<byte[]> reachedFrom = new();
        List<byte[]> saveReached = new();
        List<byte[]> crossTiles = new(); //byte[0].Length = 2; 0 posX, 1 posZ 
        List<byte[]> saveCross = new();

        List<List<byte[]>> areas = new();
        List<List<byte[]>> saveAreas = new();


        public ushort[,] GetMap
        {
            get => map;
            set => map = value;
        }

        /// <summary>
        /// The unexplored tiles that I have been next to, will be explored later
        /// </summary>
        public ref List<byte[]> CrossTiles
        {
            get => ref crossTiles;
        }

        public ref List<byte[]> Ramps
        {
            get => ref reachedFrom;
        }

        public ref List<List<byte[]>> Areas
        {
            get => ref areas;
        }

        //**Searching**
        bool skipNext;
        bool returningFromGoal;
        bool foundWay;
        int toPosX;
        int toPosZ;
        int fromPosX;
        int fromPosZ;

        List<byte[]> foundPath = new();
        List<byte[]> savedPath = new();

        readonly List<byte[]> extraBits = new();
        #endregion

        #region General data handling
        // ********************************** Data - Methods ********************************** 

        public void UpdateHeight(int _newHeight)
        {
            height = (_newHeight + height) / 2;
        }

        public void UpdateCrossTiles()
        {
            for (int i = 0; i < crossTiles.Count; i++)
            {
                if (ReadBit(crossTiles[i][0], crossTiles[i][1], BitLocation.explored))
                {
                    crossTiles.RemoveAt(i);
                    i--;
                }
            }
        }

        public void AddArea()
        {
            areas.Add(new List<byte[]>());
        }

        public void MergeAreas(int _fromArea, int _toArea)
        {
            areas[_toArea].AddRange(areas[_fromArea]);
            areas[_fromArea].Clear();
        }

        public bool IsInArea(byte[] _tile, int _area)
        {
            for (int i = 0; i < areas[_area].Count; i++)
            {
                if (_tile == areas[_area][i])
                {
                    return true;
                }
            }
            return false;
        }

        public bool IsSameArea(byte[] _tile1, byte[] _tile2)
        {
            return GetArea(_tile1) == GetArea(_tile2);
        }

        public int GetArea(byte[] _tile)
        {
            for (int i = 0; i < areas.Count; i++)
            {
                for (int j = 0; j < areas[i].Count; j++)
                {
                    if (_tile[0] == areas[i][j][0] && _tile[1] == areas[i][j][1])
                    {
                        return i;
                    }
                }
            }
            SerialConsole.Program.Log("!_!-!| Current tile is not added to area |!-!_!", true);
            return -1;
        }

        public void AddRamp(byte _x, byte _z, byte _direction, byte _rampIndex, byte _connectedMap, byte _rampLength)
        {
            reachedFrom.Add(new byte[6] { _x, _z, _direction, _rampIndex, _connectedMap, _rampLength });
        }

        /// <summary>
        /// Gets a ramp by coordinates and direction
        /// </summary>
        /// <returns>The ramp info</returns>
        public byte[] GetRampAt(byte _x, byte _z, byte _direction)
        {
            for (int i = 0; i < reachedFrom.Count; i++)
            {
                if (reachedFrom[i][(int)RampStorage.XCoord] == _x && reachedFrom[i][(int)RampStorage.ZCoord] == _z 
                    && _direction == reachedFrom[i][(int)RampStorage.RampDirection])
                {
                    return reachedFrom[i];
                }
            }
            throw new NonexistantRampException();
        }


        /// <summary>
        /// Gets a ramp by index
        /// </summary>
        /// <returns>The ramp info</returns>
        public byte[] GetRampAt(byte _index)
        {
            for (int i = 0; i < reachedFrom.Count; i++)
            {
                if (reachedFrom[i][(int)RampStorage.RampIndex] == _index)
                {
                    return reachedFrom[i];
                }
            }
            throw new NonexistantRampException();
        }

        /// <summary>
        /// Tries to find a ramp on certain position
        /// </summary>
        /// <returns>true if it finds a ramp</returns>
        public bool FindRamp(byte _x, byte _z, byte _direction)
        {
            for (int i = 0; i < reachedFrom.Count; i++)
            {
                if (reachedFrom[i][(int)RampStorage.XCoord] == _x && reachedFrom[i][(int)RampStorage.ZCoord] == _z
                    && _direction == reachedFrom[i][(int)RampStorage.RampDirection])
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Makes this bit get removed on reset
        /// </summary>
        public void AddBitInfo(byte[] _xz, BitLocation _bit)
        {
            byte[] _info = new byte[] { _xz[0], _xz[1], (byte)_bit};
            extraBits.Add(_info);
        }

        /// <summary>
        /// Save crosstile and ramp data
        /// </summary>
        public void SaveInfo()
        {
            saveCross = new List<byte[]>(crossTiles);
            saveReached = new List<byte[]>(reachedFrom);
            saveAreas = new List<List<byte[]>>(areas);
            extraBits.Clear();
        }

        /// <summary>
        /// Reset crossTile and ramp data to their saved states
        /// </summary>
        public void ResetInfo()
        {
            crossTiles = new List<byte[]>(saveCross);
            reachedFrom = new List<byte[]>(saveReached);
            areas = new List<List<byte[]>>(saveAreas);
            foreach (byte[] _extrabit in extraBits)
            {
                WriteBit(_extrabit[0], _extrabit[1], (BitLocation)_extrabit[2], false);
            }
            extraBits.Clear();
        }
        #endregion

        #region Map data handling
        // ********************************** Read And Write Map ********************************** 

        /// <summary>
        /// Checks if a certain bit exists on a certain location
        /// </summary>
        /// <returns>true if the bit was there</returns>
        public bool ReadBit(int _x, int _z, BitLocation _read)
        {
            return ((map[_x, _z] >> (int)_read) & 0b1) == 1;
        }

        /// <summary>
        /// Write one bit on a certain location (x and z) to a certain value
        /// </summary>
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

        /// <summary>
        /// Sets all bits in the map to 0
        /// </summary>
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

        /// <summary>
        /// Clears one bit from the entire map
        /// </summary>
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

        /// <summary>
        /// Sets a tile's value to 0
        /// </summary>
        public void ClearTile(int _x, int _z)
        {
            map[_x, _z] = 0;
        }

        /// <summary>
        /// Sets a tile's value to 0, with the exception of the _exception bit
        /// </summary>
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
        #endregion

        #region Pathfinding
        // ********************************** Searching ********************************** 

        /// <summary>
        /// Find a path from a certain tile to another tile
        /// </summary>
        /// <returns>A list containing the found path, empty if no path was found</returns>
        public List<byte[]> PathTo(int _toX, int _toZ, int _fromX, int _fromZ)
        {
            foundPath = new List<byte[]>();
            if (_toX == _fromX && _toZ == _fromZ)
                return foundPath;

            toPosX = _toX;
            toPosZ = _toZ;
            fromPosX = _fromX;
            fromPosZ = _fromZ;

            foundWay = false;
            skipNext = false;
            returningFromGoal = false;
            //shortenAvailable = false;
            FindFrom((byte)_fromX, (byte)_fromZ);

            if ((foundPath.Count > savedPath.Count || foundPath.Count == 0) && savedPath.Count > 0)
            {
                foundPath = savedPath;
            }

            //driveWay.ForEach(num => Debug.Log(num.X + " , " + num.Z + " ; "));
            //SerialConsole.Program.Log($"Found PathTo {_toX},{_toZ} from {_fromX},{_fromZ} at {foundPath.Count} length", true);
            //foundPath.ForEach(tile => SerialConsole.Program.Log($"::{tile[0]},{tile[1]};", false));

            ClearBit(BitLocation.mapSearched);
            ClearBit(BitLocation.inDriveWay);

            if (foundPath.Count == 0)
            {
                SerialConsole.Program.Log("!!!!!!!!!Could not find path!!!!!!!!!", true);
                Thread.Sleep(200);
            }

            //Make absolutely sure that we do not modify the returned list
            List<byte[]> scrap = foundPath;
            savedPath = new List<byte[]>();
            foundPath = new List<byte[]>();
            return scrap;
        }

        void FindFrom(byte _onX, byte _onZ)
        {
            //SerialConsole.Program.Log($":::: {_onX},{_onZ} ::::", false);
            WriteBit(_onX, _onZ, BitLocation.inDriveWay, true);
            if (Math.Abs(_onZ - toPosZ) >= Math.Abs(_onX - toPosX)) //All these if-statements are very ugly, but I do not have a better solution right now
            {
                if (_onZ <= toPosZ)
                {
                    if (!ReadBit(_onX, _onZ, BitLocation.backWall)) FindTile(_onX, (byte)(_onZ + 1));
                    returningFromGoal = false;
                }
                else
                {
                    if (!ReadBit(_onX, _onZ, BitLocation.frontWall)) FindTile(_onX, (byte)(_onZ - 1));
                    returningFromGoal = false;
                }

                if (_onX <= toPosX)
                {
                    if (!ReadBit(_onX, _onZ, BitLocation.rightWall)) FindTile((byte)(_onX + 1), _onZ);
                    returningFromGoal = false;
                }
                else
                {
                    if (!ReadBit(_onX, _onZ, BitLocation.leftWall)) FindTile((byte)(_onX - 1), _onZ);
                    returningFromGoal = false;
                }

                if (_onZ > toPosZ)
                {
                    if (!ReadBit(_onX, _onZ, BitLocation.backWall)) FindTile(_onX, (byte)(_onZ + 1));
                    returningFromGoal = false;
                }
                else
                {
                    if (!ReadBit(_onX, _onZ, BitLocation.frontWall)) FindTile(_onX, (byte)(_onZ - 1));
                    returningFromGoal = false;
                }

                if (_onX > toPosX)
                {
                    if (!ReadBit(_onX, _onZ, BitLocation.rightWall)) FindTile((byte)(_onX + 1), _onZ);
                    returningFromGoal = false;
                }
                else
                {
                    if (!ReadBit(_onX, _onZ, BitLocation.leftWall)) FindTile((byte)(_onX - 1), _onZ);
                    returningFromGoal = false;
                }
            }
            else
            {
                if (_onX <= toPosX)
                {
                    if (!ReadBit(_onX, _onZ, BitLocation.rightWall)) FindTile((byte)(_onX + 1), _onZ);
                    returningFromGoal = false;
                }
                else
                {
                    if (!ReadBit(_onX, _onZ, BitLocation.leftWall)) FindTile((byte)(_onX - 1), _onZ);
                    returningFromGoal = false;
                }

                if (_onZ <= toPosZ)
                {
                    if (!ReadBit(_onX, _onZ, BitLocation.backWall)) FindTile(_onX, (byte)(_onZ + 1));
                    returningFromGoal = false;
                }
                else
                {
                    if (!ReadBit(_onX, _onZ, BitLocation.frontWall)) FindTile(_onX, (byte)(_onZ - 1));
                    returningFromGoal = false;
                }

                if (_onX > toPosX)
                {
                    if (!ReadBit(_onX, _onZ, BitLocation.rightWall)) FindTile((byte)(_onX + 1), _onZ);
                    returningFromGoal = false;
                }
                else
                {
                    if (!ReadBit(_onX, _onZ, BitLocation.leftWall)) FindTile((byte)(_onX - 1), _onZ);
                    returningFromGoal = false;
                }

                if (_onZ > toPosZ)
                {
                    if (!ReadBit(_onX, _onZ, BitLocation.backWall)) FindTile(_onX, (byte)(_onZ + 1));
                    returningFromGoal = false;
                }
                else
                {
                    if (!ReadBit(_onX, _onZ, BitLocation.frontWall)) FindTile(_onX, (byte)(_onZ - 1));
                    returningFromGoal = false;
                }
            }
        }

        void SearchCell(byte _onX, byte _onZ)
        {
            //SerialConsole.Program.Log($":::: {_onX},{_onZ} ::::", false);
            if (_onX == toPosX && _onZ == toPosZ)
            {
                //SerialConsole.Program.Log("Found " + foundPath.Count + " long", true);

                if (foundPath.Count <= savedPath.Count || savedPath.Count == 0) //If we found a better path, save it
                {
                    savedPath = new List<byte[]>(foundPath);
                    if (savedPath.Count == Math.Abs(fromPosX - toPosX) + Math.Abs(fromPosZ - toPosZ)) //If this is the shortest possible path (manhattan distance), we are done
                    {
                        foundWay = true;
                    }
                }
                skipNext = true; //We have found optimal way from last tile, so we don't need to explore it more
                returningFromGoal = true; //We are returning to start again
                return;
            }

            bool _returning = returningFromGoal;
            returningFromGoal = false;

            WriteBit(_onX, _onZ, BitLocation.inDriveWay, true);
            //Better way when these are equal?
            if (Math.Abs(_onZ - toPosZ) >= Math.Abs(_onX - toPosX)) //All these if-statements are very ugly, but I do not have a better solution right now
            {
                if (_onZ <= toPosZ)
                {
                    if (!ReadBit(_onX, _onZ, BitLocation.backWall)) FindTile(_onX, (byte)(_onZ + 1));
                }
                else
                {
                    if (!ReadBit(_onX, _onZ, BitLocation.frontWall)) FindTile(_onX, (byte)(_onZ - 1));
                }

                if (_onX <= toPosX)
                {
                    if (!ReadBit(_onX, _onZ, BitLocation.rightWall)) FindTile((byte)(_onX + 1), _onZ);
                }
                else
                {
                    if (!ReadBit(_onX, _onZ, BitLocation.leftWall)) FindTile((byte)(_onX - 1), _onZ);
                }

                if (_onZ > toPosZ)
                {
                    if (!ReadBit(_onX, _onZ, BitLocation.backWall)) FindTile(_onX, (byte)(_onZ + 1));
                }
                else
                {
                    if (!ReadBit(_onX, _onZ, BitLocation.frontWall)) FindTile(_onX, (byte)(_onZ - 1));
                }

                if (_onX > toPosX)
                {
                    if (!ReadBit(_onX, _onZ, BitLocation.rightWall)) FindTile((byte)(_onX + 1), _onZ);
                }
                else
                {
                    if (!ReadBit(_onX, _onZ, BitLocation.leftWall)) FindTile((byte)(_onX - 1), _onZ);
                }
            }
            else
            {
                if (_onX <= toPosX)
                {
                    if (!ReadBit(_onX, _onZ, BitLocation.rightWall)) FindTile((byte)(_onX + 1), _onZ);
                }
                else
                {
                    if (!ReadBit(_onX, _onZ, BitLocation.leftWall)) FindTile((byte)(_onX - 1), _onZ);
                }

                if (_onZ <= toPosZ)
                {
                    if (!ReadBit(_onX, _onZ, BitLocation.backWall)) FindTile(_onX, (byte)(_onZ + 1));
                }
                else
                {
                    if (!ReadBit(_onX, _onZ, BitLocation.frontWall)) FindTile(_onX, (byte)(_onZ - 1));
                }

                if (_onX > toPosX)
                {
                    if (!ReadBit(_onX, _onZ, BitLocation.rightWall)) FindTile((byte)(_onX + 1), _onZ);
                }
                else
                {
                    if (!ReadBit(_onX, _onZ, BitLocation.leftWall)) FindTile((byte)(_onX - 1), _onZ);
                }

                if (_onZ > toPosZ)
                {
                    if (!ReadBit(_onX, _onZ, BitLocation.backWall)) FindTile(_onX, (byte)(_onZ + 1));
                }
                else
                {
                    if (!ReadBit(_onX, _onZ, BitLocation.frontWall)) FindTile(_onX, (byte)(_onZ - 1));
                }
            }

            WriteBit(_onX, _onZ, BitLocation.inDriveWay, false);
            returningFromGoal = _returning || returningFromGoal;
            if (!returningFromGoal)
            {
                WriteBit(_onX, _onZ, BitLocation.mapSearched, true);
            }

            skipNext = false;
        }

        void FindTile(byte _onX, byte _onZ)
        {
            if (skipNext || foundWay || (foundPath.Count > savedPath.Count && savedPath.Count != 0))
                return;

            try
            {
                if ((ReadBit(_onX, _onZ, BitLocation.explored) && !ReadBit(_onX, _onZ, BitLocation.mapSearched) &&
                    !ReadBit(_onX, _onZ, BitLocation.inDriveWay) && !ReadBit(_onX, _onZ, BitLocation.blackTile) &&
                    !ReadBit(_onX, _onZ, BitLocation.ramp)) || (_onX == toPosX && _onZ == toPosZ))
                {
                    foundPath.Add(new byte[] { _onX, _onZ });
                    SearchCell(_onX, _onZ);
                    if (!foundWay)
                        foundPath.RemoveAt(foundPath.Count - 1);
                }
            }
            catch (Exception e)
            {
                SerialConsole.Program.LogException(e);
                for (int i = 0; i < 3; i++) SerialConsole.Program.Log("!!!!!!FindTile -- index out of bounds(?)!!!!!!", true);
            }
        }
#endregion

    }
}
