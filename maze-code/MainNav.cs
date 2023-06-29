// |||| NAVIGATION - Code for the main navigation and stuff that did not belong anywhere else ||||
using EnumCommand;
using Mapping;
using System.Diagnostics;
using System.IO.Ports;

namespace SerialConsole
{
    internal partial class Program
    {
        #region Variables and objects
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
        /// <summary>
        /// The way back, FIRST BYTE[]: 0=map,1=area
        /// </summary>
        static List<List<byte[]>> mapWayBack = new();
        static List<List<byte[]>> saveWayBack = new();

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
        static volatile bool exit = false;

        static bool goingBack = false;

#warning remove config file before competition
        static double MINUTES = 8;

        /// <summary>
        /// Keep track of the amount of errors, add an amount for each error depending on severity, 
        /// if it is too high we use backup nav
        /// </summary>
        static int errors = 0;
        static string logFileName = "log.txt";

        #endregion Variables and objects

        #region Main/Startup
        // ********************************** Main Loop & Startup ********************************** 

        static void Main()
        {
            Console.CancelKeyPress += delegate (object? o, ConsoleCancelEventArgs e)
            {
                e.Cancel = false;
                Exit();
                throw new Exception("EXIT");
            };

            Delay(100);

            StartUp();

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

                    CheckTimer();
                    Turnlogic();
                    Drive(true, turboDrive);
                    Delay(10, true);

                    ErrorChecker();
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
            Config();

            listener.Start();
            Log("Waiting for connection...", true);
            client = listener.AcceptTcpClient();
            Log("Client accepted", true);

            while (!serialPort1.IsOpen)
            {
                serialPort1.PortName = "/dev/ttyS0";
                try
                {
                    serialPort1.Open();
                }
                catch
                {
                    Log("Cannot open port, try these: ", true);
                    string[] _ports = SerialPort.GetPortNames();
                    foreach (string _port in _ports)
                    {
                        Log(_port, true);
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
                            Log("Cannot open port " + _port, true);
                        }
                    }

                    Delay(200);
                }
            }
            Log($"Connected to {serialPort1.PortName}", true);

            Delay(100);
            if (serialPort1.BytesToRead != 0)
                serialPort1.ReadExisting();
            Delay(100);
            Log(" /      Waiting for reset    \\", true);

            bool _recivedReset = false;
            string _commandRecived;
            do //Wait for calibration start
            {
                while (serialPort1.BytesToRead == 0)
                {
                    Delay(20);
                }
                _commandRecived = serialPort1.ReadLine();

                if (_commandRecived.Contains(RecivedCommands.LOP.GetCommand()) && !_commandRecived.Contains(RecivedCommands.Calibration.GetCommand()))
                {
                    _recivedReset = true;
                    break;
                }

            } while (!(_commandRecived.Contains(RecivedCommands.LOP.GetCommand()) && _commandRecived.Contains(RecivedCommands.Calibration.GetCommand())));

            timer.Start();

            if (!_recivedReset)
            {
                _commandRecived = "";
                Log(": Recived colour sensor reset :", true);

                do //Wait for program start
                {
                    while (serialPort1.BytesToRead == 0)
                    {
                        Delay(20);
                    }
                    _commandRecived = serialPort1.ReadLine();
                } while (!_commandRecived.Contains(RecivedCommands.LOP.GetCommand()) || _commandRecived.Contains(RecivedCommands.Calibration.GetCommand()));
            }

            Log(" \\     Recived full reset    /", true);

            Thread serverThread = new(ServerLoop);
            serverThread.Start();
            Delay(200);

            // Setup map and start info

            posX = posZ = 25;
            currentMap = 0;
            direction = 0;
            maps.Capacity = 15;
            maps.Clear();
            maps.Add(new Map(50, 0, posX, posZ));
            maps[0].Clear();

            Log($"Area count: {maps[0].Areas.Count}", false);
            maps[0].Areas.Clear();
            AddArea(posX, posZ);
            UpdateMapFull(true);


            AddTile();
            saveWayBack = new List<List<byte[]>>(mapWayBack);
            
            Delay(100);
            Log("Done with startup", true);
        }

