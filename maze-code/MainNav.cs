using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using EnumCommand;

namespace SerialConsole
{
    internal partial class Program
    {
        //******** Navigation & Localization ********

        static int highestX = 25;
        static int highestZ = 25;
        static int lowestX = 25;
        static int lowestZ = 25;
        static int toPosX;
        static int toPosZ;
        static int posX = 25;
        static int posZ = 25;

        static int direction = 0;

        static bool locationUpdated;

        static bool frontPresent;
        static bool leftPresent;
        static bool rightPresent;

        static List<byte[]> driveWay = new();

        enum Directions
        {
            front,
            left,
            back,
            right
        }

        //static bool shortenAvailable;

        //******** Timing & exiting ********

        static readonly Stopwatch timer = new();

        const double SecoundsPerDrive = 4.5;
        const double SecoundsPerTurn = 2.5;
        static int distanceToStart = 0;
        static bool distanceUpdated;
        static volatile bool exit = false;

        //******** Team ********
        //~Köra snabbare till utforskade tiles
        //~if error - send to auriga which turns on lamps to suggest lop?
        //~VIKTIG:Hur ska timern veta om reseten är första inför första körning, eller inte - första reset (rutin)(?) -hur gör vi med kalibrering???
        //Send to auriga or recive from if there is likely an obstacle present?
        //~Edit Baud Rate to read faster, 38,4 k seems good -- does not work??
        //~Always send in beginning like in drive to avoid deadlock
        //~Resume method?

        //******** Tests ********
        //Test (+finish?) sinceCheckpoint??
        //Test reset so that it doesn't do anything unexpected
        //Double check ramp data and ramp methods

        //******** Possibilities ********
        //draw map to file?
        //private Directions ToLocalDirection() -- good to have - or just (int)Directions + direction to make global
        //Compare map to sensor (+vision?) data?

        //******** Solve/look into NOW ********
        //Make it so that if I cannot find way to start - go down ramp
        //optimiserad svängning vid kit dropping -------started in DropKits() -- finish asap
        //Sätt ingen kit check förrän i turnLogic för att optimisera
        //Fix line ~256 when ramps are fixed
        //Check that mapping + writing it to file is correct and the search alg.
        //Fault in left wall backup nav
        //Fix what to do when driveWay seeking fails
        //Direction when i drive OUT of checkpoint
        //Update checkpoint direction of that when it left tile
        //victims are saved until end of drive - problem if identified on first tile in drive -- send message in middle of drive?

        //****Little lower, still high****
        //OM VI KAN UPPSKATTA HUR HÖG + LÅNG EN RAMP ÄR - (GENOM GYRO + STRÄCKA KÖRD PÅ RAMP) KAN VI HA EN KARTA FÖR VARJE "NIVÅ", add height to mapinfo and lenth to calc position
        // ------------------ if so -> make sure crossRoads() on two maps on same height but unconnected works when findway fails
        // ------------------ finish ramp distances in Drive()
        // ------------------ Set ramp to actual ramp instead of tile next to ++ put walls for if there is two next to each other - or search algorithm ramps
        //TurnTo(direction - 2); DriveDeaf(); --When we don't want to go up ramp, panic solution
        //Search algorithm with ramps?
        //--Change map in search algorithm when searching to ramp
        //use GoBackLevel???
        //Förbättra felhantering
        //GOOD IDEA: Own delay method so that i can do work while sleeping, like finding distance to start
        //Full reset -- if we reset from start tile, also re-startup, but do not add another thread for victim


        //******** Solve/look into ********
        // TRiple slash /// for summaries above methods - useful, especially if put into classes
        //File.AppendAllText("log.txt", $"found:{dropAmount},{dropSide}"); + more logging?
        //Check driveblind() and its usage
        //Resume method? - No answer from CheckAndDropKits in drive? - new method which doesn't excpect answer?
        //Testa serial skriva och läsa "samtidigt"; lite innan och efter skickning


