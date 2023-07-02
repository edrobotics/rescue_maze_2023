// |||| NAVIGATION - Code for using the map class and map data, and localization stuff ||||
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Mapping;

namespace SerialConsole
{
    internal partial class Program
    {
        #region Variables and objects
        //******** Mapping & Checkpoints ********

        /// <summary>
        /// 0 = XCoord, 1 = ZCoord, 2 = Map, 3 = direction (on first)
        /// </summary>
        static readonly List<byte[]> sinceCheckpoint = new();
        static readonly List<Map> maps = new();
        //static readonly List<int[]> mapInfo = new(); //For storing info of where the ramp is
        static int currentMap = 0;
        static int currentHeight = 0;
        static int currentArea = 0;
        static int rampCount = 0;
        static int savedRampCount = 0;

        static bool reset = false;
        static int savedMapCount = 1;

        #endregion variables and objects

        #region Map/Localization updates
        // ********************************** Map And Localization Updates ********************************** 

        static void UpdateDirection(int _leftTurns)
        {
            if (reset) return;
            Log($"change dir from {direction}", false);
            direction += _leftTurns;

            while (direction > 3)
            {
                direction -= 4;
            }
            while (direction < 0)
            {
                direction += 4;
            }
            Log($"new dir {direction}", true);
        }

        static void KitDirectionUpdate(char _direction)
        {
            if (reset) return;
            if (_direction == 'r')
            {
                UpdateDirection(1);
            }
            else if (_direction == 'l')
            {
                UpdateDirection(-1);
            }
            else
            {
                Log($"Dir {_direction} is fake (UpdateDirection)", true);
            }
        }

        static int CharToDirection(char _kitDirection)
        {
            switch (_kitDirection)
            {
                case 'l': //The kit is on the left
                    return FixDirection(direction + 1);
                case 'r': //The kit is on the right
                    return FixDirection(direction - 1);
                default:
                    Log(_kitDirection + ": error with kit direction", true);
                    dropKits = false;
                    return 0;
            }
        }

        static bool CheckKitSide(char _side)
        {
            Log("Victim here was " + (ReadHere((BitLocation)CharToDirection(_side)) ? "real" : "fake"), true);
            return ReadHere((BitLocation)CharToDirection(_side));
        }

        static bool CheckKitSides(char _side, int _dir1, int _dir2)
        {
            if (!dropKits) return false;
            return _side switch
            {
                'l' => ReadHere((BitLocation)FixDirection(_dir1 + 1)) && ReadHere((BitLocation)FixDirection(_dir2 + 1)),
                'r' => ReadHere((BitLocation)FixDirection(_dir1 - 1)) && ReadHere((BitLocation)FixDirection(_dir2 - 1)),
                _ => false,
            };
        }

        /// <summary>
        /// Subtracs or adds 4 until the int is 0<= int <= 3, to make sure that for example adding one to the direction 3 (right) will give the direction 0 (front).
        /// Can also be used for map bits since the same system is used; direction = wall in front.
        /// </summary>
        /// <returns>An int thats 0,1,2 or 3</returns>
        static int FixDirection(int _dir)
        {
            while (_dir > 3)
            {
                _dir -= 4;
            }
            while (_dir < 0)
            {
                _dir += 4;
            }
            return _dir;
        }

        static int TileToDirection(byte[] _cell)
        {
            return TileToDirection(_cell, new byte[] { (byte)posX, (byte)posZ });
        }

        static int TileToDirection(byte[] _toCell, byte[] _fromCell)
        {
            if (_toCell[0] == _fromCell[0] && _toCell[1] == _fromCell[1] - 1)
            {
                return 0;
            }
            if (_toCell[0] == _fromCell[0] - 1 && _toCell[1] == _fromCell[1])
            {
                return 1;
            }
            if (_toCell[0] == _fromCell[0] && _toCell[1] == _fromCell[1] + 1)
            {
                return 2;
            }
            if (_toCell[0] == _fromCell[0] + 1 && _toCell[1] == _fromCell[1])
            {
                return 3;
            }

            Log("********* ACHTUNG ACHTUNG, DAS IST NICHT GUT ************", true);
            Log($" -- Between {_fromCell[0]},{_fromCell[1]} and {_toCell[0]},{_toCell[1]}", true);
            errors++;
            Delay(10);
            if (_toCell[0] == _fromCell[0] && _toCell[1] == _fromCell[1])
            {
                return direction;
            }
            errors+=2;

            if (driveWay.Count > 0)
            {
                Log("Finding new path", true);
                FindPathHere(driveWay.Last()[0], driveWay.Last()[1]);
                return TileToDirection(driveWay[0], _fromCell);
            }

            Log("DriveWay is finished, but still error, strange", true);
            return direction;
        }