        static void Config()
        {
            logFileName = $"log{DateTime.Now:MMddTHHmm}.txt";
            if (Directory.Exists(@"../logs/"))
            {
                logFileName = @"../logs/" + logFileName;
            }
            File.WriteAllText(logFileName, DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss") + "\n-----------------------------------\n:::::::::: Program start ::::::::::\n-----------------------------------");

            string _config = File.ReadAllText("config.txt");
            string _find = "MINUTES:";
            MINUTES = double.Parse(_config.Substring(_config.IndexOf(_find) + _find.Length, 1));
            Log($"MINUTES = {MINUTES}", true);
        }
        #endregion

        #region Navigation
        // ********************************** Navigation ********************************** 

        static void Turnlogic()
        {
            if (reset) return;
            turboDrive = false;

            currentArea = AreaCheck((byte)posX, (byte)posZ, currentArea, currentMap);
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

            Delay(20, true);

            //CHANGE CODE BELOW WHEN RAMP STUFF IS CHANGED
            if (((driveWay.Count == 0 && maps[currentMap].CrossTiles.Count == 0 && _unExpTiles == 0) || goingBack) 
                  && posX == maps[currentMap].StartPosX && posZ == maps[currentMap].StartPosZ && currentMap == 0)
            {
                timer.Stop();
                Log("DONE", true);
                Delay(20_000);
                Exit();
                return;
                
            }

            if (driveWay.Count > 0)
            {
                if (_unExpTiles != 0)
                {
                    for (int i = direction + 1; i > direction - 3; i--)
                    {
                        if (_surroundingTiles[FixDirection(i)])
                        {
                            maps[currentMap].CrossTiles.Add(DirToTile(i, (byte)posX, (byte)posZ)); //Add adjecent tiles to the list
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
                    turboDrive = !ReadNextTo(BitLocation.ramp, Directions.front); //If there is not a ramp in front, we turbo drive
                    driveWay.RemoveAt(0);
                }
            }
            else if (_unExpTiles > 0)
            {
                if (_unExpTiles != 1)
                {
                    for (int i = 0; i <= 3; i++)
                    {
                        if (_surroundingTiles[FixDirection(i)])
                        {
                            while (maps[currentMap].CrossTiles.Contains(DirToTile(i, (byte)posX, (byte)posZ))) //Remove all other instances of this tile
                            {
                                maps[currentMap].CrossTiles.Remove(DirToTile(i, (byte)posX, (byte)posZ));
                            }
                            maps[currentMap].CrossTiles.Add(DirToTile(i, (byte)posX, (byte)posZ)); //Add adjacent tiles to the list
                        }
                    }
                }

                for (int i = direction + 1; i >= direction - 2; i--) //Go to best tile; first left, then front, right, back tile
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
                            driveWay = mapWayBack[^1];
                            driveWay.RemoveAt(0);

                            if (driveWay.Count > 0)
                                goto NavLogic;
                            throw new Exception($"DID NOT FIND PATH TO (map){currentMap} START XZ");
                        }

                        _crossX = maps[currentMap].CrossTiles.Last()[0];
                        _crossZ = maps[currentMap].CrossTiles.Last()[1];
                    }

                    Log($"Finding path to {_crossX},{_crossZ}", true);
                    FindPathHere(_crossX, _crossZ);

                    if (driveWay.Count == 0)
                    {
                        for (int i = maps[currentMap].CrossTiles.Count-1; i <= 0; i--)
                        {
                            //(maps[currentMap].CrossTiles[^i], maps[currentMap].CrossTiles[^1]) = (maps[currentMap].CrossTiles[^1], maps[currentMap].CrossTiles[^i]); //BAD SOLUTION, IF ONE IS BAD, BOTH ARE LIKELY BAD; travel up ramp instead?
                            if (maps[currentMap].IsSameArea(maps[currentMap].CrossTiles[i], new byte[] { (byte)posX, (byte)posZ }))
                                FindPathHere(maps[currentMap].CrossTiles[i][1], maps[currentMap].CrossTiles[i][1]);
                            if (driveWay.Count > 0) goto NavLogic;
                        }
                        Log("SOMETHING PROBABLY WRONG WITH RAMP OR MAP, MAYBE DUAL RAMP, trying to solve", true);

                        driveWay = mapWayBack[^1];
                        driveWay.RemoveAt(0);
                        if (driveWay.Count > 0)
                            goto NavLogic;
                        throw new Exception($"DID NOT FIND PATH TO (map){currentMap} START XZ");
                    }
                    goto NavLogic;
                }
                else
                {
                    Log("Going back a map", true);

                    driveWay = mapWayBack[^1];
                    driveWay.RemoveAt(0);
                    if (driveWay.Count > 0)
                        goto NavLogic;
                    throw new Exception($"DID NOT FIND PATH TO (map){currentMap} START XZ");
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
            Delay(1_000);

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
                        CheckAndDropKits(true, true);
                        Turn('l');
                        CheckAndDropKits(true, true);
                        Delay(100);
                        Drive(false, false);
                    }
                    else if (frontPresent || ReadNextTo(BitLocation.blackTile, Directions.front) /*|| RampCheck(direction)*/)
                    {
                        CheckAndDropKits(true, true);
                        Turn('r');
                        Delay(50);
                    }
                    else
                    {
                        CheckAndDropKits(true, true);
                        break;
                    }
                    Delay(10);
                }

                Drive(false, false); //Do not check for ramps, we want to have 'dumber' code so less can go wrong
                CheckAndDropKits(true, true);
            }
        }

        #endregion nav

        #region Ramps
        // ********************************** Ramps ********************************** 
        static void RampDriven(int _rampDirection, int _length, int _height)
        {
            try
            {
                UpdateWayBack(); //Update old way back, to make sure it is up to date
                //RampSizeFix(ref _length, ref _height);
                bool _strange;

                if (!maps[currentMap].FindRamp((byte)posX, (byte)posZ, (byte)direction)) //New ramp
                {
                    Log("-_-_-_-_-_-_-_-_-_-_ Travelled ramp, was a new ramp _-_-_-_-_-_-_-_-_-_-", true);
                    //Update and save this map
                    WriteNextTo(BitLocation.ramp, true, Directions.front);
                    WriteNextTo(BitLocation.explored, true, Directions.front);

                    byte[] _rampTile = DirToTile(direction, (byte)posX, (byte)posZ);
                    AddSinceCheckpoint(_rampTile[0], _rampTile[1], (byte)currentMap);

                    byte _fromMap = (byte)currentMap;
                    maps[currentMap].AddRamp((byte)posX, (byte)posZ, (byte)_rampDirection, (byte)rampCount, (byte)(maps.Count - 1), (byte)_length);
                    Log($"Saved ramp: x:{posX}, z:{posZ}, dir:{_rampDirection}, rampCount:{rampCount}, map:{maps.Count - 1}", false);

                    currentHeight += _height;
                    _strange = UpdateRampLocation(_length, currentMap, direction);
                    Log($"new height {currentHeight}, map length = {_length}", true);

                    //Check if this seems to be a new map or an old map
                    int _mapIndex = FindMapAtHeight(currentHeight);

                    if (_mapIndex != -1) //Old map but new ramp
                    {
                        Log("____---- OLD MAP; NEW RAMP ----____", true);
                        currentMap = _mapIndex;
                        maps[currentMap].UpdateHeight(currentHeight);
                        currentHeight = maps[currentMap].Height;

                        maps[_fromMap].UpdateCrossTiles();

                        _rampTile = DirToTile(direction - 2, (byte)posX, (byte)posZ);
                    }
                    else //New map
                    {
                        Log("____---- NEW MAP; NEW RAMP ----____", true);

                        //Setup new map
                        maps.Add(new Map(50, currentHeight, posX, posZ));
                        currentMap = maps.Count - 1;
                        maps[currentMap].Clear();

                        _rampTile = DirToTile(direction - 2, (byte)posX, (byte)posZ);
                    }
                    maps[currentMap].AddRamp((byte)posX, (byte)posZ, (byte)FixDirection(_rampDirection - 2), (byte)rampCount, _fromMap, (byte)_length);
                    rampCount++;

                    byte[] _newRampTile = DirToTile(direction - 2, (byte)posX, (byte)posZ);
                    AddSinceCheckpoint(_newRampTile[0], _newRampTile[1], (byte)currentMap);

                    AddArea(_newRampTile[0], _newRampTile[1]);
                    AddTile();
                    if (_mapIndex != -1) TileAreaCheck((byte)posX, (byte)posZ, (byte)currentArea, (byte)currentMap);


                    WriteNextTo(BitLocation.ramp, true, Directions.back);
                    if (MarkMapAsVisited(_fromMap, currentMap, currentArea))
                    {
                        WriteNextTo(BitLocation.explored, true, Directions.back);
                    }

                    Log($"NEW: x:{posX}, z:{posZ}, dir:{_rampDirection}, rampCount:{rampCount}, map:{maps.Count - 1}", false);
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
                    int _oldMap = currentMap;
                    currentMap = currentRamp[(int)RampStorage.ConnectedMap];
                    _strange = UpdateRampLocation(_length, _oldMap, direction);
                    currentHeight += _height;

                    //Check data
                    if (posX == newMapInfo[(int)RampStorage.XCoord] && posZ == newMapInfo[(int)RampStorage.ZCoord] && currentHeight < maps[currentMap].Height + 10 && currentHeight > maps[currentMap].Height - 10)
                    {
                        Log("Old ramp data is good", true);
                        errors-=2; //This is a sign that we know where we are
                    }
                    else
                    {
                        Log($"Something is wrong with length = {_length}, or height = {_height}", true);
                        errors += 4; //This is a sign that we do not know where we are
                    }

                    posX = newMapInfo[(int)RampStorage.XCoord];
                    posZ = newMapInfo[(int)RampStorage.ZCoord];
                    currentHeight = maps[currentMap].Height;
                    currentArea = maps[currentMap].GetArea(new byte[] {(byte)posX, (byte)posZ});

                    if (AreaInWayBack(currentArea))
                    {
                        RemoveWayBack(currentArea); //We are back to this area, same shortest path back
                    }
                    else
                    {
                        mapWayBack.Add(new List<byte[]>() { new byte[] { (byte)currentMap, (byte)currentArea },
                                                            new byte[] { (byte)posX, (byte)posZ } });
                    }
                    Log($"NEW: x:{posX}, z:{posZ}, dir:{direction}, map:{currentMap}", true);
                    Log($"new height {currentHeight}, map length = {_length}", true);
                }
                if (_strange) maps[currentMap].HasStrangeRamps[direction] = true;
                locationUpdated = true;
            }
            catch (Exception e)
            {
                LogException(e);
                throw new Exception("Failed ramp driving", e);
            }
        }

        static bool UpdateRampLocation(int _rampLength, int _fromMap, int _direction)
        {
            bool _isStrange = false;

            if (maps[_fromMap].HasStrangeRamps[_direction])
            {
                if (_rampLength % 30 < 24 && _rampLength % 30 > 6)
                {
                    _rampLength -= _rampLength % 30; //Round down, since we rounded up last time if it is close to imperfect tile amount
                }
            }
            else if (maps[_fromMap].HasStrangeRamps[FixDirection(_direction - 2)])
            {
                if (_rampLength % 30 < 25 && _rampLength % 30 > 5)
                {
                    _rampLength += 30 - (_rampLength % 30); //Round up, since we rounded up last time if it is close to imperfect tile amount
                }
            }
            else if (_rampLength % 30 < 20 && _rampLength % 30 > 10)
            {
                Log($"_-!-_!_-!-_ UPDATED RAMP LENGTH = {_rampLength} WITH + 15cm _-!-_!_-!-_", true);
                _isStrange = true;
                _rampLength += 30 - (_rampLength % 30); //Add so that we "have travelled another tile", to make sure there i no error
            }
            
            float _totalLength = _rampLength + 30f; //Take into account that we drove a step as well
            switch (direction) //Update position with the help of the ramp length
            {
                case 0:
                    posZ -= (int)MathF.Round(_totalLength / 30f);
                    break;
                case 1:
                    posX -= (int)MathF.Round(_totalLength / 30f);
                    break;
                case 2:
                    posZ += (int)MathF.Round(_totalLength / 30f);
                    break;
                case 3:
                    posX += (int)MathF.Round(_totalLength / 30f);
                    break;
                default:
                    throw new Exception("ERROR DIRECTION RAMP WHAT");
            }

            return _isStrange;
        }

        //static void RampSizeFix(ref int _length, ref int _heigth)
        //{
        //    if (_heigth == 0) return;

        //    if (_heigth > 0)
        //    {
        //        _length = (int)(_length * 0.9);
        //    }
        //    else
        //    {
        //        _length = (int)(_length * 1.1);
        //        _heigth = (int)(_heigth * 1.1);
        //    }
        //}

        /// <summary>
        /// Finds a map depending on height
        /// </summary>
        /// <param name="_height">How high up the ramp is</param>
        /// <returns>The map index of the closest map, if it is in range</returns>
        static int FindMapAtHeight(int _height)
        {
            int _closestHeight = 0;
            int _closestMap = -1;

            for (int i = 0; i < maps.Count; i++)
            {
                if (_height > maps[i].Height - 7 && _height < maps[i].Height + 7)
                {
                    if ((Math.Abs(_height - _closestHeight) > Math.Abs(_height - maps[i].Height)) || _closestMap == -1)
                    {
                        _closestMap = i;
                        _closestHeight = maps[i].Height;
                    }
                }
            }
            return _closestMap;
        }

        #endregion ramps

        #region Timing
        // ********************************** Timing & exiting ********************************** 
        /// <summary>
        /// Checks the timer and returns to start if time is close to out
        /// </summary>
        static void CheckTimer()
        {
            UpdateWayBack();
            secondsToStart = StartPathSeconds();

            if (timer.ElapsedMilliseconds/1000 +  secondsToStart > (MINUTES - 0.5) * 60 && !goingBack)
            {
                Console.WriteLine("Time passed, returning");
                driveWay = PathToStart();
                goingBack = true;
            }
        }

        static void Delay(int _millis, bool _doWork)
        {
            if (_doWork)
            {
                Stopwatch sw = Stopwatch.StartNew();

                foreach (Map map in maps)
                {
                    map.UpdateCrossTiles();
                }

                sw.Stop();

                Log($"Delay first part took {sw.ElapsedMilliseconds} ms", false);
                Delay(_millis - (int)sw.ElapsedMilliseconds);
            }
            else
            {
                Delay(_millis);
            }
        }

        static void Delay(int _millis)
        {
            if (_millis > 0) //We must be completely sure, as -1 sleeps forever
                Thread.Sleep(_millis);
        }

        static int StartPathSeconds()
        {
            int _secounds = 0;
            for (int _area = mapWayBack.Count - 1; _area >= 0; _area--)
            {
                Log($"StartPathSeconds: In mapwayback[{_area}]: ", false);
                mapWayBack[_area].ForEach(_tile => Log($"*_*_*_* {_tile[0]},{_tile[1]} *_*_*_*", false));
                double _driveTime = 0,
                       _turnTime = 0;

                for (int i = 1; i < mapWayBack[_area].Count; i++) //Forget about first tile since it is info
                {
                    _driveTime += SecondsPerDrive;

                    if (i > 1 && i < mapWayBack[_area].Count - 1) //Last tile has no turning from, since it is the final tile.
                    {
                        if (TileToDirection(mapWayBack[_area][i], mapWayBack[_area][i + 1]) != 
                            TileToDirection(mapWayBack[_area][i - 1], mapWayBack[_area][i]))
                        {
                            _turnTime += SecondsPerTurn;
                        }
                    }
                }
                _secounds += (int)Math.Round((_driveTime + _turnTime + 10) * 1,2); //Extra margin (for example dropping missed kits) and exit time

                if (_area != 0)
                {

                    byte[] _ramp = RampByRamptile(mapWayBack[_area][^1][0], mapWayBack[_area][^1][1], mapWayBack[_area][0][0]);
                    _secounds += (int)(Math.Round(_ramp[(int)RampStorage.RampLength] / 30f + 1) * SecondsPerDrive); //Add ramp drive time
                }
            }

            return _secounds;
        }

        #endregion timing

        #region Miscellaneous

        static void ErrorChecker()
        {
            if (errors >= 10)
            {
                throw new Exception("Too many small errors");
            }
            errors--; //Decrease error amount, so that backup nav is only used if there are multiple errors quickly
            if (errors < 0) errors = 0;
            Log($"** * ** Errors: {errors} ** * **", true);
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
                    File.AppendAllText(logFileName,$"\nmap {m+1}/{maps.Count}\n" + string.Join('\n', _mapToText) + "\n\n"); // Writes map to log file
                }
                catch (Exception e)
                {
                    Log($"Could not create log because: ", true);
                    LogException(e);
                }
            }
            Log("done with map file", true);
            Delay(1000);
            listener.Stop();
            serialPort1.Close();
        }


        // ********************************** Logging ********************************** 

        /// <summary>
        /// Writes a log message to the log file
        /// </summary>
        /// <param name="_message">The message that should be logged</param>
        /// <param name="_consoleLog">Display message in console</param>
        public static void Log(string _message, bool _consoleLog)
        {
#if DEBUG
            File.AppendAllText(logFileName, $"\n{timer.ElapsedMilliseconds}: {_message}");
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
            Console.WriteLine($"Exception -- {e.Message}");
        }
        #endregion misc
    }
}