        //******** Solved/done? (look into/could be bugs) ********
        //Black tiles and ramp tiles in search tiles
        //Count ramp as one tile? --rewrite UpRamp and DownRamp
        //SEARCH DIRECTLY TO TILE !!!!!!!!!!!!!!!!!
        //Black tiles & ramp in Search Algorithm
        //Search directly to explore tile instead of crossroad
        //Use remaining 4 bits for ramp location?
        //change string lists to int[] lists? or own class lists - done?
        //CrossTiles to tile done?
        //Reset driveWay and update map if driveWay fails
        //Remap on wall "hit" in drive -
        //Check and log ramp data -- seems to be out of bounds??

        // ********************************** Main Loop & Startup ********************************** 
        #region Main

        static void Main()
        {
            Console.CancelKeyPress += delegate (object? o, ConsoleCancelEventArgs e)
            {
                e.Cancel = false;
                Exit();
                throw new Exception("EXIT");
            };

            Thread.Sleep(100);

            StartUp();
            Log("Finished startup, drive loop starting", false);

        LoopStart:
            try
            {
                //Main loop
                while (!exit)
                {
                    reset = false;
                    //CheckTimer();
                    Turnlogic();
                    Drive(true);
                    Thread.Sleep(10);
                }
            }
            catch (Exception e)
            {
                LogException(e);
                if (!exit)
                {
                    BackupNav();
                    goto LoopStart;
                }
            }
        }

        static void StartUp()
        {
#if DEBUG
            File.WriteAllText("log.txt", "Testing start:: ");
#endif
            listener.Start();
            Log("Waiting for connection...", true);
            client = listener.AcceptTcpClient();
            Log("Client accepted", true);

            OpenPort:
            try
            {
                serialPort1.Open();
            }
            catch (Exception e)
            {
                LogException(e);
                Log("Cannot open port, try these: ");
                string[] _ports = SerialPort.GetPortNames();
                foreach (string _port in _ports)
                {
                    Log(_port);
                }

                foreach (string _port in _ports)
                {
                    try
                    {
                        if (_port.Contains("/dev/ttyUSB") || _port.Contains("COM") || _port.Contains("/dev/ttyS"))
                        {
                            serialPort1.PortName = _port;
                            serialPort1.Open();
                        }
                    }
                    catch (Exception ex)
                    {
                        LogException(ex);
                        Log("Cannot open port " + _port);
                    }
                }
                if (!serialPort1.IsOpen)
                {
                    serialPort1.PortName = "/dev/ttyS0";
                    Thread.Sleep(200);
                    goto OpenPort;
                }
            }
            Log($"Connected to {serialPort1.PortName}", true);

            Thread.Sleep(100);
            if (serialPort1.BytesToRead != 0)
                serialPort1.ReadExisting();
            serialPort1.DiscardInBuffer();
            serialPort1.DiscardOutBuffer();
            Thread.Sleep(400);
            Log("Waiting for reset");

            do
            {
                while (serialPort1.BytesToRead == 0)
                {
                    Thread.Sleep(20);
                }
            } while (!serialPort1.ReadLine().Contains(RecivedCommands.LOP.GetCommand()));
            timer.Start();

            Thread.Sleep(200);

            currentMap = 0;
            direction = 0;
            maps.Capacity = 15;
            maps.Add(new Map(50));
            //mapInfo.Add(new int[] { 25, 25, direction });
            maps[0].Clear();

            Log("Updating first tile");
            UpdateMapFull();
            AddSinceCheckPoint();

            Thread.Sleep(300);
            Thread serverThread = new(ServerLoop);
            serverThread.Start();

            Log("Done with startup", true);
        }
        #endregion

        // ********************************** Navigation ********************************** 
        #region Nav

