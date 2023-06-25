// |||| NAVIGATION - Code for using the map class and map data, and localization stuff ||||
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SerialConsole
{
    internal partial class Program
    {
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
                    Log(_kitDirection + ": error with kit direction");
                    dropKits = false;
                    return 0;
            }
        }

        static bool CheckKitSide(char _side)
        {
            Log("Victim here was " + (ReadHere((BitLocation)CharToDirection(_side)) ? "real" : "fake"));
            return ReadHere((BitLocation)CharToDirection(_side));
        }

        /// <summary>
        /// Subtracs or adds 4 until the int is 0<= int <= 3, to make sure that for example adding one to the direction 3 (right) will give the direction 0 (front).
        /// Can also be used for map bits since I use the same system.
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
            errors++;
            Thread.Sleep(10);
            if (_toCell[0] == _fromCell[0] && _toCell[1] == _fromCell[1])
            {
                return direction;
            }
            errors+=2;

            if (driveWay.Count > 0)
            {
                Log("Finding new path");
                FindPathHere(driveWay.Last()[0], driveWay.Last()[1]);
                return TileToDirection(driveWay[0], _fromCell);
            }

            Log("DriveWay is finished, but still error, strange", true);
            return direction;
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
            if (reset) return;

            if (locationUpdated)
            {
                return;
            }
            locationUpdated = true;

            UpdateDirection(0);
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
                errors++;
                for (int i = 0; i < 5; i++) Log("Something wrong with direction in UpdateLocation!!!", true);
                throw new Exception("HOW IS THIS EVEN POSSIBLE, SOMEHING VERY WRONG WITH UPDATE LOCATION");
            }

            if (posX > maps[currentMap].Length)
            {
                posX = maps[currentMap].Length;
                for (int i = 0; i < 5; i++) Log("!!!Error PosX hi", true);
                errors+=2;
            }
            if (posX < 0)
            {
                posX = 0;
                for (int i = 0; i < 5; i++) Log("!!!Error posX lo", true);
                errors+=2;
            }
            if (posZ > maps[currentMap].Length)
            {
                posZ = maps[currentMap].Length;
                for (int i = 0; i < 5; i++) Log("!!!Error PosZ hi", true);
                errors+=2;
            }
            if (posZ < 0)
            {
                posZ = 0;
                for (int i = 0; i < 5; i++) Log("!!!Error posZ lo", true);
                errors+=2;
            }
            AddSinceCheckPoint();

            if (posX == maps[currentMap].StartPosX && posZ == maps[currentMap].StartPosZ && currentMap == 0 && timer.ElapsedMilliseconds > (7 * 60 + 30) * 1000)
            {
                Thread.Sleep(30_000);
            }
        }

        static void UpdateMap()//Behind - Always false when driving normally
        {
            if (reset) return;
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
            Log("Updating full map", true);
            UpdateMap();
            Turn('l');
            SensorCheck();
            WriteHere((BitLocation)FixDirection(direction + 1), leftPresent); //wall locally behind set'
            if (_turnBack)
                Turn('r');
        }


        // ********************************** Checkpoints ********************************** 

        static void Reset()
        {
            if (reset) return;

            errors = 2;
            Log("Resetting", true);
            try
            {
                driveWay.Clear();

                rampCount = savedRampCount;
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
                direction = 0;
                errors = 6;
                UpdateMapFull(true);
                AddSinceCheckPoint();
                throw new Exception("Reset error", e);
            }
            dropKits = false;

            Thread.Sleep(10);
            
            if (serialPort1.BytesToRead != 0)
            {
                serialPort1.ReadExisting();
            }

            Thread.Sleep(300);

            UpdateMapFull(true);
            AddSinceCheckPoint();
            currentArea = maps[currentMap].GetArea(new byte[] {(byte)posX, (byte)posZ});
            Thread.Sleep(10);
            reset = true;
        }

        /// <summary>To make sure that we always add since checkpoint the same way</summary>
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

            maps[currentMap].Areas[currentArea].Add(new byte[] { (byte)posX, (byte)posZ });
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
            if (maps[_map].ReadBit(_x, _z, BitLocation.explored) && !maps[_map].ReadBit(_x, _z, BitLocation.blackTile))
            {
                int _tileArea = maps[_map].GetArea(new byte[] { _x, _z });
                if (_tileArea != _area)
                {
                    Log($"MERGING AREA {_area} to {_tileArea}", true);
                    maps[_map].MergeAreas(_area, _tileArea);
                    return _tileArea;
                }
            }
            return _area;
        }

        static void VisitedCheckpoint()
        {
            sinceCheckpoint.Clear();
            savedMapCount = maps.Count;
            savedRampCount = rampCount;
            foreach (Map map in maps)
            {
                map.SaveInfo();
            }
            AddSinceCheckPoint();
        }

        //Experimental
//        static List<byte[]> FullPathTo(byte _toX, byte _toZ, byte _toMap, byte _fromX, byte _fromZ, byte _fromMap)
//        {
//#error not done

//            List<byte[]> fullPath = new();
//            List<byte[]> path = new();
//            byte _simMap = _fromMap;
//            byte _simX = _fromX;
//            byte _simZ = _fromZ;

//            byte[] _ramp;
//            byte[] _tile2;
//            byte[] _newInfo;

//            while (!(_simMap == _toMap && _simX == _toX && _simZ == _toZ))
//            {
//                Log("(Re?)started path finding to start", true);
//                while (_simMap != _toMap)
//                {
//                    Log($"On map {_simMap}", true);

//                    //If we cannot find a path to the ramp, try other maps
//                    for (int i = 0; i < maps[_simMap].Ramps.Count; i++)
//                    {
//                        //New ramp in acsending order, we want the first possible
//                        _ramp = maps[_simMap].Ramps[i];
//                        if (_ramp[(int)RampStorage.RampSearched] == 1)
//                        {
//                            continue;
//                        }