        static byte[] DirToTile(int _globalDir, byte _x, byte _z)
        {
            return FixDirection(_globalDir) switch
            {
                0 => new byte[] { _x, (byte)(_z - 1) },
                1 => new byte[] { (byte)(_x - 1), _z },
                2 => new byte[] { _x, (byte)(_z + 1) },
                3 => new byte[] { (byte)(_x + 1), _z },
                _ => throw new Exception("Not possible but ok")
            };
        }

        static bool ShouldGoTo(int _posX, int _posZ)
        {
            return !maps[currentMap].ReadBit(_posX, _posZ, BitLocation.explored) && !maps[currentMap].ReadBit(_posX, _posZ, BitLocation.blackTile);
        }

        static void UpdateLocation() //ONLY USE AFTER YOU KNOW THERE IS A NEW CELL
        {
            if (reset) return;

            if (locationUpdated)
            {
                return;
            }
            locationUpdated = true;

            UpdateDirection(0);
            switch (direction)
            {
                case 0:
                    posZ--;
                    if (posZ < lowestZ)
                        lowestZ = posZ;
                    break;
                case 1:
                    posX--;
                    if (posX < lowestX)
                        lowestX = posX;
                    break;
                case 2:
                    posZ++;
                    if (posZ > highestZ)
                        highestZ = posZ;
                    break;
                case 3:
                    posX++;
                    if (posX > highestX)
                        highestX = posX;
                    break;
                default:
                    throw new Exception("HOW IS THIS EVEN POSSIBLE, SOMEHING VERY WRONG WITH UPDATE LOCATION");
            }

            if (posX >= maps[currentMap].Length)
            {
                Log("!!!Error PosX hi, expanding", true);
                errors+=4;
                maps[currentMap] += 2;
            }
            if (posX < 0)
            {
                posX = 0;
                for (int i = 0; i < 5; i++) Log("!!!Error posX lo", true);
                errors+=7;
            }
            if (posZ >= maps[currentMap].Length)
            {
                Log("!!!Error PosZ hi, expanding", true);
                errors += 4;
                maps[currentMap] += 2;
            }
            if (posZ < 0)
            {
                posZ = 0;
                for (int i = 0; i < 5; i++) Log("!!!Error posZ lo", true);
                errors+=7;
            }
            AddTile();

            if (posX == maps[currentMap].StartPosX && posZ == maps[currentMap].StartPosZ && currentMap == 0 && timer.ElapsedMilliseconds > (MINUTES * 60 - 30) * 1000)
            {
                Delay(30_000);
            }
        }