        static void Turnlogic()
        {
            if (reset) return;

            bool[] _surroundingTiles = {ShouldGoTo(posX, posZ - 1) && !ReadHere(BitLocation.frontWall), //NOT BLACK TILES   ///false,false, false, true
                                        ShouldGoTo(posX - 1, posZ) && !ReadHere(BitLocation.leftWall),
                                        ShouldGoTo(posX, posZ + 1) && !ReadHere(BitLocation.backWall),
                                        ShouldGoTo(posX + 1, posZ) && !ReadHere(BitLocation.rightWall)};

            int _unExpTiles = 0;
            for (int i = 0; i < _surroundingTiles.Length; i++)
            {
                Log($"{(Directions)i} tile around: {_surroundingTiles[i]}", false);
                if (_surroundingTiles[i])
                    _unExpTiles++;
            }

        NavLogic:
            if (reset) return;
            
            Thread.Sleep(20);

#warning change code below when ramps are done
            //+ all startpos stuff when ramps are done (or change startpos in map + add parameter in initializer)
            //CHANGE CODE BELOW WHEN RAMP STUFF IS CHANGED
            if (driveWay.Count == 0 && maps[currentMap].CrossTiles.Count == 0 && _unExpTiles == 0 && posX == maps[currentMap].StartPosX && posZ == maps[currentMap].StartPosZ)
            {
                CheckAndDropKits(true);
                if (currentMap == 0)
                {
                    timer.Stop();
                    Log("DONE", true);
                    Thread.Sleep(20_000);
                    Exit();
                    return;
                }
                else
                {
                    //We want to go to the first ramp, where this map was first discovered, so that we can go back to start
                    TurnTo(maps[currentMap].GetFirstRampAt((byte)posX, (byte)posZ)[(int)RampStorage.RampDirection]); //THIS IS STARTPOS
                    return;
                }
            }

            if (driveWay.Count > 0)
            {
                if (_unExpTiles != 0)
                {
                    //maps[currentMap].CrossTiles.Add(new byte[] { (byte)posX, (byte)posZ }); //CLEAR SINCECHECKP. FROM COORDS ON RESET ++ ADD CURRENT MAP
                    for (int i = direction + 1; i > direction - 3; i--)
                    {
                        if (_surroundingTiles[FixDirection(i)])
                        {
                            maps[currentMap].CrossTiles.Add(DirToTile(i, (byte)posX, (byte)posZ)); //CLEAR SINCECHECKP. FROM COORDS ON RESET ++ ADD CURRENT MAP
                        }
                    }
                }
                while (driveWay[0][0] == posX && driveWay[0][1] == posZ)
                {
                    driveWay.RemoveAt(0);
                }
                Log($"Is turning to {driveWay[0][0]},{driveWay[0][1]}", true);
                TurnTo(TileToDirection(driveWay[0]));
                driveWay.RemoveAt(0);
            }
            else if (_unExpTiles > 0)
            {
                //if (_unExpTiles != 1)
                //{
                //    //foreach (bool _surrTile in _surroundingTiles)
                //    maps[currentMap].CrossTiles.Add(new byte[] { (byte)posX, (byte)posZ }); //CLEAR SINCECHECKP. FROM COORDS ON RESET ++ ADD CURRENT MAP
                //}
                //for (int i = direction + 1; i > direction - 3; i--)
                //{
                //    if (_surroundingTiles[FixDirection(i)])
                //    {
                //        TurnTo(i);
                //        break;
                //    }
                //}
                for (int i = direction - 2; i <= direction + 1; i++)
                {
                    if (_surroundingTiles[FixDirection(i)])
                    {
                        //UnityEngine.Debug.Log($"ADDED({i}): {DirToTile(i, posX, posZ)[0]},{DirToTile(i, posX, posZ)[1]}");
                        if (_unExpTiles != 1)
                        {
                            while (maps[currentMap].CrossTiles.Contains(DirToTile(i, (byte)posX, (byte)posZ)))
                            {
                                maps[currentMap].CrossTiles.Remove(DirToTile(i, (byte)posX, (byte)posZ));
                            }
                            maps[currentMap].CrossTiles.Add(DirToTile(i, (byte)posX, (byte)posZ)); //CLEAR SINCECHECKP. FROM COORDS ON RESET ++ ADD CURRENT MAP
                        }
                    }
                }
                for (int i = direction + 1; i >= direction - 2; i--)
                {
                    if (_surroundingTiles[FixDirection(i)])
                    {
                        TurnTo(FixDirection(i));
                        break;
                    }
                }
            }
            else if (_unExpTiles == 0)
            {
                if (maps[currentMap].CrossTiles.Count > 1)
                {
                    byte[] _currentCoords = new byte[] { (byte)posX, (byte)posZ };
                    while (maps[currentMap].CrossTiles.Contains(_currentCoords))
                    {
                        maps[currentMap].CrossTiles.Remove(_currentCoords);
                    }

                    int _crossX = maps[currentMap].CrossTiles.Last()[0];
                    int _crossZ = maps[currentMap].CrossTiles.Last()[1];

                    while (!ShouldGoTo(_crossX, _crossZ))
                    {
                        maps[currentMap].CrossTiles.RemoveAt(maps[currentMap].CrossTiles.Count - 1);

                        if (maps[currentMap].CrossTiles.Count == 0)
                        {
                            driveWay = new List<byte[]>(PathTo(maps[currentMap].StartPosX, maps[currentMap].StartPosZ));
                            goto NavLogic;
                        }

                        _crossX = maps[currentMap].CrossTiles.Last()[0];
                        _crossZ = maps[currentMap].CrossTiles.Last()[1];
                    }

                    Log($"Finding path to {_crossX},{_crossZ}", true);
                    driveWay = new List<byte[]>(PathTo(_crossX, _crossZ));

                    if (driveWay.Count > 0)
                    {
                        maps[currentMap].CrossTiles.RemoveAt(maps[currentMap].CrossTiles.Count - 1);
                    }
                    else
                    {
                        for (int i = maps[currentMap].CrossTiles.Count-1; i <= 0; i--)
                        {
                            //(maps[currentMap].CrossTiles[^i], maps[currentMap].CrossTiles[^1]) = (maps[currentMap].CrossTiles[^1], maps[currentMap].CrossTiles[^i]); //BAD SOLUTION, IF ONE IS BAD, BOTH ARE LIKELY BAD; travel up ramp instead?
                            driveWay = new List<byte[]>(PathTo(maps[currentMap].CrossTiles[i][1], maps[currentMap].CrossTiles[i][1]));
                            if (driveWay.Count > 0) goto NavLogic;
                        }
                        Log("SOMETHING PROBABLY WRONG WITH RAMP OR MAP, LIKELY DUAL RAMP, trying to solve", true);
                        GoToRamp(maps[currentMap].GetRampAt((byte)rampCount));
                    }
                    goto NavLogic;
                }
                else
                {
                    Log("Going back to start", true);
                    driveWay = new List<byte[]>(PathTo(maps[currentMap].StartPosX, maps[currentMap].StartPosZ));
                    goto NavLogic;
                }
            }
            else
            {
                Log("TURN ISSUE AIUAUIWS", true);
            }
        }

