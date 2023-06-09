using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SerialConsole
{
    internal partial class Program
    {
        static bool skipNext;
        static bool returningFromGoal;
        static bool foundWay;

        static List<byte[]> foundPath = new();
        static List<byte[]> savedPath = new();

        // ********************************** Map Navigation ********************************** 
        /*
        static void FindPathTo(int _toX, int _toZ, int _map)
        {
            // Test each cell for options
            if (_map != currentMap)
            {
                GoBackLevel(_map);
            }

            toPosX = _toX;
            toPosZ = _toZ;

            foundWay = false;
            FindExploredCells(posX, posZ);
            foundWay = false;
            driveWay.ForEach(way => Console.WriteLine(way + " , "));

            for (int i = 0; i < maps[currentMap].GetLength(0); i++)
            {
                for (int j = 0; j < maps[currentMap].GetLength(1); j++)
                {
                    WriteMapBit(i, j, mapSearched, false);
                }
            }
        }*/

        static void GoToRamp(byte[] _ramp) //FIX when ramps done
        {
            Log($"Going to ramp at {_ramp[(int)RampStorage.XCoord]},{_ramp[(int)RampStorage.ZCoord]}", false);
            driveWay = new List<byte[]>(PathTo(_ramp[(int)RampStorage.XCoord], _ramp[(int)RampStorage.ZCoord])) //Way to tile next to ramp
            {
                DirToTile(_ramp[(int)RampStorage.RampDirection], _ramp[(int)RampStorage.XCoord], _ramp[(int)RampStorage.ZCoord]) //Driving to the real ramp
            };
            if (driveWay.Count <= 1 && _ramp[(int)RampStorage.XCoord] != posX && _ramp[(int)RampStorage.ZCoord] != posZ)
            {
                for(int i = 0; i < 5; i++) Log("!!Something is wrong in SearchToRamp, possible ramp storage or mapping error!!", true);
                throw new Exception("Going To Ramp failed");
            }
        }

        static List<byte[]> PathTo(int _toX, int _toZ)
        {
            foundPath = new List<byte[]>();
            if (_toX == posX && _toZ == posZ)
                return foundPath;

            toPosX = _toX;
            toPosZ = _toZ;

            foundWay = false;
            skipNext = false;
            returningFromGoal = false;
            //shortenAvailable = false;
            FindFrom((byte)posX, (byte)posZ);

            if ((foundPath.Count > savedPath.Count || foundPath.Count == 0) && savedPath.Count > 0)
            {
                foundPath = new List<byte[]>(savedPath);
            }

            //driveWay.ForEach(num => Debug.Log(num.X + " , " + num.Z + " ; "));
            Log($"Found PathTo {_toX},{_toZ} from {posX},{posZ} at {foundPath.Count} length", true);
            foundPath.ForEach(tile => Log($"::{tile[0]},{tile[1]};", false));

            maps[currentMap].ClearBit(BitLocation.mapSearched);
            maps[currentMap].ClearBit(BitLocation.inDriveWay);

            savedPath.Clear();
            if (foundPath.Count == 0 && _toX != posX && _toZ != posZ)
            {
                Log("!!!!!!!!!Could not find path!!!!!!!!!", true);
                Thread.Sleep(250); //Don't do work here
                //return false;
            }

            return foundPath;
        }

        static void FindFrom(byte _onX, byte _onZ)
        {
            Log($"*****{_onX},{_onZ}*****", false);
            maps[currentMap].WriteBit(_onX, _onZ, BitLocation.inDriveWay, true);
            if (Math.Abs(_onZ - toPosZ) >= Math.Abs(_onX - toPosX))
            {
                if (_onZ <= toPosZ)
                {
                    if (!maps[currentMap].ReadBit(_onX, _onZ, BitLocation.backWall)) FindTile(_onX, (byte)(_onZ + 1));
                    returningFromGoal = false;
                }
                else
                {
                    if (!maps[currentMap].ReadBit(_onX, _onZ, BitLocation.frontWall)) FindTile(_onX, (byte)(_onZ - 1));
                    returningFromGoal = false;
                }

                if (_onX <= toPosX)
                {
                    if (!maps[currentMap].ReadBit(_onX, _onZ, BitLocation.leftWall)) FindTile((byte)(_onX + 1), _onZ);
                    returningFromGoal = false;
                }
                else
                {
                    if (!maps[currentMap].ReadBit(_onX, _onZ, BitLocation.rightWall)) FindTile((byte)(_onX - 1), _onZ);
                    returningFromGoal = false;
                }

                if (_onZ > toPosZ)
                {
                    if (!maps[currentMap].ReadBit(_onX, _onZ, BitLocation.backWall)) FindTile(_onX, (byte)(_onZ + 1));
                    returningFromGoal = false;
                }
                else
                {
                    if (!maps[currentMap].ReadBit(_onX, _onZ, BitLocation.frontWall)) FindTile(_onX, (byte)(_onZ - 1));
                    returningFromGoal = false;
                }

                if (_onX > toPosX)
                {
                    if (!maps[currentMap].ReadBit(_onX, _onZ, BitLocation.leftWall)) FindTile((byte)(_onX + 1), _onZ);
                    returningFromGoal = false;
                }
                else
                {
                    if (!maps[currentMap].ReadBit(_onX, _onZ, BitLocation.rightWall)) FindTile((byte)(_onX - 1), _onZ);
                    returningFromGoal = false;
                }
            }
            else
            {
                if (_onX <= toPosX)
                {
                    if (!maps[currentMap].ReadBit(_onX, _onZ, BitLocation.leftWall)) FindTile((byte)(_onX + 1), _onZ);
                    returningFromGoal = false;
                }
                else
                {
                    if (!maps[currentMap].ReadBit(_onX, _onZ, BitLocation.rightWall)) FindTile((byte)(_onX - 1), _onZ);
                    returningFromGoal = false;
                }

                if (_onZ <= toPosZ)
                {
                    if (!maps[currentMap].ReadBit(_onX, _onZ, BitLocation.backWall)) FindTile(_onX, (byte)(_onZ + 1));
                    returningFromGoal = false;
                }
                else
                {
                    if (!maps[currentMap].ReadBit(_onX, _onZ, BitLocation.frontWall)) FindTile(_onX, (byte)(_onZ - 1));
                    returningFromGoal = false;
                }

                if (_onX > toPosX)
                {
                    if (!maps[currentMap].ReadBit(_onX, _onZ, BitLocation.leftWall)) FindTile((byte)(_onX + 1), _onZ);
                    returningFromGoal = false;
                }
                else
                {
                    if (!maps[currentMap].ReadBit(_onX, _onZ, BitLocation.rightWall)) FindTile((byte)(_onX - 1), _onZ);
                    returningFromGoal = false;
                }

                if (_onZ > toPosZ)
                {
                    if (!maps[currentMap].ReadBit(_onX, _onZ, BitLocation.backWall)) FindTile(_onX, (byte)(_onZ + 1));
                    returningFromGoal = false;
                }
                else
                {
                    if (!maps[currentMap].ReadBit(_onX, _onZ, BitLocation.frontWall)) FindTile(_onX, (byte)(_onZ - 1));
                    returningFromGoal = false;
                }
            }
        }

        static void SearchCell(byte _onX, byte _onZ)
        {
            Log($"*****{_onX},{_onZ}*****", false);
            if (_onX == toPosX && _onZ == toPosZ)
            {
                Log("Found " + foundPath.Count + " long", true);

                if (foundPath.Count < savedPath.Count || savedPath.Count == 0) //If we found a better path, save it
                {
                    savedPath = new List<byte[]>(foundPath);
                    if (savedPath.Count == Math.Abs(posX - toPosX) + Math.Abs(posZ - toPosZ)) //If this is the shortest possible path (manhattan distance), we are done
                    {
                        foundWay = true;
                    }
                }
                skipNext = true; //We have found optimal way from last tile, so we don't need to explore it more
                returningFromGoal = true; //We are returning to start again
                return;
            }

            bool returning = returningFromGoal;
            returningFromGoal = false;

            maps[currentMap].WriteBit(_onX, _onZ, BitLocation.inDriveWay, true);
            //Better way when these are equal?
            if (Math.Abs(_onZ - toPosZ) >= Math.Abs(_onX - toPosX))
            {
                if (_onZ <= toPosZ)
                {
                    if (!maps[currentMap].ReadBit(_onX, _onZ, BitLocation.backWall)) FindTile(_onX, (byte)(_onZ + 1));
                }
                else
                {
                    if (!maps[currentMap].ReadBit(_onX, _onZ, BitLocation.frontWall)) FindTile(_onX, (byte)(_onZ - 1));
                }

                if (_onX <= toPosX)
                {
                    if (!maps[currentMap].ReadBit(_onX, _onZ, BitLocation.rightWall)) FindTile((byte)(_onX + 1), _onZ);
                }
                else
                {
                    if (!maps[currentMap].ReadBit(_onX, _onZ, BitLocation.leftWall)) FindTile((byte)(_onX - 1), _onZ);
                }

                if (_onZ > toPosZ)
                {
                    if (!maps[currentMap].ReadBit(_onX, _onZ, BitLocation.backWall)) FindTile(_onX, (byte)(_onZ + 1));
                }
                else
                {
                    if (!maps[currentMap].ReadBit(_onX, _onZ, BitLocation.frontWall)) FindTile(_onX, (byte)(_onZ - 1));
                }

                if (_onX > toPosX)
                {
                    if (!maps[currentMap].ReadBit(_onX, _onZ, BitLocation.rightWall)) FindTile((byte)(_onX + 1), _onZ);
                }
                else
                {
                    if (!maps[currentMap].ReadBit(_onX, _onZ, BitLocation.leftWall)) FindTile((byte)(_onX - 1), _onZ);
                }
            }
            else
            {
                if (_onX <= toPosX)
                {
                    if (!maps[currentMap].ReadBit(_onX, _onZ, BitLocation.rightWall)) FindTile((byte)(_onX + 1), _onZ);
                }
                else
                {
                    if (!maps[currentMap].ReadBit(_onX, _onZ, BitLocation.leftWall)) FindTile((byte)(_onX - 1), _onZ);
                }

                if (_onZ <= toPosZ)
                {
                    if (!maps[currentMap].ReadBit(_onX, _onZ, BitLocation.backWall)) FindTile(_onX, (byte)(_onZ + 1));
                }
                else
                {
                    if (!maps[currentMap].ReadBit(_onX, _onZ, BitLocation.frontWall)) FindTile(_onX, (byte)(_onZ - 1));
                }

                if (_onX > toPosX)
                {
                    if (!maps[currentMap].ReadBit(_onX, _onZ, BitLocation.rightWall)) FindTile((byte)(_onX + 1), _onZ);
                }
                else
                {
                    if (!maps[currentMap].ReadBit(_onX, _onZ, BitLocation.leftWall)) FindTile((byte)(_onX - 1), _onZ);
                }

                if (_onZ > toPosZ)
                {
                    if (!maps[currentMap].ReadBit(_onX, _onZ, BitLocation.backWall)) FindTile(_onX, (byte)(_onZ + 1));
                }
                else
                {
                    if (!maps[currentMap].ReadBit(_onX, _onZ, BitLocation.frontWall)) FindTile(_onX, (byte)(_onZ - 1));
                }
            }

            maps[currentMap].WriteBit(_onX, _onZ, BitLocation.inDriveWay, false);
            returningFromGoal = returning || returningFromGoal;
            if (!returningFromGoal)
            {
                maps[currentMap].WriteBit(_onX, _onZ, BitLocation.mapSearched, true);
            }

            skipNext = false;
        }

        static void FindTile(byte _onX, byte _onZ)
        {
            if (skipNext || foundWay)
                return;

            try
            {
                if ((maps[currentMap].ReadBit(_onX, _onZ, BitLocation.explored) && !maps[currentMap].ReadBit(_onX, _onZ, BitLocation.mapSearched) && 
                    !maps[currentMap].ReadBit(_onX, _onZ, BitLocation.inDriveWay) && !maps[currentMap].ReadBit(_onX, _onZ, BitLocation.blackTile) && 
                    !maps[currentMap].ReadBit(_onX, _onZ, BitLocation.ramp)) || (_onX == toPosX && _onZ == toPosZ))
                {
                    foundPath.Add(new byte[] {_onX, _onZ });
                    SearchCell(_onX, _onZ);
                    if (!foundWay)
                        foundPath.RemoveAt(foundPath.Count - 1);
                }
            }
            catch (Exception e)
            {
                LogException(e);
                for (int i = 0; i < 3; i++) Log("!!!!!!FindTile -- index out of bounds(?)!!!!!!", true);
            }
        }

        /*
        static void FindNZ(byte _onX, byte _onZ)
        {
            if (skipNext)
            {
                return;
            }
            //if (shortenAvailable)
            //{
            //    if (shortenToX == _onX && shortenToZ == _onZ)
            //    {
            //        shortenAvailable = false;
            //    }
            //    else
            //    {
            //        return;
            //    }
            //}
            if (!foundWay && _onZ - 1 >= 0 /*&& !ReadMapBit(_onX, _onZ, shortestPath)) //If there is a position one step +Z
            {
                if (ReadMapBit(_onX, _onZ - 1, BitLocation.explored) && !ReadMapBit(_onX, _onZ - 1, BitLocation.mapSearched) && !ReadMapBit(_onX, _onZ, BitLocation.frontWall) && !ReadMapBit(_onX, _onZ - 1, BitLocation.inDriveWay))
                {
                    driveWay.Add(new byte[] { _onX, (byte)(_onZ - 1) });
                    SearchCell(_onX, (byte)(_onZ - 1));
                    if (!foundWay)
                        driveWay.RemoveAt(driveWay.Count - 1);
                }
            }
        }
        static void FindPZ(byte _onX, byte _onZ)
        {
            if (skipNext)
            {
                return;
            }
            //if (shortenAvailable)
            //{
            //    if (shortenToX == _onX && shortenToZ == _onZ)
            //    {
            //        shortenAvailable = false;
            //    }
            //    else
            //    {
            //        return;
            //    }
            //}
            if (!foundWay && _onZ + 1 < maps[currentMap].GetLength(1)) //If there is a position one step -Z
            {
                if (ReadMapBit(_onX, _onZ + 1, BitLocation.explored) && !ReadMapBit(_onX, _onZ + 1, BitLocation.mapSearched) && !ReadMapBit(_onX, _onZ, BitLocation.backWall) && !ReadMapBit(_onX, _onZ + 1, BitLocation.inDriveWay))
                {
                    driveWay.Add(new byte[] { _onX, (byte)(_onZ + 1) });
                    SearchCell(_onX, (byte)(_onZ + 1));
                    if (!foundWay)
                        driveWay.RemoveAt(driveWay.Count - 1);
                }
            }
        }
        static void FindPX(byte _onX, byte _onZ)
        {
            if (skipNext)
            {
                return;
            }
            //if (shortenAvailable)
            //{
            //    if (shortenToX == _onX && shortenToZ == _onZ)
            //    {
            //        shortenAvailable = false;
            //    }
            //    else
            //    {
            //        return;
            //    }
            //}
            if (!foundWay && _onX + 1 < maps[currentMap].GetLength(1)) //If there is a position one step +X
            {
                if (ReadMapBit(_onX + 1, _onZ, BitLocation.explored) && !ReadMapBit(_onX + 1, _onZ, BitLocation.mapSearched) && !ReadMapBit(_onX, _onZ, BitLocation.rightWall) && !ReadMapBit(_onX + 1, _onZ, BitLocation.inDriveWay))
                {
                    driveWay.Add(new byte[] { (byte)(_onX + 1), _onZ });
                    SearchCell((byte)(_onX + 1), _onZ);
                    if (!foundWay)
                        driveWay.RemoveAt(driveWay.Count - 1);
                }
            }
        }
        static void FindNX(byte _onX, byte _onZ)
        {
            if (skipNext)
            {
                return;
            }
            //if (shortenAvailable)
            //{
            //    if (shortenToX == _onX && shortenToZ == _onZ)
            //    {
            //        shortenAvailable = false;
            //    }
            //    else
            //    {
            //        return;
            //    }
            //}

            if (!foundWay && _onX - 1 >= 0) //If there is a position one step -X
            {
                if (ReadMapBit(_onX - 1, _onZ, BitLocation.explored) && !ReadMapBit(_onX - 1, _onZ, BitLocation.mapSearched) && !ReadMapBit(_onX, _onZ, BitLocation.leftWall) && !ReadMapBit(_onX - 1, _onZ, BitLocation.inDriveWay))
                {
                    driveWay.Add(new byte[] { (byte)(_onX - 1), _onZ });
                    SearchCell((byte)(_onX - 1), _onZ);
                    if (!foundWay)
                        driveWay.RemoveAt(driveWay.Count - 1);
                }
            }
        }*/
        /*
        static void ReturnToStart()
        {
            FindPathTo(startPosX, startPosZ, 0);
            Thread.Sleep(100);

            while (driveWay.Count > 0)
            {
                TurnTo(driveWay[0]);
                driveWay.RemoveAt(0);
                Drive();
            }

            Thread.Sleep(30000);
        }
        
        static void FindExploredCells(int _onX, int _onZ)
        {
            if (_onX == toPosX && _onZ == toPosZ)
            {
                foundWay = true;
            }

            if (!foundWay && !ReadMapBit(_onX, _onZ, 5) && !ShortenPath(_onX, _onZ))
            {
                WriteMapBit(_onX, _onZ, mapSearched, true);

                if (_onZ > toPosZ)
                {
                    if (!foundWay && _onZ - 1 >= 0) //If there is a position one step +Z
                    {
                        if (ReadMapBit(_onX, _onZ - 1, explored) && !ReadMapBit(_onX, _onZ - 1, mapSearched) && !ReadMapBit(_onX, _onZ, 0) && !ReadMapBit(_onX, _onZ - 1, blackTile))
                        {
                            driveWay.Add(0);
                            FindExploredCells(_onX, _onZ - 1);
                            if (!foundWay)
                                driveWay.RemoveAt(driveWay.Count - 1);
                        }
                    }
                }
                else
                {
                    if (!foundWay && _onZ + 1 < maps[currentMap].GetLength(1)) //If there is a position one step -Z
                    {
                        if (ReadMapBit(_onX, _onZ + 1, explored) && !ReadMapBit(_onX, _onZ + 1, mapSearched) && !ReadMapBit(_onX, _onZ, 2) && !ReadMapBit(_onX, _onZ + 1, blackTile))
                        {
                            driveWay.Add(2);
                            FindExploredCells(_onX, _onZ + 1);
                            if (!foundWay)
                                driveWay.RemoveAt(driveWay.Count - 1);
                        }
                    }
                }

                if (_onX > toPosX)
                {
                    if (!foundWay && _onX + 1 < maps[currentMap].GetLength(0)) //If there is a position one step +X
                    {
                        if (ReadMapBit(_onX + 1, _onZ, explored) && !ReadMapBit(_onX + 1, _onZ, mapSearched) && !ReadMapBit(_onX, _onZ, 3) && !ReadMapBit(_onX + 1, _onZ, blackTile))
                        {
                            driveWay.Add(3);
                            FindExploredCells(_onX + 1, _onZ);
                            if (!foundWay)
                                driveWay.RemoveAt(driveWay.Count - 1);
                        }
                    }

                }
                else
                {
                    if (!foundWay && _onX - 1 >= 0) //If there is a position one step -X
                    {
                        if (ReadMapBit(_onX - 1, _onZ, explored) && !ReadMapBit(_onX - 1, _onZ, mapSearched) && !ReadMapBit(_onX, _onZ, 1) && !ReadMapBit(_onX - 1, _onZ, blackTile))
                        {
                            driveWay.Add(1);
                            FindExploredCells(_onX - 1, _onZ);
                            if (!foundWay)
                                driveWay.RemoveAt(driveWay.Count - 1);
                        }
                    }
                }

                if (_onZ <= toPosZ)
                {
                    if (!foundWay && _onZ - 1 >= 0) //If there is a position one step +Z
                    {
                        if (ReadMapBit(_onX, _onZ - 1, explored) && !ReadMapBit(_onX, _onZ - 1, mapSearched) && !ReadMapBit(_onX, _onZ, 0) && !ReadMapBit(_onX, _onZ - 1, blackTile))
                        {
                            driveWay.Add(0);
                            FindExploredCells(_onX, _onZ - 1);
                            if (!foundWay)
                                driveWay.RemoveAt(driveWay.Count - 1);
                        }
                    }
                }
                else
                {
                    if (!foundWay && _onZ + 1 < maps[currentMap].GetLength(1)) //If there is a position one step -Z
                    {
                        if (ReadMapBit(_onX, _onZ + 1, explored) && !ReadMapBit(_onX, _onZ + 1, mapSearched) && !ReadMapBit(_onX, _onZ, 2) && !ReadMapBit(_onX, _onZ + 1, blackTile))
                        {
                            driveWay.Add(2);
                            FindExploredCells(_onX, _onZ + 1);
                            if (!foundWay)
                                driveWay.RemoveAt(driveWay.Count - 1);
                        }
                    }
                }

                if (_onX <= toPosX)
                {
                    if (!foundWay && _onX + 1 < maps[currentMap].GetLength(0)) //If there is a position one step +X
                    {
                        if (ReadMapBit(_onX + 1, _onZ, explored) && !ReadMapBit(_onX + 1, _onZ, mapSearched) && !ReadMapBit(_onX, _onZ, 3) && !ReadMapBit(_onX + 1, _onZ, blackTile))
                        {
                            driveWay.Add(3);
                            FindExploredCells(_onX + 1, _onZ);
                            if (!foundWay)
                                driveWay.RemoveAt(driveWay.Count - 1);
                        }
                    }
                }
                else
                {
                    if (!foundWay && _onX - 1 >= 0) //If there is a position one step -X
                    {
                        if (ReadMapBit(_onX - 1, _onZ, explored) && !ReadMapBit(_onX - 1, _onZ, mapSearched) && !ReadMapBit(_onX, _onZ, 1) && !ReadMapBit(_onX - 1, _onZ, blackTile))
                        {
                            driveWay.Add(1);
                            FindExploredCells(_onX - 1, _onZ);
                            if (!foundWay)
                                driveWay.RemoveAt(driveWay.Count - 1);
                        }
                    }
                }
            }
        }

        
        static bool ShortenPath(int _onX, int _onZ)
        {
            if (shortenAvailable)
            {
                if (shortenToX == _onX && shortenToZ == _onZ)
                {
                    shortenAvailable = false;
                }
                else
                {
                    WriteMapBit(_onX, _onZ, mapSearched, false);
                    return true;
                }
            }

            if (!foundWay && !ReadMapBit(_onX, _onZ, mapSearched))
            {
                if (_onZ - 1 >= 0) //If there is a position one +Z, direction is W
                {
                    if (ReadMapBit(_onX, _onZ - 1, explored) && !ReadMapBit(_onX, _onZ, 0))
                    {
                        if (ReadMapBit(_onX, _onZ - 1, mapSearched) && driveWay.Last() != 2)
                        {
                            shortenToX = _onX;
                            shortenToZ = _onZ - 1;
                            shortenAvailable = true;
                            return true;
                        }
                    }
                }

                if (_onX - 1 >= 0) //If there is a position one step -X
                {
                    if (ReadMapBit(_onX - 1, _onZ, explored) && !ReadMapBit(_onX, _onZ, 1))
                    {
                        if (ReadMapBit(_onX - 1, _onZ, mapSearched) && driveWay.Last() != 3)
                        {
                            shortenToX = _onX - 1;
                            shortenToZ = _onZ;
                            shortenAvailable = true;
                            return true;
                        }
                    }
                }

                if (_onZ + 1 < maps[currentMap].GetLength(1)) //If there is a position one step -Z
                {
                    if (ReadMapBit(_onX, _onZ + 1, explored) && !ReadMapBit(_onX, _onZ, 2))
                    {
                        if (ReadMapBit(_onX, _onZ + 1, mapSearched) && driveWay.Last() != 0)
                        {
                            shortenToX = _onX;
                            shortenToZ = _onZ + 1;
                            shortenAvailable = true;
                            return true;
                        }
                    }
                }

                if (_onX + 1 < maps[currentMap].GetLength(0)) //If there is a position one step +X
                {
                    if (ReadMapBit(_onX + 1, _onZ, explored) && !ReadMapBit(_onX, _onZ, 3))
                    {
                        if (ReadMapBit(_onX + 1, _onZ, mapSearched) && driveWay.Last() != 1)
                        {
                            shortenToX = _onX + 1;
                            shortenToZ = _onZ;
                            shortenAvailable = true;
                            return true;
                        }
                    }
                }
            }

            return false;
        }
        */
    }
}
