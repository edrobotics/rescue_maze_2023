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

        #region Navigation & localization
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

        enum Directions
        {
            front,
            left,
            back,
            right
        }
        #endregion

        #region paths
        static List<byte[]> driveWay = new();
        /// <summary>
        /// The way back, FIRST BYTE[]: 0=map,1=area; SECOND BYTE[] = Current Pos
        /// </summary>
        static List<List<byte[]>> mapWayBack = new();
        static List<List<byte[]>> saveWayBack = new();
        #endregion

        #region Timing and exiting + log

        static readonly Stopwatch timer = new();

        const double SecondsPerDrive = 3;
        const double SecondsPerTurn = 2.5;
        static int secondsToStart = 0;
        static volatile bool exit = false;

        static bool goingBack = false;
        static bool willGoBack = true;

#warning check config file before competition
        static double MINUTES = 8;
        static int MAXERRORS = 10;

        /// <summary>
        /// Keep track of the amount of errors, add an amount for each error depending on severity, 
        /// if it is too high we use backup nav
        /// </summary>
        static int errors = 0;
        static string logFileName = "log.txt";

        #endregion Time
        #endregion Variables and objects

        #region Main/Startup

        #region Main

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
                //int _loops = 0;
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
                    //_loops++;
                    //if (_loops > 2 && _loops % 11 == 0 && !exit && !reset) LogMap();
                }
            }
            catch (Exception e)
            {
                while (!exit)
                {
                    try
                    {
                        LogException(e);
                        BackupNav(); //If we return it is likely due to a reset, which means that we can try to go back to normal navigation
                        goto LoopStart;
                    }
                    catch (Exception ex)
                    {
                        LogException(ex);
                    }
                }
            }
            finally
            {
                Delay(10);
            }
        }
        #endregion main

        #region Startup
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
                Log("Recived: " + _commandRecived, false);
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
                    Log("Recived: " + _commandRecived, false);
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
            for (int i = 0; i < 1000; i++)
            {
                logFileName = $"log{i:000}.log";
                if (Directory.Exists(@"../logs/"))
                {
                    logFileName = @"../logs/" + logFileName;
                }
                if (!File.Exists(logFileName))
                {
                    break;
                }
            }
            File.WriteAllText(logFileName, DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss") + "\n-----------------------------------\n:::::::::: Program start ::::::::::\n-----------------------------------");

            try
            {
                string _config = File.ReadAllText("config.cfg");

                string _findMins = "MINUTES:";
                MINUTES = double.Parse(_config[(_config.IndexOf(_findMins) + _findMins.Length).._config.IndexOf(';', _config.IndexOf(_findMins))].Trim());
                Log($"MINUTES = {MINUTES}", true);

                string _findErrors = "MAXERRORS:";
                MAXERRORS = int.Parse(_config[(_config.IndexOf(_findErrors) + _findErrors.Length).._config.IndexOf(';', _config.IndexOf(_findErrors))].Trim());
                Log($"MAXERRORS = {MAXERRORS}", true);
            }
            catch
            {
                Log("++-- Config failed --++", true);
                MINUTES = 8;
                MAXERRORS = 12;
            }
        }
        #endregion startup
        #endregion main nav

        #region Navigation

        #region Turn and path deciding

        static void Turnlogic()
        {
        LogicStart:
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

            Delay(10, true);

            if (posX == maps[0].StartPosX && posZ == maps[0].StartPosZ && currentMap == 0)
            {
                Log($"ON START TILE WITH DriveWay:{driveWay.Count},CrossTiles:{maps[0].CrossTiles.Count},UnExpTiles:{_unExpTiles}", true);

                if ((driveWay.Count == 0 && maps[0].CrossTiles.Count == 0 && _unExpTiles == 0) || goingBack)
                {
                    Done();
                }
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
                    if (driveWay.Count == 0) goto NavLogic;
                }
                Log($"Is turning to {driveWay[0][0]},{driveWay[0][1]}", true);
                TurnTo(TileToDirection(driveWay[0]));
                
                if (ReadHere((BitLocation)direction))
                {
                    BlinkLamp(BlinkOptions.MappingError);
                    driveWay.Clear();
                    if (goingBack) driveWay = PathToStart();
                    goto LogicStart;
                }

                if (!reset)
                {
                    turboDrive = !ReadNextTo(BitLocation.ramp, Directions.front) && ReadNextTo(BitLocation.explored, Directions.front); //If there is explored, not a ramp in front, we turbo drive
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
                if (maps[currentMap].CrossTiles.Count > 0)
                {
                    byte[] _currentCoords = new byte[] { (byte)posX, (byte)posZ };
                    while (maps[currentMap].CrossTiles.Contains(_currentCoords))
                    {
                        maps[currentMap].CrossTiles.Remove(_currentCoords);
                        if (maps[currentMap].CrossTiles.Count == 0)
                            goto NavLogic;
                    }

                    int _crossX = maps[currentMap].CrossTiles.Last()[0];
                    int _crossZ = maps[currentMap].CrossTiles.Last()[1];

                    while (!ShouldGoTo(_crossX, _crossZ))
                    {
                        maps[currentMap].CrossTiles.RemoveAt(maps[currentMap].CrossTiles.Count - 1);

                        if (maps[currentMap].CrossTiles.Count == 0)
                        {
                            goto NavLogic;
                        }

                        _crossX = maps[currentMap].CrossTiles.Last()[0];
                        _crossZ = maps[currentMap].CrossTiles.Last()[1];
                    }

                    Log($"Finding path to {_crossX},{_crossZ}", true);
                    FindPathHere(_crossX, _crossZ);

                    if (driveWay.Count == 0)
                    {
                        for (int i = maps[currentMap].CrossTiles.Count - 1; i <= 0; i--)
                        {
                            if (maps[currentMap].IsSameArea(maps[currentMap].CrossTiles[i], new byte[] { (byte)posX, (byte)posZ }))
                                FindPathHere(maps[currentMap].CrossTiles[i][1], maps[currentMap].CrossTiles[i][1]);
                            if (driveWay.Count > 0) goto NavLogic;
                        }
                        Log("SOMETHING PROBABLY WRONG WITH RAMP OR MAP, MAYBE DUAL RAMP, trying to solve", true);

                        errors+=2;
                        driveWay = new List<byte[]>(mapWayBack[^1]);
                        driveWay.RemoveAt(0);
                        driveWay.RemoveAt(1);
                        if (driveWay.Count > 0 && !(posX == maps[0].StartPosZ && posZ == maps[0].StartPosZ && currentMap == 0))
                        {
                            if (currentMap == 0) BlinkLamp(BlinkOptions.Returning);
                            goto NavLogic;
                        }
                        throw new Exception($"DID NOT FIND PATH TO (map){currentMap} START XZ");
                    }
                    goto NavLogic;
                }
                else
                {
                    if (currentMap == 0)
                    {
                        goingBack = true;
                        BlinkLamp(BlinkOptions.Returning);
                        Log("Going to start due to tile shortage", true);
                    }
                    else
                    {
                        Log("Going back a map", true);
                    }

                    driveWay = new List<byte[]>(mapWayBack[^1]);
                    driveWay.RemoveAt(0);
                    driveWay.RemoveAt(1);
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
        #endregion

        #region Old
        static void BackupNav()
        {
            for (int i = 0; i < 10; i++) Log("!!!!!!!!!!!!!!!PROBLEM PROBLEM NAVIGATION FAILED!!!!!!!!!!!!!!!", true);
            BlinkLamp(BlinkOptions.NavigationFailure);
            Delay(1_000);

            while (!reset && !exit)
            {
                while (true /*!leftPresent || frontPresent || ReadNextTo(posX, posZ, blackTile, Directions.front) || RampCheck(direction)*/)
                {
                    SensorCheck();
                    if (reset || exit)
                        return;
                    if (!leftPresent && !ReadNextTo(BitLocation.blackTile, Directions.left) /*&& !RampCheck(direction + 1)*/)
                    {
                        CheckAndDropKits(true, true, true);
                        Turn('l');
                        CheckAndDropKits(true, true, true);
                        Delay(100);
                        Drive(false, false);
                    }
                    else if (frontPresent || ReadNextTo(BitLocation.blackTile, Directions.front) /*|| RampCheck(direction)*/)
                    {
                        CheckAndDropKits(true, true, true);
                        Turn('r');
                        Delay(50);
                    }
                    else
                    {
                        CheckAndDropKits(true, true, true);
                        break;
                    }
                    Delay(10);
                }

                Drive(false, false); //Do not check for ramps, we want to have 'dumber' code so less can go wrong
                CheckAndDropKits(true, true, true);
            }
        }
        #endregion

        #endregion nav

        #region Ramps
        // ********************************** Ramps ********************************** 
        static void RampDriven(int _rampDirection, int _length, int _height)
        {
            if (Math.Abs(_height - currentHeight) < 4)
            {
                currentHeight += _height;
                return;
            }

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

                    byte[] _rampTile = DirToTile(direction, (byte)posX, (byte)posZ);
                    AddTile(_rampTile[0], _rampTile[1], currentMap, currentArea);

                    byte _fromMap = (byte)currentMap;
                    byte _fromX = (byte)posX,
                         _fromZ = (byte)posZ;

                    currentHeight += _height;
                    _strange = UpdateRampLocation(_length, currentMap, direction);
                    Log($"new height {currentHeight}, map length = {_length}", true);

                    //Check if this seems to be a new map or an old map
                    int _mapIndex = FindMapAtHeight(currentHeight);

                    if (_mapIndex != -1) //Old map but new ramp
                    {
                        maps[_fromMap].AddRamp(_fromX, _fromZ, (byte)_rampDirection, (byte)rampCount, (byte)(_mapIndex), (byte)_length);
                        Log($"Saved ramp: x:{_fromX}, z:{_fromZ}, dir:{_rampDirection}, rampCount:{rampCount}, map:{_mapIndex}", false);

                        Log("____---- OLD MAP; NEW RAMP ----____", true);
                        currentMap = _mapIndex;
                        currentHeight = maps[currentMap].UpdateHeight(currentHeight);

                        maps[_fromMap].UpdateCrossTiles();

                        _rampTile = DirToTile(direction - 2, (byte)posX, (byte)posZ);
                    }
                    else //New map
                    {
                        Log("____---- NEW MAP; NEW RAMP ----____", true);

                        //Setup new map
                        maps.Add(new Map(50, currentHeight, posX, posZ));
                        currentMap = maps.Count - 1;

                        _rampTile = DirToTile(direction - 2, (byte)posX, (byte)posZ);

                        maps[_fromMap].AddRamp(_fromX, _fromZ, (byte)_rampDirection, (byte)rampCount, (byte)(maps.Count - 1), (byte)_length);
                        Log($"Saved ramp: x:{_fromX}, z:{_fromZ}, dir:{_rampDirection}, rampCount:{rampCount}, map:{maps.Count - 1}", false);
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

                    Log($"NEW: x:{posX}, z:{posZ}, dir:{direction}, rampCount:{rampCount}, map:{currentMap}", false);
                }
                else //Used ramp
                {
                    Log("-_-_-_-_-_-_-_-_-_-_ Ramp was a previously used ramp _-_-_-_-_-_-_-_-_-_-", true);

                    //Get data
                    byte[] currentRamp = maps[currentMap].GetRampAt((byte)posX, (byte)posZ, (byte)direction);
                    byte[] newMapInfo = maps[currentRamp[(int)RampStorage.ConnectedMap]].GetRampAt(currentRamp[(int)RampStorage.RampIndex]);
                    foreach (byte _info in currentRamp)
                    {
                        Log("::..::" + _info + "::..::", false);
                    }
                    foreach (byte _info in newMapInfo)
                    {
                        Log("..::.." + _info + "..::..", false);
                    }

                    //Update data
                    int _oldMap = currentMap;
                    currentMap = currentRamp[(int)RampStorage.ConnectedMap];
                    _strange = UpdateRampLocation(_length, _oldMap, direction);
                    currentHeight += _height;

                    //Check data
                    if (posX == newMapInfo[(int)RampStorage.XCoord] && posZ == newMapInfo[(int)RampStorage.ZCoord] && currentHeight < maps[currentMap].Height + 10 && currentHeight > maps[currentMap].Height - 10)
                    {
                        Log("Old ramp data is good", true);
                        errors -= 2; //This is a sign that we know where we are
                    }
                    else
                    {
                        Log($"Something is wrong with length = {_length}, or height = {_height}", true);
                        errors += 4; //This is a sign that we do not know where we are
                    }

                    posX = newMapInfo[(int)RampStorage.XCoord];
                    posZ = newMapInfo[(int)RampStorage.ZCoord];
                    currentHeight = maps[currentMap].UpdateHeight(currentHeight);
                    currentArea = maps[currentMap].GetArea(new byte[] { (byte)posX, (byte)posZ });

                    if (AreaInWayBack(currentArea, currentMap))
                    {
                        RemoveWayBack(currentArea, currentMap); //We are back to this area, same shortest path back
                    }
                    else
                    {
                        Log("Added mapwayback, unmapwaybacked area", true);
                        mapWayBack.Add(new List<byte[]>() { new byte[] { (byte)currentMap, (byte)currentArea },
                                                            DirToTile(direction - 2, (byte)posX, (byte)posZ) });
                    }
                    Log($"NEW: x:{posX}, z:{posZ}, dir:{direction}, map:{currentMap}, area: {currentArea}", true);
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

        #region Time check
        /// <summary>
        /// Checks the timer and returns to start if time is close to out
        /// </summary>
        static void CheckTimer()
        {
            UpdateWayBack();
            secondsToStart = StartPathSeconds();
            Log($"{timer.ElapsedMilliseconds/1000 + secondsToStart} vs {(MINUTES - 0.5) * 60} goingback:{goingBack}", true);

            if (timer.ElapsedMilliseconds/1000 + secondsToStart > (MINUTES - 0.5) * 60 && !goingBack)
            {
                if (secondsToStart < 3.5 * 60)
                {
                    for (int i = 0; i < 3; i++) Log("!%!%! Time passed, returning !%!%!", true);
                    driveWay = PathToStart();
                    driveWay.ForEach(_tile => Log($"WMWMW {_tile[0]},{_tile[1]} WMWMW", false));
                    goingBack = true;
                    BlinkLamp(BlinkOptions.Returning);
                    if (reset) return;
                }
                else
                {
                    if (!willGoBack)
                    {
                        BlinkLamp(BlinkOptions.NotReturning);
                        if (reset) return;
                        willGoBack = false;
                    }
                }
            }
        }

        static int StartPathSeconds()
        {
            int _seconds = 0;
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
                _seconds += (int)Math.Round((_driveTime + _turnTime + 10) * 1, 2); //Extra margin (for example dropping missed kits) and exit time

                if (_area != 0)
                {

                    byte[] _ramp = RampByRamptile(mapWayBack[_area][^1][0], mapWayBack[_area][^1][1], mapWayBack[_area][0][0]);
                    _seconds += (int)(Math.Round(_ramp[(int)RampStorage.RampLength] / 30f + 1) * SecondsPerDrive); //Add ramp drive time
                }
            }

            Log("Seconds back: " + secondsToStart, true);
            return _seconds;
        }

        #endregion

        #region Delays
        static void Delay(int _millis, bool _doWork)
        {
            if (_doWork)
            {
                Stopwatch sw = Stopwatch.StartNew();

                for (int i = 0; i < maps.Count; i++)
                {
                    maps[i].UpdateCrossTiles();
                    foreach (byte[] _ramp in maps[i].Ramps)
                    {
                        bool _write = MarkMapAsVisited(_ramp[(int)RampStorage.ConnectedMap], i, maps[i].GetArea(new byte[] { _ramp[(int)RampStorage.XCoord], _ramp[(int)RampStorage.ZCoord] }));
                        byte[] _tile = DirToTile(_ramp[(int)RampStorage.RampDirection], _ramp[(int)RampStorage.XCoord], _ramp[(int)RampStorage.ZCoord]);

                        if (_write != maps[i][_tile[0], _tile[1], BitLocation.explored])
                        {
                            maps[i][_tile[0], _tile[1], BitLocation.explored] = _write;
                            if (_write) maps[currentMap].AddBitInfo(DirToTile(direction, (byte)posX, (byte)posZ), BitLocation.explored);
                        }
                    }
                }

                sw.Stop();

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
        #endregion

        #endregion timing

        #region Miscellaneous

        #region Exiting
        static void Done()
        {
            if (timer.ElapsedMilliseconds < 10 && timer.IsRunning) return;
            timer.Stop();
            Log("DONE", true);
            BlinkLamp(BlinkOptions.Done);
            if (reset) return;
            Delay(15_000);
            Exit();
        }

        static void ErrorChecker()
        {
            if (errors >= MAXERRORS)
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
            LogMap();
            Delay(1000);
            listener.Stop();
            serialPort1.Close();
        }
        #endregion

        #region Logging

        /// <summary>
        /// Writes a log message to the log file
        /// </summary>
        /// <param name="_message">The message that should be logged</param>
        /// <param name="_consoleLog">Display message in console</param>
        public static void Log(string _message, bool _consoleLog)
        {
#if DEBUG
            try
            {
                File.AppendAllText(logFileName, $"\n{timer.ElapsedMilliseconds}: {_message}");
                if (_consoleLog) Console.WriteLine(_message);
            }
            catch { }
#endif
        }

        /// <summary>
        /// Logs exceptions and adds an error
        /// </summary>
        public static void LogException(Exception e)
        {
            errors += 4;
            try
            {
                Log($"Exception: {e}", false);
                Console.WriteLine($"Exception -- {e.Message}");
            }
            catch { }
        }

        static void LogMap()
        {
            try
            {
                File.WriteAllText("map.log", "Map log:\n");
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
                        File.AppendAllText("map.log", $"\n\nmap {m + 1}/{maps.Count}\n" + string.Join('\n', _mapToText) + "\n"); // Writes map to log file
                    }
                    catch (Exception e)
                    {
                        try
                        {
                            LogException(e);
                        }
                        catch { }
                    }
                }
                Delay(200);
            }
            catch
            {
                Log("Saving map failed", false);
            }
        }
        #endregion log
        #endregion misc
    }
}