//                        //The tile we are searching to, to be able to go down the ramp
//                        _tile2 = DirToTile(_ramp[(int)RampStorage.RampDirection], _ramp[(int)RampStorage.XCoord], _ramp[(int)RampStorage.ZCoord]);

//                        if (maps[_simMap].IsSameArea(_tile2, new byte[] { _simX, _simZ }))
//                        {
//                            path = new List<byte[]>(maps[_simMap].PathTo(_tile2[0], _tile2[1], _simX, _simZ));
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

        //    return fullPath;
        //}

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


        /// <summary>Searches from this tile to another tile</summary><returns>The path to the tile</returns>
        static void FindPathHere(int _toX, int _toZ)
        {
            driveWay = new List<byte[]>(maps[currentMap].PathTo(_toX, _toZ, posX, posZ));
        }

        /// <summary>
        /// Searches for a path to start
        /// </summary>
        /// <returns>The path to the tile, or nothing </returns>
        static List<byte[]> PathToStart()
        {
            byte _toX = (byte)maps[0].StartPosX,
                 _toZ = (byte)maps[0].StartPosZ;
            if (!maps[0].ReadBit(_toX, _toZ, BitLocation.explored)) _ = new List<byte[]>();
            List<byte[]> fullPath = new();
            List<byte[]> path = new();
            byte _simMap = (byte)currentMap;
            byte _simX = (byte)posX;
            byte _simZ = (byte)posZ;

            byte[] _ramp;
            byte[] _tile2;
            byte[] _newInfo;

            while (!(_simMap == 0 && _simX == _toX && _simZ == _toZ))
            {
                Log("(Re?)started path finding to start", true);
                while (_simMap != 0)
                {
                    Log($"On map {_simMap}", true);
                    
                    //If we cannot find a path to the ramp, try other maps
                    for (int i = 0; i < maps[_simMap].Ramps.Count; i++)
                    {
                        //New ramp in acsending order, we want the first possible
                        _ramp = maps[_simMap].Ramps[i];

                        //The tile we are searching to, to be able to go down the ramp
                        _tile2 = DirToTile(_ramp[(int)RampStorage.RampDirection], _ramp[(int)RampStorage.XCoord], _ramp[(int)RampStorage.ZCoord]);

                        path = new List<byte[]>(maps[_simMap].PathTo(_tile2[0], _tile2[1], _simX, _simZ));

                        if (path.Count != 0)
                        {
                            fullPath.AddRange(path);
                            _newInfo = maps[_ramp[(int)RampStorage.ConnectedMap]].GetRampAt(_ramp[(int)RampStorage.RampIndex]);
                            _simMap = _ramp[(int)RampStorage.ConnectedMap];
                            _simX = _newInfo[(int)RampStorage.XCoord];
                            _simZ = _newInfo[(int)RampStorage.ZCoord];
                            break;
                        }
                    }

                    if (path.Count == 0)
                    {
                        Log("CANNOT FIND WAY DOWN", true);
                        errors += 4;
                        return new List<byte[]>();
                    }
                }

                //When we are on the first map, try to find our way to the start
                path = new List<byte[]>(maps[_simMap].PathTo(_toX, _toZ, _simX, _simZ));
                if (path.Count != 0 || (_simX == _toX && _simZ == _toZ))
                {
                    fullPath.AddRange(path);
                    Log("Found path to start", true);
                    return fullPath; 
                }

                //If we did not find a path to goal, this area might be unconnected from the goal area
                for (int i = 0; i < maps[_simMap].Ramps.Count; i++)
                {
                    //Path down ramp will be path to ramp 1 + going the ramp
                    _ramp = maps[_simMap].Ramps[i];

                    //The tile we are searching to, to be able to go down the ramp
                    _tile2 = DirToTile(_ramp[(int)RampStorage.RampDirection], _ramp[(int)RampStorage.XCoord], _ramp[(int)RampStorage.ZCoord]);

                    path = new List<byte[]>(maps[_simMap].PathTo(_tile2[0], _tile2[1], _simX, _simZ));

                    if (path.Count != 0)
                    {
                        fullPath.AddRange(path);
                        _newInfo = maps[_ramp[(int)RampStorage.ConnectedMap]].GetRampAt(_ramp[(int)RampStorage.RampIndex]);
                        _simMap = _ramp[(int)RampStorage.ConnectedMap];
                        _simX = _newInfo[(int)RampStorage.XCoord];
                        _simZ = _newInfo[(int)RampStorage.ZCoord];
                        break;
                    }
                }
            }

            return fullPath;
        }

        static void GoToRamp(byte[] _ramp)
        {
            Log($"Going to ramp at {_ramp[(int)RampStorage.XCoord]},{_ramp[(int)RampStorage.ZCoord]}", false);
            driveWay = new List<byte[]>(maps[currentMap].PathTo(_ramp[(int)RampStorage.XCoord], _ramp[(int)RampStorage.ZCoord], posX, posZ)) //Way to tile next to ramp
            {
                DirToTile(_ramp[(int)RampStorage.RampDirection], _ramp[(int)RampStorage.XCoord], _ramp[(int)RampStorage.ZCoord]) //Driving through the real ramp
            };
            if (driveWay.Count <= 1 && _ramp[(int)RampStorage.XCoord] != posX && _ramp[(int)RampStorage.ZCoord] != posZ)
            {
                for (int i = 0; i < 5; i++) Log("!!Something is wrong in GoToRamp, possible ramp storage or mapping error!!", true);
                throw new Exception("Going To Ramp failed");
            }
        }
    }
}