        static void BackupNav()
        {
            for (int i = 0; i < 10; i++)  Log("!!!!!!!!!!!!!!!PROBLEM PROBLEM NAVIGATION FAILED!!!!!!!!!!!!!!!", true);
            Thread.Sleep(1_000);

            while (true)
            {
                if (reset || exit)
                    return;

                while (true /*!leftPresent || frontPresent || ReadNextTo(posX, posZ, blackTile, Directions.front) || RampCheck(direction)*/)
                {
                    SensorCheck();
                    if (reset || exit)
                        return;
                    if (!leftPresent && !ReadNextTo(BitLocation.blackTile, Directions.left) /*&& !RampCheck(direction + 1)*/)
                    {
                        Turn('l');
                        Thread.Sleep(200);
                        Drive(false);
                    }
                    else if (frontPresent || ReadNextTo(BitLocation.blackTile, Directions.front) /*|| RampCheck(direction)*/)
                    {
                        Log($"BlackTile to left: {ReadNextTo(BitLocation.blackTile, Directions.left)}; Blacktile in front: {ReadNextTo(BitLocation.blackTile, Directions.front)}", false);
                        Turn('r');
                        Thread.Sleep(100);
                    }
                    else
                    {
                        CheckAndDropKits(true);
                        Log($"BlackTile to left: {ReadNextTo(BitLocation.blackTile, Directions.left)}; Blacktile in front: {ReadNextTo(BitLocation.blackTile, Directions.front)}", false);
                        break;
                    }
                    Thread.Sleep(10);
                }

                Drive(false); //Do not check for ramps, we want to have 'dumber' code so less can go wrong
            }
        }

