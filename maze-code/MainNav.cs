﻿using EnumCommand;
using System.Diagnostics;
using System.IO.Ports;

namespace SerialConsole
{
    internal partial class Program
    {
        //******** Navigation & Localization ********

        static int highestX = 25;
        static int highestZ = 25;
        static int lowestX = 25;
        static int lowestZ = 25;
        static int posX = 25;
        static int posZ = 25;

        static int direction = 0;

        static bool locationUpdated;
        static bool turboDrive = false;

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

        const double SecondsPerDrive = 3;
        const double SecondsPerTurn = 3;
        static int secondsToStart = 0;
        static bool startTimeUpdated;
        static volatile bool exit = false;

        static bool timeOut = false;

#warning change before competition
        const int MINUTES = 4;

        /// <summary>
        /// Keep track of the amount of errors, add an amount for each error depending on severity, 
        /// if it is too high we use backup nav
        /// </summary>
        static int errors = 0;

        // ********************************** Main Loop & Startup ********************************** 
        #region Main

        static void Main()
        {
            File.WriteAllText("log.txt", "::::::::::Program start::::::::::");
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
                    if (reset)
                    {
                        reset = false;
                        dropKits = false;
                    }
                    //CheckTimer();
                    Turnlogic();
                    Drive(true, turboDrive);
                    Thread.Sleep(10);

                    if (errors >= 10)
                        throw new Exception("Too many small errors");
                    errors--; //Decrease error amount, so that backup nav is only used if there are multiple errors quickly
                    if (errors < 0) errors = 0;
                    Log($"** ** Errors: {errors} ** **", true);
                }
            }
            catch (Exception e)
            {
                LogException(e);
                if (!exit)
                {
                    BackupNav(); //If we return it is likely due to a reset, which means we can go back to normal navigation
                    goto LoopStart;
                }
            }
        }

        static void StartUp()
        {
            listener.Start();
            Log("Waiting for connection...", true);
            client = listener.AcceptTcpClient();
            Log("Client accepted", true);

            while (!serialPort1.IsOpen)
            {
                serialPort1.PortName = "/dev/ttyS0";
                try
                {
                    serialPort1.DiscardInBuffer();
                    serialPort1.Open();
                }
                catch
                {
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
                        catch
                        {
                            Log("Cannot open port " + _port);
                        }
                    }

                    Thread.Sleep(200);
                }
            }
            Log($"Connected to {serialPort1.PortName}", true);

            Thread.Sleep(100);
            if (serialPort1.BytesToRead != 0)
                serialPort1.ReadExisting();
            Thread.Sleep(100);
            Log("Waiting for reset");

            string _commandRecived;
            do //Wait for calibration start
            {
                while (serialPort1.BytesToRead == 0)
                {
                    Thread.Sleep(20);
                }
                _commandRecived = serialPort1.ReadLine();
            } while (!(_commandRecived.Contains(RecivedCommands.LOP.GetCommand()) && _commandRecived.Contains(RecivedCommands.Calibration.GetCommand())));

            timer.Start();
            _commandRecived = "";
            Log("-Recived colour sensor reset", true);

            do //Wait for program start
            {
                while (serialPort1.BytesToRead == 0)
                {
                    Thread.Sleep(20);
                }
                _commandRecived = serialPort1.ReadLine();
            } while (!_commandRecived.Contains(RecivedCommands.LOP.GetCommand()) || _commandRecived.Contains(RecivedCommands.Calibration.GetCommand()));
            Log("-Recived second reset", true);

            Thread serverThread = new(ServerLoop);
            serverThread.Start();
            Thread.Sleep(200);

            // Setup map and start info

            currentMap = 0;
            direction = 0;
            maps.Capacity = 15;
            maps.Add(new Map(50, 0, 25, 25));
            maps[0].Clear();

            Log("Updating first tile");
            UpdateMapFull(true);
            AddSinceCheckPoint();

            Thread.Sleep(100);
            Log("Done with startup", true);
        }
        #endregion

        // ********************************** Navigation ********************************** 
        #region Nav

        static void Turnlogic()
        {
            if (reset) return;
            turboDrive = false;

            bool[] _surroundingTiles = {ShouldGoTo(posX, posZ - 1) && !ReadHere(BitLocation.frontWall), //NOT BLACK TILES   ///false,false, false, true
                                        ShouldGoTo(posX - 1, posZ) && !ReadHere(BitLocation.leftWall),
                                        ShouldGoTo(posX, posZ + 1) && !ReadHere(BitLocation.backWall),
                                        ShouldGoTo(posX + 1, posZ) && !ReadHere(BitLocation.rightWall)};

            int _unExpTiles = 0;
            for (int i = 0; i < _surroundingTiles.Length; i++)
            {
                Log($"Global {(Directions)i} tile around: {_surroundingTiles[i]}", false);
                if (_surroundingTiles[i])
                    _unExpTiles++;
            }

        NavLogic:
            if (reset) return;
            
            Thread.Sleep(20);

            //+ all startpos stuff when ramps are done (or change startpos in map + add parameter in initializer)
            //CHANGE CODE BELOW WHEN RAMP STUFF IS CHANGED
            if (((driveWay.Count == 0 && maps[currentMap].CrossTiles.Count == 0 && _unExpTiles == 0) || (timeOut)) && posX == maps[currentMap].StartPosX && posZ == maps[currentMap].StartPosZ && currentMap == 0)
            {
                timer.Stop();
                Log("DONE", true);
                Thread.Sleep(20_000);
                Exit();
                return;
                
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

                if (!reset)
                {
                    turboDrive = true;
                    driveWay.RemoveAt(0);
                }
            }
            else if (_unExpTiles > 0)
            {
                for (int i = direction - 2; i <= direction + 1; i++)
                {
                    if (_surroundingTiles[FixDirection(i)])
                    {
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
                            FindPathHere(maps[currentMap].StartPosX, maps[currentMap].StartPosZ);
                            if (driveWay.Count > 0)
                                goto NavLogic;
                            throw new Exception($"DID NOT FIND PATH TO (map){currentMap} START XZ");
                        }

                        _crossX = maps[currentMap].CrossTiles.Last()[0];
                        _crossZ = maps[currentMap].CrossTiles.Last()[1];
                    }

                    Log($"Finding path to {_crossX},{_crossZ}", true);
                    FindPathHere(_crossX, _crossZ);

                    if (driveWay.Count > 0)
                    {
                        maps[currentMap].CrossTiles.RemoveAt(maps[currentMap].CrossTiles.Count - 1);
                    }
                    else
                    {
                        for (int i = maps[currentMap].CrossTiles.Count-1; i <= 0; i--)
                        {
                            //(maps[currentMap].CrossTiles[^i], maps[currentMap].CrossTiles[^1]) = (maps[currentMap].CrossTiles[^1], maps[currentMap].CrossTiles[^i]); //BAD SOLUTION, IF ONE IS BAD, BOTH ARE LIKELY BAD; travel up ramp instead?
                            FindPathHere(maps[currentMap].CrossTiles[i][1], maps[currentMap].CrossTiles[i][1]);
                            if (driveWay.Count > 0) goto NavLogic;
                        }
                        Log("SOMETHING PROBABLY WRONG WITH RAMP OR MAP, MAYBE DUAL RAMP, trying to solve", true);
                        GoToRamp(maps[currentMap].GetRampAt((byte)(rampCount-1)));
                    }
                    goto NavLogic;
                }
                else
                {
                    Log("Going back to start", true);
                    FindPathHere(maps[currentMap].StartPosX, maps[currentMap].StartPosZ);
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
                        CheckAndDropKits(true);
                        Turn('l');
                        CheckAndDropKits(true);
                        Thread.Sleep(100);
                        Drive(false, false);
                    }
                    else if (frontPresent || ReadNextTo(BitLocation.blackTile, Directions.front) /*|| RampCheck(direction)*/)
                    {
                        CheckAndDropKits(true);
                        Turn('r');
                        Thread.Sleep(50);
                    }
                    else
                    {
                        CheckAndDropKits(true);
                        break;
                    }
                    Thread.Sleep(10);
                }

                Drive(false, false); //Do not check for ramps, we want to have 'dumber' code so less can go wrong
                CheckAndDropKits(true);
            }
        }

        #endregion

        // ********************************** Ramps ********************************** 

        static void RampDriven(int _rampDirection, int _length, int _height)
        {
            try
            {
                RampSizeFix(ref _length, ref _height);

                if (!maps[currentMap].FindRamp((byte)posX, (byte)posZ, (byte)direction)) //New ramp
                {
                    Log("-_-_-_-_-_-_-_-_-_-_ Travelled ramp, was a new ramp _-_-_-_-_-_-_-_-_-_-", true);
                    //Update and save this map
                    WriteNextTo(BitLocation.ramp, true, Directions.front);
                    WriteNextTo(BitLocation.explored, true, Directions.front);

                    byte[] _rampTile = DirToTile(direction, (byte)posX, (byte)posZ);
                    sinceCheckpoint.Add(new byte[] { _rampTile[0], _rampTile[1], (byte)currentMap });

                    byte _fromMap = (byte)currentMap;
                    maps[currentMap].AddRamp((byte)posX, (byte)posZ, (byte)_rampDirection, (byte)rampCount, (byte)(maps.Count - 1));

                    currentHeight += _height;
                    UpdateRampLocation(_length);
                    Log($"Saved ramp: x:{posX}, z:{posZ}, dir:{_rampDirection}, rampCount:{rampCount}, map:{maps.Count - 1}", false);
                    Log($"new height {currentHeight}, map length = {_length}", true);

                    //Check if this seems to be a new map or an old map
                    int _mapIndex = FindMapAtHeight(currentHeight);

                    if (_mapIndex != -1) //Old map but new ramp
                    {
                        Log("____---- OLD MAP; NEW RAMP ----____");
                        currentMap = _mapIndex;
                        maps[currentMap].AddRamp((byte)posX, (byte)posZ, (byte)FixDirection(_rampDirection - 2), (byte)rampCount, _fromMap);
                        rampCount++;

                        currentHeight = maps[currentMap].Height;
                        WriteNextTo(BitLocation.ramp, true, Directions.back);
                        _rampTile = DirToTile(direction - 2, (byte)posX, (byte)posZ);
                        sinceCheckpoint.Add(new byte[] { _rampTile[0], _rampTile[1], (byte)currentMap });
                    }
                    else //New map
                    {
                        Log("____---- NEW MAP; NEW RAMP ----____");

                        //Setup new map
                        maps.Add(new Map(50, currentHeight, posX, posZ));
                        currentMap = maps.Count - 1;

                        maps[currentMap].AddRamp((byte)posX, (byte)posZ, (byte)FixDirection(_rampDirection - 2), (byte)rampCount, _fromMap);
                        maps[currentMap].Clear();
                        rampCount++;
                        Log($"NEW: x:{posX}, z:{posZ}, dir:{direction}, map:{currentMap}", true);

                        WriteNextTo(BitLocation.ramp, true, Directions.back);
                        _rampTile = DirToTile(direction - 2, (byte)posX, (byte)posZ);
                        sinceCheckpoint.Add(new byte[] { _rampTile[0], _rampTile[1], (byte)currentMap });
                    }
                }
                else //Used ramp
                {
                    Log("-_-_-_-_-_-_-_-_-_-_ Ramp was a previously used ramp _-_-_-_-_-_-_-_-_-_-", true);
                    if (!ReadNextTo(BitLocation.explored, Directions.front))
                    { 
                        WriteNextTo(BitLocation.explored, true, Directions.front);
                        maps[currentMap].AddBitInfo(DirToTile(direction, (byte)posX, (byte)posZ), BitLocation.explored);
                    }

                    //Get data
                    byte[] currentRamp = maps[currentMap].GetRampAt((byte)posX, (byte)posZ, (byte)direction);
                    byte[] newMapInfo = maps[currentRamp[(int)RampStorage.ConnectedMap]].GetRampAt(currentRamp[(int)RampStorage.RampIndex]);

                    //Update data
                    currentMap = currentRamp[(int)RampStorage.ConnectedMap];
                    UpdateRampLocation(_length);
                    currentHeight += _height;

                    //Check data
                    if (posX == newMapInfo[(int)RampStorage.XCoord] && posZ == newMapInfo[(int)RampStorage.ZCoord] && currentHeight < maps[currentMap].Height + 10 && currentHeight > maps[currentMap].Height - 10)
                    {
                        Log("Old ramp data is good", true);
                    }
                    else
                    {
                        Log($"Something is wrong with length = {_length}, or height = {_height}", true);
                        errors += 4;
                    }

                    posX = newMapInfo[(int)RampStorage.XCoord];
                    posZ = newMapInfo[(int)RampStorage.ZCoord];
                    currentHeight = maps[currentMap].Height;

                    Log($"NEW: x:{posX}, z:{posZ}, dir:{direction}, map:{currentMap}", true);
                    Log($"new height {currentHeight}, map length = {_length}", true);
                }
                locationUpdated = true;
            }
            catch (Exception e)
            {
                LogException(e);
                throw new Exception("Failed ramp driving", e);
            }
        }

        static void UpdateRampLocation(int _rampLength)
        {
            switch (direction) //Update position with the help of the ramp length
            {
                case 0:
                    posZ -= (int)MathF.Round(_rampLength / 30f);
                    break;
                case 1:
                    posX -= (int)MathF.Round(_rampLength / 30f);
                    break;
                case 2:
                    posZ += (int)MathF.Round(_rampLength / 30f);
                    break;
                case 3:
                    posX += (int)MathF.Round(_rampLength / 30f);
                    break;
                default:
                    throw new Exception("ERROR DIRECTION RAMP WHAT");
            }
        }

        static void RampSizeFix(ref int _length, ref int _heigth)
        {
            if (_heigth == 0) return;

            if (_heigth > 0)
            {
                _length = (int)(_length * 0.9);
            }
            else
            {
                _length = (int)(_length * 1.1);
                _heigth = (int)(_heigth * 1.1);
            }
        }

        /// <summary>
        /// Finds a map depending on height
        /// </summary>
        /// <param name="_height">How high up the ramp is</param>
        /// <returns>The map index</returns>
        static int FindMapAtHeight(int _height)
        {
            for (int i = 0; i < maps.Count; i++)
            {
                if (_height > maps[i].Height - 7 && _height < maps[i].Height + 7)
                {
                    return i;
                }
            }
            return -1;
        }

        // ********************************** Timing & exiting ********************************** 

        static void CheckTimer()
        {
#warning fix
            if (!startTimeUpdated)
            {
                secondsToStart = PathSeconds(PathToStart());
                startTimeUpdated = true;
            }
            //if !starttimechecked => checktime
            // if driveWay => check time on it
            if (timer.ElapsedMilliseconds +  secondsToStart * 1000 > 7.5 * 60 * 1000)//7,75 min passed
            {
                Console.WriteLine("7 mins passed, returning");
                driveWay = new List<byte[]>(PathToStart());
                timeOut = true;
            }
        }

        static void DelayThread(int _millis, bool _doWork)
        {
            Stopwatch sw = Stopwatch.StartNew();

            if (((_millis > 50) || (_millis > 10 && currentMap == 0)) && _doWork && !startTimeUpdated)
            {
                secondsToStart = PathSeconds(PathToStart());
                startTimeUpdated = true;
            }

            if (_millis - (int)sw.ElapsedMilliseconds > 1)
            {
                try
                {
                    sw.Stop();
                    Thread.Sleep(_millis - (int)sw.ElapsedMilliseconds);
                    Log($"distance search took {sw.ElapsedMilliseconds}", false);
                }
                catch (Exception e)
                {
                    LogException(e);
                    Log("Could not sleep");
                }
            }
        }

        static int PathSeconds(List<byte[]> path)
        {
            double _driveTime = 0,
                   _turnTime = 0;

            _turnTime += SecondsPerTurn * 2; //Assume that we are facing away from the first search for extra margin

            for (int i = 0; i < path.Count; i++)
            {
                _driveTime += SecondsPerDrive;

                if (i > 0 && i < path.Count - 1) //Last tile has no turning from, since it is the final tile. First tile already added.
                {
                    if (TileToDirection(path[i], path[i + 1]) != TileToDirection(path[i - 1], path[i]))
                    {
                        _turnTime += SecondsPerTurn;
                    }
                }
            }

            return (int)Math.Round((_driveTime + _turnTime + 10) * 1,2); //Extra margin (for example dropping missed kits) and exit time
        }

        static void Exit()
        {
            reset = true;
            exit = true;
            Log($"Lowest: {lowestX},{lowestZ}; Highest: {highestX},{highestZ}", true);
            for (int m = 0; m < maps.Count; m++)
            {
                string[] _mapToText = new string[(3 + highestZ - lowestZ)];
                int _loops = 0;
                for (int i = lowestZ - 1; i <= highestZ + 1; i++)
                {
                    for (int j = lowestX - 1; j <= highestX + 1; j++)
                    {
                        string _bits = $"{j};{i}:";
                        for (int k = 15; k >= 0; k--)
                        {
                            _bits += maps[m].ReadBit(j, i, (BitLocation)k) ? "1" : "0";
                        }

                        _mapToText[_loops] += _bits + ",";
                    }
                    _loops++;
                }
                try
                {
                    File.AppendAllText("log.txt",$"map {m+1}/{maps.Count}\n" + string.Join('\n', _mapToText) + "\n\n"); // Writes map to log file
                }
                catch (Exception e)
                {
                    Log($"Could not create log because: ", true);
                    LogException(e);
                }
            }
            Log("done with map file", true);
            Thread.Sleep(1000);
            listener.Stop();
            serialPort1.Close();
        }


        // ********************************** Logging ********************************** 

        public static void Log(string _message)
        {
#if DEBUG
            Console.WriteLine(_message);
#endif
        }

        public static void Log(string _message, bool _consoleLog)
        {
#if DEBUG
            File.AppendAllText("log.txt", $"\n{timer.ElapsedMilliseconds}: {_message}");
            if (_consoleLog) Console.WriteLine(_message);
#endif
        }

        /// <summary>
        /// Logs exceptions and adds an error
        /// </summary>
        public static void LogException(Exception e)
        {
            errors += 2;
            Log($"Exception: {e.Message} at {e.Source}:{e}", false);
            Log($"Exception -- {e.Message}");
        }
    }
}