        static void UpdateMap()//Behind - Always false when driving normally
        {
            SensorCheck();
            if (reset) return;

            bool wallNZ;
            bool wallNX;
            bool wallPZ;
            bool wallPX;

            if (direction == 0)
            {
                wallNZ = frontPresent; //front is front
                wallNX = leftPresent;
                wallPZ = false;
                wallPX = rightPresent;
            }
            else if (direction == 1)
            {
                wallNZ = rightPresent; //right is front
                wallNX = frontPresent;
                wallPZ = leftPresent;
                wallPX = false;
            }
            else if (direction == 2)
            {
                wallNZ = false; //back is front
                wallNX = rightPresent;
                wallPZ = frontPresent;
                wallPX = leftPresent;
            }
            else if (direction == 3)
            {
                wallNZ = leftPresent; //left is front
                wallNX = false;
                wallPZ = rightPresent;
                wallPX = frontPresent;
            }
            else
            {
                Log("ERROR", true);
                throw new Exception("THIS SHOULD NOT BE POSSIBLE, direction out of bounds");
            }

            //Double check
            if (ReadHere(BitLocation.explored))
            {
                if (!(wallNZ == ReadHere(BitLocation.frontWall) &&
                    wallNX == ReadHere(BitLocation.leftWall) &&
                    wallPZ == ReadHere(BitLocation.backWall) &&
                    wallPX == ReadHere(BitLocation.rightWall)))
                {
                    for (int i = 0; i < 5; i++) Log("!!! Error in mapping, probably !!!", true);
                    errors += 4;
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

            Log("-----" + posX + " , " + posZ + "-----" + " : ", true);

            string _log = "";
            for (int i = 15; i >= 0; i--)
            {
                _log += ReadHere((BitLocation)i) ? "1" : "0";
            }
            Log(_log, false);
        }

        static void UpdateMapFull(bool _turnBack)
        {
            if (reset) return;
            Log("Updating full map", true);
            UpdateMap();
            Turn('l');
            SensorCheck();
            if (reset) return;
            WriteHere((BitLocation)FixDirection(direction + 1), leftPresent); //wall locally behind set
            if (_turnBack)
                Turn('r');
        }

        #endregion

        #region General data storage and checking
        // ********************************** Checkpoints ********************************** 

        /// <summary>
        /// Resets mapping and localization to that of the last checkpoint, after a LoP
        /// </summary>
        /// <exception cref="Exception">- When reset fails</exception>
        static void Reset()
        {
            if (reset) return;

            errors = 2;
            Log("Resetting", true);
            try
            {
                driveWay.Clear();

                goingBack = false;
                rampCount = savedRampCount;
                mapWayBack = new List<List<byte[]>>(saveWayBack);

                while (savedMapCount < maps.Count)
                {
                    maps.RemoveAt(maps.Count - 1);
                    Log("!!!! DELETING MAP BC RESET !!!!", true);
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

                    Log($"reset pos: {posX},{posZ}, dir: {direction}, map: {currentMap}", true);
                    for (int i = sinceCheckpoint.Count-1; i >= 0; i--)
                    {
                        byte[] _coords = sinceCheckpoint[i];
                        Log($"::removing maps[{_coords[2]}] - {_coords[0]},{_coords[1]}", false);
                        if (_coords[2] < maps.Count)
                            maps[_coords[2]].ClearTile(_coords[0], _coords[1], BitLocation.victim);
                        sinceCheckpoint.RemoveAt(i);
                    }
                }
            }
            catch (Exception e)
            {
                LogException(e);
                for (int i = 0; i < 3; i++) Log($"!!Reset error, very bad:!!", true);
                posX = 25;
                posZ = 25;
                currentMap = 0;
                currentArea = 0;
                direction = 0;
                errors = 6;
                AddTile();
                UpdateMapFull(true);
                throw new Exception("Reset error", e);
            }
            dropKits = false;

            Delay(10);
            
            if (serialPort1.BytesToRead != 0)
            {
                serialPort1.ReadExisting();
            }

            Delay(300, true);

            AddTile();
            UpdateMapFull(true);
            currentArea = maps[currentMap].GetArea(new byte[] {(byte)posX, (byte)posZ});
            if (currentArea == -1)
                currentArea = 0;
            Delay(10);
            reset = true;
        }

        /// <summary>Make sure we always add to sincecheckpoint the same way</summary>
        static void AddSinceCheckpoint()
        {
            AddSinceCheckpoint((byte)posX, (byte)posZ, (byte)currentMap);
        }

        static void AddSinceCheckpoint(byte _x, byte _z, byte _map)
        {
            if (sinceCheckpoint.Count == 0)
            {
                sinceCheckpoint.Add(new byte[] { _x, _z, _map, (byte)direction });
            }
            else if (!ReadHere(BitLocation.explored))
            {
                sinceCheckpoint.Add(new byte[] { _x, _z, _map });
            }
        }

        /// <summary>Add this tile to sincecheckpoint, mapwayback and areas</summary>
        static void AddTile()
        {
            AddSinceCheckpoint();

            if (!ReadHere(BitLocation.tileAdded))
            {
                Log($"Adding {posX},{posZ}", false);
                maps[currentMap].Areas[currentArea].Add(new byte[] { (byte)posX, (byte)posZ });
                WriteHere(BitLocation.tileAdded, true);
            }
        }

        /// <summary>Add this tile to sincecheckpoint, mapwayback and areas</summary>
        static void AddTile(byte _x, byte _z, int _map, int _area)
        {
            AddSinceCheckpoint(_x, _z, (byte)_map);

            if (!maps[_map].ReadBit(_x, _z, BitLocation.tileAdded))
            {
                Log($"Adding {_x},{_z}", false);
                maps[_map].Areas[_area].Add(new byte[] { _x, _z });
                maps[_map].WriteBit(_x, _z, BitLocation.tileAdded, true);
            }
        }

        static void AddArea(int _firstX, int _firstZ)
        {
            maps[currentMap].AddArea();
            currentArea = maps[currentMap].Areas.Count - 1;
            maps[currentMap].Areas[currentArea].Add(new byte[] { (byte)_firstX, (byte)_firstZ });

            mapWayBack.Add(new List<byte[]>() { new byte[] { (byte)currentMap, (byte)currentArea }, 
                                                new byte[] { (byte)_firstX, (byte)_firstZ } });
        }

        /// <summary>
        /// Search to the ramp, the tile we searched from last time
        /// </summary>
        static void UpdateWayBack()
        {
            UpdateWayBack(posX, posZ, mapWayBack[^1][^1][0], mapWayBack[^1][^1][1], currentMap, currentArea);
        }

        static void UpdateWayBack(int _fromX, int _fromZ, int _toX, int _toZ, int _map, int _area)
        {
            mapWayBack[^1] = maps[_map].PathTo(_toX, _toZ, _fromX, _fromZ);
            mapWayBack[^1].Insert(0, new byte[] { (byte)_map, (byte)_area });
            mapWayBack[^1].Insert(1, new byte[] { (byte)_fromX, (byte)_fromZ });
        }

        static bool AreaInWayBack(int _area)
        {
            for (int i = mapWayBack.Count - 1; i >= 0; i--)
            {
                if (mapWayBack[i][0][1] == _area)
                {
                    return true;
                }
            }
            return false;
        }

        static void RemoveWayBack(int _untilArea)
        {
            for (int i = mapWayBack.Count - 1; i >= 0; i--)
            {
                if (mapWayBack[i][0][1] == _untilArea)
                {
                    return;
                }
                mapWayBack.RemoveAt(i);
            }
        }

        static void RemoveWayBack(int _fromArea, int _toArea, int _map)
        {
            bool _a1 = false, _a2 = false;
            for (int i = mapWayBack.Count - 1; i >= 0; i--)
            {
                if (mapWayBack[i][0][1] == _fromArea && mapWayBack[i][0][0] == _map)
                    _a1 = true;
                if (mapWayBack[i][0][1] == _toArea && mapWayBack[i][0][0] == _map)
                    _a2 = true;
                if (_a1 && _a2)
                    break;
                mapWayBack.RemoveAt(i);
            }
            mapWayBack[^1][0][1] = (byte)_toArea;
            UpdateWayBack();
        }

        static int AreaCheck(byte _x, byte _z, int _area, int _map)
        {
            if (!maps[_map].ReadBit(_x, _z, BitLocation.frontWall))
                _area = TileAreaCheck(_x, (byte)(_z - 1), _area, _map);
            if (!maps[_map].ReadBit(_x, _z, BitLocation.leftWall))
                _area = TileAreaCheck((byte)(_x - 1), _z, _area, _map);
            if (!maps[_map].ReadBit(_x, _z, BitLocation.backWall))
                _area = TileAreaCheck(_x, (byte)(_z + 1), _area, _map);
            if (!maps[_map].ReadBit(_x, _z, BitLocation.rightWall))
                _area = TileAreaCheck((byte)(_x + 1), _z, _area, _map);
            return _area;
        }

        static int TileAreaCheck(byte _x, byte _z, int _area, int _map)
        {
            if (maps[_map].ReadBit(_x, _z, BitLocation.explored) && !maps[_map].ReadBit(_x, _z, BitLocation.blackTile) && !maps[_map].ReadBit(_x, _z, BitLocation.ramp))
            {
                int _tileArea = maps[_map].GetArea(new byte[] { _x, _z });
                if (_tileArea == -1)
                {
                    Log($"{_x},{_z} is not added, why???", false);
                    return _area;
                }

                if (_tileArea != _area)
                {
#warning Will area index change for other saved stuff?
                    //To make sure that index does not change, we need to 
                    if (_tileArea < _area)
                    {
                        (_tileArea, _area) = (_area, _tileArea);
                    }
                    //Merge areas
                    Log($"MERGING AREA {_tileArea} to {_area}", true);
                    maps[_map].MergeAreas(_area, _tileArea);
                    RemoveWayBack(_tileArea, _area, _map);
                    return _tileArea;
                }
            }
            return _area;
        }

        static void VisitedCheckpoint()
        {
            saveWayBack = new List<List<byte[]>>(mapWayBack);
            sinceCheckpoint.Clear();
            savedMapCount = maps.Count;
            savedRampCount = rampCount;
            foreach (Map map in maps)
            {
                map.SaveInfo();
            }
            AddSinceCheckpoint();
        }

        static bool MarkMapAsVisited(int _checkMap, int _currMap, int _currArea)
        {
            if (maps[_checkMap].CrossTiles.Count == 0)
            {
                return true;
            }

            foreach (byte[] _crossTile in maps[_checkMap].CrossTiles)
            {
                if (!maps[_checkMap].ReadBit(_crossTile[0], _crossTile[1], BitLocation.ramp))
                {
                    return false;
                }
            }

            foreach (byte[] _crossTile in maps[_checkMap].CrossTiles)
            {
                try
                {
                    byte[] _ramp = maps[_currMap].GetRampAt(RampByRamptile(_crossTile[0], _crossTile[1], _checkMap)[(int)RampStorage.RampIndex]);
                    if (maps[_currMap].GetArea(new byte[] { _ramp[(int)RampStorage.XCoord], _ramp[(int)RampStorage.ZCoord] }) != _currArea)
                    {
                        return false;
                    }
                }
                catch (NonexistantRampException)
                {
                    return false;
                }
            }

            return true;
        }

        static byte[] RampByRamptile(int _x, int _z, int _map)
        {
            for (int i = 0; i < maps[_map].Ramps.Count; i++)
            {
                byte[] _ramp = maps[_map].Ramps[i];

                byte[] _rampTile = DirToTile(_ramp[(int)RampStorage.RampDirection], _ramp[(int)RampStorage.XCoord], _ramp[(int)RampStorage.ZCoord]);
                if (_rampTile[0] == _x && _rampTile[1] == _z)
                {
                    return _ramp;
                }
            }

            throw new NonexistantRampException();
        }

        #endregion general data

        #region Map data

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

        #endregion map

        #region Pathfinding

        /// <summary>Searches from this tile to another tile</summary><returns>The path to the tile</returns>
        static void FindPathHere(int _toX, int _toZ)
        {
            driveWay = maps[currentMap].PathTo(_toX, _toZ, posX, posZ);
        }

        /// <summary>
        /// Searches for a path to start
        /// </summary>
        /// <returns>The path to the tile, or nothing </returns>
        static List<byte[]> PathToStart()
        {
            List<byte[]> path = new();

            for (int i = mapWayBack.Count - 1; i >= 0; i--)
            {
                for (int j = 2; j < mapWayBack[i].Count; j++) //Do not add first as it is info, or second as it is 'this' tile
                {
                    path.Add(mapWayBack[i][j]);
                }
            }
            Log("[Begin to start]", false);
            foreach (byte[] _tile in path)
            {
                Log($"## {_tile[0]},{_tile[1]} ##", false);
            }
            Log("[End to start]", false);
            return path;

//#warning update
//            byte _toX = (byte)maps[0].StartPosX,
//                 _toZ = (byte)maps[0].StartPosZ;
//            if (!maps[0].ReadBit(_toX, _toZ, BitLocation.explored)) _ = new List<byte[]>();
//            List<byte[]> fullPath = new();
//            //List<byte[]> path = new();
//            byte _simMap = (byte)currentMap;
//            byte _simX = (byte)posX;
//            byte _simZ = (byte)posZ;

//            byte[] _ramp;
//            byte[] _tile2;
//            byte[] _newInfo;

//            while (!(_simMap == 0 && _simX == _toX && _simZ == _toZ))
//            {
//                Log("(Re?)started path finding to start", true);
//                while (_simMap != 0)
//                {
//                    Log($"On map {_simMap}", true);
                    
//                    //If we cannot find a path to the ramp, try other maps
//                    for (int i = 0; i < maps[_simMap].Ramps.Count; i++)
//                    {
//                        //New ramp in acsending order, we want the first possible
//                        _ramp = maps[_simMap].Ramps[i];

//                        //The tile we are searching to, to be able to go down the ramp
//                        _tile2 = DirToTile(_ramp[(int)RampStorage.RampDirection], _ramp[(int)RampStorage.XCoord], _ramp[(int)RampStorage.ZCoord]);

//                        path = new List<byte[]>(maps[_simMap].PathTo(_tile2[0], _tile2[1], _simX, _simZ));

//                        if (path.Count != 0)
//                        {
//                            fullPath.AddRange(path);
//                            _newInfo = maps[_ramp[(int)RampStorage.ConnectedMap]].GetRampAt(_ramp[(int)RampStorage.RampIndex]);
//                            _simMap = _ramp[(int)RampStorage.ConnectedMap];
//                            _simX = _newInfo[(int)RampStorage.XCoord];
//                            _simZ = _newInfo[(int)RampStorage.ZCoord];
//                            break;
//                        }
//                    }

//                    if (path.Count == 0)
//                    {
//                        Log("CANNOT FIND WAY DOWN", true);
//                        errors += 4;
//                        return new List<byte[]>();
//                    }
//                }

//                //When we are on the first map, try to find our way to the start
//                path = new List<byte[]>(maps[_simMap].PathTo(_toX, _toZ, _simX, _simZ));
//                if (path.Count != 0 || (_simX == _toX && _simZ == _toZ))
//                {
//                    fullPath.AddRange(path);
//                    Log("Found path to start", true);
//                    return fullPath; 
//                }

//                //If we did not find a path to goal, this area might be unconnected from the goal area
//                for (int i = 0; i < maps[_simMap].Ramps.Count; i++)
//                {
//                    //Path down ramp will be path to ramp 1 + going the ramp
//                    _ramp = maps[_simMap].Ramps[i];

//                    //The tile we are searching to, to be able to go down the ramp
//                    _tile2 = DirToTile(_ramp[(int)RampStorage.RampDirection], _ramp[(int)RampStorage.XCoord], _ramp[(int)RampStorage.ZCoord]);

//                    path = new List<byte[]>(maps[_simMap].PathTo(_tile2[0], _tile2[1], _simX, _simZ));

//                    if (path.Count != 0)
//                    {
//                        fullPath.AddRange(path);
//                        _newInfo = maps[_ramp[(int)RampStorage.ConnectedMap]].GetRampAt(_ramp[(int)RampStorage.RampIndex]);
//                        _simMap = _ramp[(int)RampStorage.ConnectedMap];
//                        _simX = _newInfo[(int)RampStorage.XCoord];
//                        _simZ = _newInfo[(int)RampStorage.ZCoord];
//                        break;
//                    }
//                }
//            }

//            return fullPath;
        }

        //static void GoToRamp(byte[] _ramp)
        //{
        //    Log($"Going to ramp at {_ramp[(int)RampStorage.XCoord]},{_ramp[(int)RampStorage.ZCoord]}", false);
        //    driveWay = maps[currentMap].PathTo(_ramp[(int)RampStorage.XCoord], _ramp[(int)RampStorage.ZCoord], posX, posZ); //Way to tile next to ramp
        //    driveWay.Add(DirToTile(_ramp[(int)RampStorage.RampDirection], _ramp[(int)RampStorage.XCoord], _ramp[(int)RampStorage.ZCoord])); //Driving through the real ramp

        //    if (driveWay.Count <= 1 && _ramp[(int)RampStorage.XCoord] != posX && _ramp[(int)RampStorage.ZCoord] != posZ)
        //    {
        //        for (int i = 0; i < 5; i++) Log("!!Something is wrong in GoToRamp, possible ramp storage or mapping error!!", true);
        //        throw new Exception("Going To Ramp failed");
        //    }
        //}
        #endregion path
    }
}