        #endregion

        // ********************************** Ramps ********************************** 

        static void RampDriven(int _rampDirection)
        {
            try
            {
                if (!maps[currentMap].FindRamp((byte)posX, (byte)posZ, (byte)direction))
                {
                    //Update and save this map
                    WriteNextTo(BitLocation.ramp, true, Directions.front);
                    WriteNextTo(BitLocation.explored, true, Directions.front);

                    Log("Travelled ramp, was a new ramp _-_-_-_-_-_-_-_-_-_-", true);

                    byte _fromMap = (byte)currentMap;
                    maps[currentMap].AddRamp((byte)posX, (byte)posZ, (byte)_rampDirection, (byte)rampCount, (byte)(maps.Count - 1));
                    Log($"Saved ramp: x:{posX}, z:{posZ}, dir:{_rampDirection}, rampCount:{rampCount}, map:{maps.Count - 1}", false);

                    //Setup new map
                    maps.Add(new Map(50));
                    currentMap = maps.Count - 1;
                    posX = maps[currentMap].StartPosX;
                    posZ = maps[currentMap].StartPosZ;
                    maps[currentMap].AddRamp((byte)posX, (byte)posZ, (byte)FixDirection(_rampDirection - 2), (byte)rampCount, _fromMap);

                    maps[currentMap].Clear();
                    locationUpdated = true;
                    rampCount++;
                    Log($"NEW: x:{posX}, z:{posZ}, dir:{direction}, map:{currentMap}", true);

                    WriteNextTo(BitLocation.ramp, true, Directions.back);
                }
                else
                {
                    Log("Ramp was a previously used ramp _-_-_-_-_-_-_-_-_-_-", true);
                    WriteNextTo(BitLocation.explored, true, Directions.front);

                    //Update data
                    byte[] currentRamp = maps[currentMap].GetRampAt((byte)posX, (byte)posZ, (byte)direction);
                    byte[] newMapInfo = maps[currentRamp[(int)RampStorage.ConnectedMap]].GetRampAt(currentRamp[(int)RampStorage.RampIndex]);
                    posX = newMapInfo[(int)RampStorage.XCoord];
                    posZ = newMapInfo[(int)RampStorage.ZCoord];
                    //direction = newMapInfo[(int)RampStorage.RampDirection];
                    //UpdateDirection(2);
                    currentMap = currentRamp[(int)RampStorage.ConnectedMap];
                    locationUpdated = true;

                    Log($"NEW: x:{posX}, z:{posZ}, dir:{direction}, map:{currentMap}", true);
                }
            }
            catch (Exception e)
            {
                LogException(e);
                throw new Exception("Failed ramp driving", e);
            }
        }


        //static void DownRamp()
        //{

        //    if (currentMap != 0)//Hur vet vi
        //    {
        //    }
        //    else
        //    {
        //        Console.WriteLine("ERROR TRIED TO GO DOWN RAMP VIOAFUIEB");
        //    }
        //}

        //static bool RampCheck(int _direction) //NOT DONE
        //{
        //    if (currentMap != 0)
        //        return false;

        //    while (_direction > 3)
        //    {
        //        _direction -= 4;
        //    }
        //    while (_direction < 0)
        //    {
        //        _direction += 4;
        //    }

        //    if (ReadHere(BitLocation.ramp))
        //    {
        //        foreach (int[] _mapInform in mapInfo)
        //        {
        //            if (_mapInform[2] == _direction)
        //            {
        //                return true;
        //            }
        //        }
        //    }
        //    return false;
        //}



        // ********************************** Timing & exiting ********************************** 
        /*
        static void CheckTimer()
        {
            if (timer.ElapsedMilliseconds > 7*60*1000)//7 min = 420 s = 420 * 10^3 ms
            {
                Console.WriteLine("7 mins passed, returning");
                ReturnToStart();
                timer.Reset();
            }
        }*/

        // ********************************** Helpers/nice to have instead of writing manually ********************************** 
        #region Helpers

        static void DelayThread(int _millis, bool _doWork)
        {
            Stopwatch sw = Stopwatch.StartNew();
            if (_millis > 10 && _doWork)
            {
                if (!distanceUpdated)
                {
#warning NOT DONE
                    //while (currentMap != 0){ } - do smth to check go back
                    if (currentMap == 0)
                    {
                        distanceToStart = PathTo(maps[currentMap].StartPosX, maps[currentMap].StartPosZ).Count;
                        distanceUpdated = true;
                    }
                }
            }

            if (_millis - (int)sw.ElapsedMilliseconds > 1)
            {
                try
                {
                    Thread.Sleep(_millis - (int)sw.ElapsedMilliseconds);
                }
                catch (Exception e)
                {
                    LogException(e);
                    Log("Could not sleep");
                }
            }
        }

        static void Log(string _message)
        {
#if DEBUG
            Console.WriteLine(_message);
#endif
        }

        static void Log(string _message, bool _consoleLog)
        {
#if DEBUG
            File.AppendAllText("log.txt", $"\n{timer.ElapsedMilliseconds}: {_message}");
            if (_consoleLog) Console.WriteLine(_message);
#endif
        }

        static void LogException(Exception e)
        {
            Log($"Exception {e.Message} at {e.Source},{e.TargetSite}", false);
            Log($"Exception -- {e.Message}");
        }

        static void Exit()
        {
            reset = true;
            exit = true;
#if DEBUG
            File.WriteAllText("map.txt", "");
            string[] mapToText = new string[(3 + highestZ - lowestZ)];
            int loops = 0;
            Log($"Lowest: {lowestX},{lowestZ}; Highest: {highestX},{highestZ}", true);
            for (int m = 0; m < maps.Count; m++)
            {
                for (int i = lowestZ - 1; i <= highestZ + 1; i++)
                {
                    for (int j = lowestX - 1; j <= highestX + 1; j++)
                    {
                        string bits = $"{j};{i}:";
                        for (int k = 15; k >= 0; k--)
                        {
                            bits += maps[m].ReadBit(j, i, (BitLocation)k) ? "1" : "0";
                        }

                        mapToText[loops] += bits + ",";
                    }
                    loops++;
                }
                try
                {
                    File.AppendAllText("map.txt",$"map {m}\n" + string.Join('\n', mapToText) + "\n\n");
                    //File.WriteAllText("map.txt", string.Join('\n', mapToText)); // Writes map to text file; previous: /*@"C:\Users\0515frma\Desktop\Test\log.txt"*/
                }
                catch (Exception e)
                {
                    Log($"Could not create log because: ", true);
                    LogException(e);
                }
            }
            Log("done with map file", true);
#endif
            Thread.Sleep(1000);
            listener.Stop();
            serialPort1.Close();
            //throw new Exception();
        }
        #endregion
    }
}
