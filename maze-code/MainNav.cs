using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace SerialConsole
{
    internal class Program
    {
        // ********************************** Variables And Objects ********************************** 

        //******** Bit locations ********
        static readonly short explored = 4;
        static readonly short mapSearched = 5;
        static readonly short victim = 6;
        static readonly short ramp = 7;
        static readonly short blackTile = 8;
        static readonly short checkPointTile = 8;
        static readonly short blueTile = 10;
        static readonly short inDriveWay = 11;


        //******** Localization ********

        static int highestX = 25;
        static int highestZ = 25;
        static int lowestX = 25;
        static int lowestZ = 25;
        static readonly int startPosX = 25;
        static readonly int startPosZ = 25;
        static int toPosX;
        static int toPosZ;
        static int shortenToX;
        static int shortenToZ;
        static int posX = 25;
        static int posZ = 25;

        static int direction = 0;

        static bool locationUpdated;

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

        //******** Navigation ********

        static List<byte[]> driveWay = new();
        static List<byte[]> saveWay = new();
        static List<byte[]> crossRoads = new();
        static List<byte[]> saveCross = new();

        static bool skipNext;
        static bool returningFromGoal;
        static bool foundWay = false;
        static bool shortenAvailable;

        //******** Mapping & Checkpoints ********

        //static readonly ushort[,] mainMap = new ushort[50, 50]; //W = 0, A = 1, S = 2, D = 3, explored = 4, mapSearched = 5, victim  = 6, ramp = 7, black tile = 8, checkp = 9, blue = 10, crossroad = 11 -more? obstacles?
        static readonly List<byte[]> sinceCheckpoint = new();
        static readonly List<ushort[,]> maps = new();
        static readonly List<int[]> mapInfo = new(); //For storing info of where the ramp is
        static int currentMap = 0;
        static bool reset = false;

        //******** Rescue Kits ********

        static bool dropKits = false;
        static int dropAmount = 0;
        static char dropSide = 'l';
        static int kitsLeft = 12;


        //******** Communication ********

        static readonly SerialPort serialPort1 = new("/dev/ttyUSB0", 9600, Parity.None, 8, StopBits.One); //Edit stopbits? //"/dev/ttyUSB0" for pi, "COM3" or "COM5" for testing
        static readonly TcpListener listener = new(System.Net.IPAddress.Any, 4242);
        static TcpClient client;

        //******** Timing & exiting ********

        static readonly Stopwatch timer = new();

        // ********************************** Main Navigation Loop & Startup ********************************** 

        static void Main()
        {
            Console.CancelKeyPress += delegate (object? o, ConsoleCancelEventArgs e)
            {
                e.Cancel = false;
                Exit();
                throw new Exception();
            };

            Thread.Sleep(100);

            StartUp();

            Console.WriteLine(sinceCheckpoint[0]);

            //Main loop
            while (true)
            {
                reset = false;
                //CheckTimer();
                Turnlogic();
                Drive();
                Thread.Sleep(10);
            }
        }

        static void StartUp()
        {
            timer.Start();

            listener.Start();
            Console.WriteLine("Waiting for connection...");
            client = listener.AcceptTcpClient();
            Console.WriteLine("Client accepted");

        OpenPort:
            try
            {
                serialPort1.Open();
            }
            catch
            {
                Console.WriteLine("Cannot open port, try these: ");
                string[] _ports = SerialPort.GetPortNames();
                foreach (string _port in _ports)
                {
                    Console.WriteLine(_port);
                }

                try
                {
                    foreach (string _port in _ports)
                    {
                        Console.WriteLine(_port + " is open");
                        if (_port.Contains("/dev/ttyUSB") || _port.Contains("COM"))
                        {
                            serialPort1.PortName = _port;
                            serialPort1.Open();
                        }
                    }
                }
                catch
                {
                    Console.WriteLine("cannot switch to COM3");
                    if (!serialPort1.IsOpen)
                    {
                        serialPort1.PortName = "/dev/ttyUSB0";
                    }
                }
                Thread.Sleep(500);
                if (!serialPort1.IsOpen)
                    goto OpenPort;
            }

            Thread.Sleep(3000);

            if (serialPort1.BytesToRead != 0)
            {
                if (serialPort1.ReadLine() == "!l")
                {
                    serialPort1.DiscardInBuffer();
                    serialPort1.DiscardOutBuffer();
                }
                else
                {
                    serialPort1.WriteLine("!w");
                    while (serialPort1.BytesToRead == 0)
                    {
                        Thread.Sleep(20);
                    }
                }
            }
            else
            {
                serialPort1.WriteLine("!w");
                while (serialPort1.BytesToRead == 0)
                {
                    Thread.Sleep(20);
                }
            }

            //set all map bits to 0, update map
            currentMap = 0;
            direction = 0;
            maps.Capacity = 15;
            maps.Add(new ushort[50, 50]);
            mapInfo.Add(new int[] { 25, 25, direction });

            for (int i = 0; i < maps[0].GetLength(0); i++)
            {
                for (int j = 0; j < maps[0].GetLength(1); j++)
                {
                    maps[0][i, j] = 0;
                }
            }

            UpdateMap();
            Turn('l');
            WriteMapBit(posX, posZ, 2, leftPresent);
            Turn('r');
            AddSinceCheckPoint();

            Thread.Sleep(1000);
            Thread serverThread = new(ServerLoop);
            serverThread.Start();
        }

        static void Turnlogic()
        {
            if (reset)
                return;
            SensorCheck();
            CheckAndDropKits();
            UpdateMap();

            bool[] _unExplored = {!ReadMapBit(posX, posZ - 1, explored) && !ReadMapBit(posX, posZ, 0), //NOT BLACK TILES
                             !ReadMapBit(posX - 1, posZ, explored) && !ReadMapBit(posX, posZ, 1),
                             !ReadMapBit(posX, posZ + 1, explored) && !ReadMapBit(posX, posZ, 2),
                             !ReadMapBit(posX + 1, posZ, explored) && !ReadMapBit(posX, posZ, 3)};


            int _unExpTiles = 0;
            foreach (bool _unExpTile in _unExplored)
            {
                if (_unExpTile)
                    _unExpTiles++;
            }

        Logic:
            if (reset)
                return;
            Thread.Sleep(20);
            if (driveWay.Count == 0 && crossRoads.Count == 0 && _unExpTiles == 0 && posX == startPosX && posZ == startPosZ)
            {
                timer.Stop();
                Console.WriteLine("DONE");
                Thread.Sleep(20_000);
                return;
            }

            if (driveWay.Count > 0)
            {
                if (_unExpTiles != 0)
                {
                    crossRoads.Add(new byte[] { (byte)posX, (byte)posZ }); //CLEAR SINCECHECKP. FROM COORDS ON RESET ++ ADD CURRENT MAP
                }
                while (driveWay[0][0] == posX && driveWay[0][1] == posZ)
                {
                    driveWay.RemoveAt(0);
                }
                TurnTo(TileToDirection(driveWay[0]));
                driveWay.RemoveAt(0);
            }
            else if (_unExpTiles > 0)
            {
                if (_unExpTiles != 1)
                    crossRoads.Add(new byte[] { (byte)posX, (byte)posZ }); //CLEAR SINCECHECKP. FROM COORDS ON RESET ++ ADD CURRENT MAP
                for (int i = direction + 1; i > direction - 3; i--)
                {
                    if (_unExplored[FixDirection(i)])
                    {
                        TurnTo(FixDirection(i));
                        break;
                    }
                }
            }
            else if (_unExpTiles == 0)
            {
                if (crossRoads.Count > 1)
                {
                    byte[] _currentCoords = new byte[] { (byte)posX, (byte)posZ };
                    while (crossRoads.Contains(_currentCoords))
                    {
                        crossRoads.Remove(_currentCoords);
                    }

                    int _crossX = crossRoads.Last()[0];
                    int _crossZ = crossRoads.Last()[1];

                    while (!((!ReadMapBit(_crossX, _crossZ - 1, explored) && !ReadMapBit(_crossX, _crossZ, 0)) || //NOT BLACK TILES
                            (!ReadMapBit(_crossX - 1, _crossZ, explored) && !ReadMapBit(_crossX, _crossZ, 1)) ||
                            (!ReadMapBit(_crossX, _crossZ + 1, explored) && !ReadMapBit(_crossX, _crossZ, 2)) ||
                            (!ReadMapBit(_crossX + 1, _crossZ, explored) && !ReadMapBit(_crossX, _crossZ, 3))))
                    {
                        crossRoads.RemoveAt(crossRoads.Count - 1);

                        if (crossRoads.Count == 0)
                        {
                            FindPathTo(startPosX, startPosZ);
                            goto Logic;
                        }

                        _crossX = crossRoads.Last()[0];
                        _crossZ = crossRoads.Last()[1];
                    }

                    if (FindPathTo(_crossX, _crossZ))
                        crossRoads.RemoveAt(crossRoads.Count - 1);
                    goto Logic;
                }
                else
                {
                    FindPathTo(startPosX, startPosZ);
                    goto Logic;
                }
            }
            else
            {
                Console.WriteLine("TURN ISSUE AIUAUIWS");
            }
            /*
            SensorCheck();
            CheckAndDropKits();
            if (reset)
                return;

            while (!leftPresent || frontPresent || ReadNextTo(posX, posZ, blackTile, Directions.front) || RampCheck(direction))
            {
                if (reset)
                    return;
                if (!leftPresent && !ReadNextTo(posX, posZ, blackTile, Directions.left) && !RampCheck(direction + 1))
                {
                    Turn('l');
                    Thread.Sleep(500);
                    Drive();
                    if (ReadNextTo(posX, posZ, blackTile, Directions.front))
                    {
                        Turn('r');
                    }
                }
                else if (frontPresent || ReadNextTo(posX, posZ, blackTile, Directions.front) || RampCheck(direction))
                {
                    Turn('r');
                    Thread.Sleep(100);
                }
                else
                {
                    break;
                }
                Thread.Sleep(10);
            }
            /*
            if (driveWay.Count == 0)
            {
                
            }
            else
            {
                TurnTo(driveWay[0]);
                driveWay.RemoveAt(0);
                if (reset)
                    return;
            }*/
        }

        // ********************************** Socket server ********************************** 

        static void ServerLoop()
        {
            NetworkStream stream = client.GetStream();
            //StreamReader sr = new StreamReader(client.GetStream());
            //StreamWriter sw = new StreamWriter(client.GetStream());
            while (true)
            {
                Thread.Sleep(10);
                try
                {
                    byte[] _buffer = new byte[1024];
                    stream.Read(_buffer, 0, _buffer.Length);
                    int recv = 0;

                    foreach (byte _b in _buffer)
                    {
                        if (_b != 0)
                        {
                            recv++;
                        }
                    }

                    string _recivedData = Encoding.UTF8.GetString(_buffer, 0, recv);

                    if (_recivedData.Contains('k') && !dropKits) //&& map bit is false?
                    {
                        //If dropkits - check how many and if its the same as the already activated one do nothing
                        dropKits = true;
                        dropAmount = int.Parse(_recivedData.Substring(_recivedData.IndexOf('k') + 1, 1));
                        dropSide = _recivedData[_recivedData.IndexOf('k') + 2];
                        Console.WriteLine($"found {dropAmount}");
                    }
                    _recivedData = "";
                    //sw.WriteLine("Done");
                    //sw.Flush();
                }
                catch
                {
                    Console.WriteLine("Client falure, retrying...");
                }
                //listener.Stop();
            }
        }


        // ********************************** Driving ********************************** 

        static void Drive()
        {
            locationUpdated = false;
            CheckAndDropKits();

            if (reset)
                return;

            Console.WriteLine("Driving");

            string _recived = "";
            string _interruptRec = "";

            serialPort1.WriteLine("!d");
            Thread.Sleep(100);

            while (serialPort1.BytesToRead == 0)
            {
                Thread.Sleep(20);
            }

            _recived = serialPort1.ReadLine();
            if (_recived.Contains("!l"))
            {
                Reset();
                return;
            }
            _recived = "";
            serialPort1.DiscardInBuffer();
            Thread.Sleep(20);


            while (serialPort1.BytesToRead == 0)
            {
                Thread.Sleep(20);
                if (dropKits && !ReadMapBit(posX, posZ, victim))
                {
                    _interruptRec = Interrupt();
                    if (_interruptRec.Contains('i'))
                    {
                        CheckAndDropKits();
                    }
                    else
                    {
                        break;
                    }
                }
            }

            if (serialPort1.BytesToRead == 0)
            {
                _recived = _interruptRec;
                Thread.Sleep(800); //DO NOT REMOVE
            }
            else
            {
                _recived = serialPort1.ReadLine();
            }

            if (_recived.Contains("!l"))
            {
                Reset();
                return;
            }

            try
            {

                if (_recived[0] != '!')
                {
                    _recived = _recived.Remove(0, _recived.IndexOf('!'));
                }

                if (_recived[1] != 'a')
                {
                    Console.WriteLine(_recived);
                    Console.WriteLine("Something went wrong, retrying");
                    SensorCheck();
                    if (!frontPresent && !ReadNextTo(posX, posZ, blackTile, Directions.front))
                    {
                        Drive();
                        return;
                    }
                    else
                    {
                        return;
                    }
                }
            }
            catch
            {
                Console.WriteLine(_recived);
                Console.WriteLine("Something went very wrong, retrying");
                SensorCheck();
                if (!frontPresent && !ReadNextTo(posX, posZ, blackTile, Directions.front))
                {
                    Drive();
                    return;
                }
                else
                {
                    return;
                }
            }

            try
            {
                //    Console.Write("recived: ");
                //    Console.WriteLine(_recived); //!a,v,0
                string[] _driveInfo = _recived.Split(',');

                if (_driveInfo[2].Contains("1"))
                {
                    Console.Write("recived ramp: ");
                    Console.WriteLine(_recived);
                    if ((!ReadMapBit(posX, posZ, ramp) && currentMap == 0) || (ReadMapBit(posX, posZ, ramp) && currentMap != 0))
                    {
                        WriteMapBit(posX, posZ, ramp, true);
                        if (currentMap == 0)
                        {
                            UpRamp(direction);
                        }
                        else
                        {
                            DownRamp();
                        }

                        if (_driveInfo[1].Contains('c'))
                        {
                            WriteMapBit(posX, posZ, checkPointTile, true);
                            Console.WriteLine("New Checkp");
                            ResetSinceCheckpoint();

                            AddSinceCheckPoint();
                        }
                        else if (_driveInfo[1].Contains('b'))
                        {
                            WriteMapBit(posX, posZ, blueTile, true);
                        }

                        WriteMapBit(posX, posZ, ramp, true);
                    }
                    else
                    {
                        TurnTo(direction - 2);
                        DriveDeaf();
                    }
                }
                else
                {
                    if (_driveInfo[1].Contains('s')) //Did not move a tile
                    {
                        Console.Write("recived black tile: ");
                        Console.WriteLine(_recived);
                        //if (!leftPresent)
                        //{
                        //    Turn('l');
                        //    Thread.Sleep(100);
                        //    Drive();
                        //}
                        //else if (!rightPresent)
                        //{
                        //    Turn('r');
                        //    Thread.Sleep(100);
                        //    Drive();
                        //}

                        try
                        {
                            WriteNextTo(posX, posZ, blackTile, true, Directions.front);
                        }
                        catch
                        {
                            Console.WriteLine("Error - cannot write black tile in front!!!");
                            Thread.Sleep(5000);
                        }
                    }
                    else //Moved a tile
                    {
                        if (!locationUpdated)
                        {
                            UpdateLocation();
                            locationUpdated = true;
                        }
                        UpdateMap();

                        if (_driveInfo[1].Contains('c'))
                        {
                            Console.Write("recived checkpoint: ");
                            Console.WriteLine(_recived);
                            WriteMapBit(posX, posZ, checkPointTile, true);

                            if (ReadMapBit(posX, posZ, checkPointTile))
                            {
                                Console.WriteLine("New Checkp");
                                ResetSinceCheckpoint();
                            }
                            AddSinceCheckPoint();
                        }
                        else if (_driveInfo[1].Contains('b'))
                        {
                            Console.Write("recived blue tile: ");
                            Console.WriteLine(_recived);
                            WriteMapBit(posX, posZ, blueTile, true);
                        }
                    }
                }
            }
            catch
            {
                Console.WriteLine(_recived + " -problem color");
            }

            Thread.Sleep(10);

            serialPort1.DiscardInBuffer();
            serialPort1.DiscardOutBuffer();

            Thread.Sleep(50);

            SensorCheck();
            CheckAndDropKits();
        }

        static void DriveDeaf()
        {
            string _recived = "";

            serialPort1.WriteLine("!d");
            Thread.Sleep(100);

            while (serialPort1.BytesToRead == 0)
            {
                Thread.Sleep(20);
            }

            _recived = serialPort1.ReadLine();
            if (_recived.Contains("!l"))
            {
                Reset();
                return;
            }
            _recived = "";
            serialPort1.DiscardInBuffer();
            Thread.Sleep(200);

            while (serialPort1.BytesToRead == 0)
            {
                Thread.Sleep(20);
            }

            _recived = serialPort1.ReadLine();
            if (_recived.Contains("!l"))
            {
                Reset();
                return;
            }

            try
            {
                if (_recived[0] != '!')
                {
                    _recived = _recived.Remove(0, _recived.IndexOf('!'));
                }

                if (_recived[1] != 'a')
                {
                    Console.WriteLine(_recived);
                    Console.WriteLine("Something went wrong, retrying");
                    SensorCheck();
                    if (!frontPresent && !ReadNextTo(posX, posZ, blackTile, Directions.front))
                    {
                        DriveDeaf();
                    }
                    else
                    {
                        return;
                    }
                }
            }
            catch
            {
                Console.WriteLine(_recived);
                Console.WriteLine("Something went very wrong, retrying");
                SensorCheck();
                if (!frontPresent && ReadNextTo(posX, posZ, blackTile, Directions.front))
                {
                    DriveDeaf();
                }
                else
                {
                    return;
                }
            }

            Thread.Sleep(10);

            serialPort1.DiscardInBuffer();
            serialPort1.DiscardOutBuffer();

            Thread.Sleep(50);

            SensorCheck();
            CheckAndDropKits();
        }


        // ********************************** Turning ********************************** 

        static void Turn(char _direction)
        {
            if (reset)
                return;
            if (_direction == 'l')
            {
                Console.WriteLine("Turning left");
                SerialComm("!t,l"); //turn left
                UpdateDirection(1);
            }
            else if (_direction == 'r')
            {
                Console.WriteLine("Turning right");
                SerialComm("!t,r"); //turn right
                UpdateDirection(-1);
            }
            else
            {
                Console.WriteLine("Turn - WRONG DIRECTION");
            }
            SensorCheck();
            CheckAndDropKits();
        }

        static void TurnTo(int _toDirection)
        {
            if (reset)
                return;
            while (_toDirection > 3)
            {
                _toDirection -= 4;
            }
            while (_toDirection < 0)
            {
                _toDirection += 4;
            }

            while (_toDirection != direction)
            {
                Turn('r'); //turn right

                UpdateDirection(-1);
            }
            SensorCheck();
            CheckAndDropKits();
        }


        // ********************************** Serial Communication ********************************** 

        static void SensorCheck()
        {
            if (reset)
                return;
            Console.WriteLine("checking sensors");
            string _sensorInfo = SerialComm("!w");
            try
            {
                if (_sensorInfo[1] == 'a')
                {
                    //'0' = 00110000, '1' = 00110001
                    byte _sensorData = (byte)_sensorInfo.Split(',')[1][0];

                    rightPresent = (_sensorData & 0b1) == 1;
                    leftPresent = ((_sensorData >> 1) & 0b1) == 1;
                    frontPresent = ((_sensorData >> 2) & 0b1) == 1;
                    Console.WriteLine((frontPresent ? "1" : "0") + (leftPresent ? "1" : "0") + (rightPresent ? "1" : "0"));
                }
                else
                {
                    Console.WriteLine($"{_sensorInfo} - Sensorcheck error - incorrect format recived, trying again");
                    SensorCheck();
                }
            }
            catch
            {
                if (_sensorInfo == "!l")
                {
                    return;
                }
                Console.WriteLine($"{_sensorInfo} - Sensorcheck error - incorrect format recived, trying again");
                SensorCheck();
            }
        }

        static void CheckAndDropKits()
        {
            if (reset)
                return;

            if (dropKits && !ReadMapBit(posX, posZ, victim))
            {
                if (kitsLeft < dropAmount)
                    dropAmount = 0;
                Console.WriteLine($"Dropping {dropAmount} kits");
                string _recived = SerialComm($"!k,{dropAmount},{dropSide}");
                kitsLeft -= dropAmount;
                try
                {
                    if (_recived[1] == 's' || _recived.Split(',')[1].Contains('0'))
                    {
                        WriteMapBit(posX, posZ, victim, true);
                    }
                    else if (_recived.Split(',')[1].Contains('1'))
                    {
                        if (!locationUpdated)
                        {
                            UpdateLocation();
                            locationUpdated = true;
                        }
                        WriteMapBit(posX, posZ, victim, true);
                        Console.WriteLine(ReadMapBit(posX, posZ, victim));
                    }
                    else
                    {
                        Console.Write(_recived);
                        Console.WriteLine(" -problem with kit step");
                    }
                }
                catch
                {
                    if (_recived.Contains("!l"))
                    {
                        return;
                    }
                    Console.Write(_recived);
                    Console.WriteLine(" WRONG");
                }
                Thread.Sleep(100);
                dropKits = false;
            }
            else if (dropKits)
            {
                dropKits = false;
            }
        }

        static string Interrupt()
        {
            if (reset)
                return "!l";
            Console.WriteLine("interrupting");
            string _recived = SerialComm("!i");
            Thread.Sleep(100);
            return _recived;
        }

        static string SerialComm(string _send)
        {
            if (reset)
                return "!l";

            serialPort1.WriteLine(_send);

            while (serialPort1.BytesToRead == 0)
            {
                Thread.Sleep(20);
            }

            Thread.Sleep(50);
            string _recived = serialPort1.ReadLine();

            if (_recived.Contains("!l"))
            {
                Reset();
                return _recived;
            }

            try
            {
                if (_recived[0] != '!')
                {
                    _recived = _recived.Remove(0, _recived.IndexOf('!'));
                }

                if (_recived[1] != 's' && _recived[1] != 'a' && _recived[1] != 'l')
                {
                    Console.WriteLine("Something went wrong, retrying");
                    Console.WriteLine(_recived);
                    _recived = SerialComm(_send);
                }
            }
            catch
            {
                Console.WriteLine(_recived);
                Console.WriteLine("Something went very wrong, retrying");
                _recived = SerialComm(_send);
            }

            serialPort1.DiscardInBuffer();
            serialPort1.DiscardOutBuffer();
            Thread.Sleep(100);
            return _recived;
        }


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

            Console.WriteLine("********* ACHTUNG ACHTUNG, DAS IST NICHT GUT ************");
            Console.WriteLine("********* ACHTUNG ACHTUNG, DAS IST NICHT GUT ************");
            Console.WriteLine("********* ACHTUNG ACHTUNG, DAS IST NICHT GUT ************");
            Console.WriteLine("********* ACHTUNG ACHTUNG, DAS IST NICHT GUT ************");
            Console.WriteLine("********* ACHTUNG ACHTUNG, DAS IST NICHT GUT ************");
            return -1;
        }

        static void UpRamp(int _rampDirection)
        {
            Console.WriteLine("Going up ramp");

            maps.Add(new ushort[50, 50]);
            currentMap = maps.Count - 1;
            mapInfo.Add(new int[] { 25, 25, _rampDirection });
            posX = 25;
            posZ = 25;
        }

        static void DownRamp()
        {
            Console.WriteLine("Going down ramp");

            if (currentMap != 0)
            {
                int[] _info = mapInfo[currentMap];
                posX = _info[0];
                posZ = _info[1];
                direction = _info[2];
                currentMap = 0;
                UpdateDirection(2);
            }
            else
            {
                Console.WriteLine("ERROR TRIED TO GO DOWN RAMP VIOAFUIEB");
            }
        }

        static void UpdateLocation() //ONLY USE AFTER YOU KNOW THERE IS A NEW CELL
        {
            if (direction == 0)
            {
                posZ--;
            }
            else if (direction == 1)
            {
                posX--;
            }
            else if (direction == 2)
            {
                posZ++;
            }
            else if (direction == 3)
            {
                posX++;
            }

            if (posX > maps[currentMap].GetLength(0))
            {
                posX = maps[currentMap].GetLength(0);
                Console.WriteLine("Error PosX");
            }
            if (posX < 0)
            {
                posX = 0;
                Console.WriteLine("Error posX");
            }
            if (posZ > maps[currentMap].GetLength(1))
            {
                posZ = maps[currentMap].GetLength(1);
                Console.WriteLine("Error PosZ");
            }
            if (posZ < 0)
            {
                posZ = 0;
                Console.WriteLine("Error posZ");
            }

            if (posX == startPosX && posZ == startPosZ && currentMap == 0 && timer.ElapsedMilliseconds > (7 * 60 + 30) * 1000)
            {
                Thread.Sleep(30_000);
            }

            if (posX > highestX)
                highestX = posX;
            if (posX < lowestX)
                lowestX = posX;
            if (posZ > highestZ)
                highestZ = posZ;
            if (posZ < lowestZ)
                lowestZ = posZ;
        }

        static void UpdateMap()//Behind - Always false when driving normally
        {
            SensorCheck();

            if (!ReadMapBit(posX, posZ, explored)) // WASD, forward left back right
            {
                bool wallNZ;
                bool wallNX;
                bool wallPZ;
                bool wallPX;
                if (direction == 0)
                {
                    SensorCheck();
                    wallNZ = frontPresent; //front is front
                    wallNX = leftPresent;
                    wallPZ = false;
                    wallPX = rightPresent;
                }
                else if (direction == 1)
                {
                    SensorCheck();
                    wallNZ = rightPresent; //right is front
                    wallNX = frontPresent;
                    wallPZ = leftPresent;
                    wallPX = false;
                }
                else if (direction == 2)
                {
                    SensorCheck();
                    wallNZ = false; //back is front
                    wallNX = rightPresent;
                    wallPZ = frontPresent;
                    wallPX = leftPresent;
                }
                else if (direction == 3)
                {
                    SensorCheck();
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
                    Console.WriteLine("ERROR");
                }

                WriteMapBit(posX, posZ, 0, wallNZ);
                WriteMapBit(posX, posZ, 1, wallNX);
                WriteMapBit(posX, posZ, 2, wallPZ);
                WriteMapBit(posX, posZ, 3, wallPX);
                WriteMapBit(posX, posZ, explored, true);

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
            }

            Console.Write(posX + " , " + posZ + ": ");

            for (int i = 15; i >= 0; i--)
            {
                Console.Write(ReadMapBit(posX, posZ, i) ? "1" : "0");
            }
            Console.WriteLine();
        }

        static bool RampCheck(int _direction)
        {
            if (currentMap != 0)
                return false;

            while (_direction > 3)
            {
                _direction -= 4;
            }
            while (_direction < 0)
            {
                _direction += 4;
            }

            if (ReadMapBit(posX, posZ, ramp))
            {
                foreach (int[] _mapInform in mapInfo)
                {
                    if (_mapInform[2] == _direction)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        // ********************************** Checkpoints ********************************** 

        static void Reset()
        {
            Console.WriteLine("Reset");
            reset = true;
            try
            {
                serialPort1.DiscardInBuffer();
                serialPort1.DiscardOutBuffer();
                driveWay.Clear();
                crossRoads = new List<byte[]>(saveCross);

                if (sinceCheckpoint.Count > 0)
                {
                    byte[] _checkpointXZ = sinceCheckpoint[0];
                    posX = _checkpointXZ[0];
                    posZ = _checkpointXZ[1];
                    currentMap = _checkpointXZ[2];
                    direction = _checkpointXZ[3];

                    Console.WriteLine("Started reset");
                    for (int i = 0; i < sinceCheckpoint.Count; i++)
                    {
                        byte[] _coords = sinceCheckpoint[0];
                        if (ReadMapBit(posX, posZ, victim))
                        {
                            maps[_coords[2]][_coords[0], _coords[1]] = 0;
                            WriteMapBit(_coords[0], _coords[1], victim, true, _coords[2]);
                        }
                        else
                        {
                            maps[_coords[2]][_coords[0], _coords[1]] = 0;
                        }
                        sinceCheckpoint.RemoveAt(0);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Reset error");
                Console.WriteLine(e.Message);
            }

            Thread.Sleep(100);
            serialPort1.DiscardInBuffer();
            serialPort1.DiscardOutBuffer();

            dropKits = false;
            SerialComm("!w");
            Thread.Sleep(100);
        }

        static void AddSinceCheckPoint()
        {
            if (sinceCheckpoint.Count == 0)
            {
                sinceCheckpoint.Add(new byte[] { (byte)posX, (byte)posZ, (byte)currentMap, (byte)direction });
            }
            else if (!ReadMapBit(posX, posZ, explored))
            {
                sinceCheckpoint.Add(new byte[] { (byte)posX, (byte)posZ, (byte)currentMap });
            }
        }

        static void ResetSinceCheckpoint()
        {
            sinceCheckpoint.Clear();
            saveCross = new List<byte[]>(crossRoads);
        }


        // ********************************** Read And Write Map ********************************** 

        static bool ReadMapBit(int _x, int _z, int _read)
        {
            return ((maps[currentMap][_x, _z] >> _read) & 0b1) == 1;
        }

        static void WriteMapBit(int _x, int _z, int _write, bool _value)
        {
            ushort _t = (ushort)(0b1 << _write);
            if (_value)
            {
                maps[currentMap][_x, _z] = (ushort)(maps[0][_x, _z] | (_t));
            }
            else
            {
                maps[currentMap][_x, _z] = (ushort)(maps[0][_x, _z] & ~_t);
            }
        }

        static void WriteMapBit(int _x, int _z, int _write, bool _value, int _mapIndex)
        {
            ushort _t = (ushort)(0b1 << _write);
            if (_value)
            {
                maps[_mapIndex][_x, _z] = (ushort)(maps[0][_x, _z] | (_t));
            }
            else
            {
                maps[_mapIndex][_x, _z] = (ushort)(maps[0][_x, _z] & ~_t);
            }
        }

        static void WriteNextTo(int _x, int _z, int _write, bool value, Directions _toDirection)
        {
            if ((direction == 0 && _toDirection == Directions.front) || (direction == 1 && _toDirection == Directions.right) || (direction == 2 && _toDirection == Directions.back) || (direction == 3 && _toDirection == Directions.left))
            {
                WriteMapBit(_x, _z - 1, _write, value);
            }
            else if ((direction == 0 && _toDirection == Directions.left) || (direction == 1 && _toDirection == Directions.front) || (direction == 2 && _toDirection == Directions.right) || (direction == 3 && _toDirection == Directions.back))
            {
                WriteMapBit(_x - 1, _z, _write, value);
            }
            else if ((direction == 0 && _toDirection == Directions.back) || (direction == 1 && _toDirection == Directions.left) || (direction == 2 && _toDirection == Directions.front) || (direction == 3 && _toDirection == Directions.right))
            {
                WriteMapBit(_x, _z + 1, _write, value);
            }
            else if ((direction == 0 && _toDirection == Directions.right) || (direction == 1 && _toDirection == Directions.back) || (direction == 2 && _toDirection == Directions.left) || (direction == 3 && _toDirection == Directions.front))
            {
                WriteMapBit(_x + 1, _z, _write, value);
            }
            else
            {
                Console.WriteLine("ReadNextTo Direction error ");
            }
        }

        static bool ReadNextTo(int _x, int _z, int _read, Directions _toDirection)
        {
            if ((direction == 0 && _toDirection == Directions.front) || (direction == 1 && _toDirection == Directions.right) || (direction == 2 && _toDirection == Directions.back) || (direction == 3 && _toDirection == Directions.left))
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
            }
            return false;
        }


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

        static void Exit()
        {
            string[] mapToText = new string[(3 + highestZ - lowestZ)];
            int loops = 0;
            for (int i = lowestZ - 1; i <= highestZ + 1; i++)
            {
                for (int j = lowestX - 1; j <= highestX + 1; j++)
                {
                    string bits = $"{i};{j}:";
                    for (int k = 15; k >= 0; k--)
                    {
                        bits += ReadMapBit(i, j, k) ? "1" : "0";
                    }

                    mapToText[loops] += bits + ",";
                }
                loops++;
            }
            try
            {
                File.WriteAllText(/*@"C:\Users\0515frma\Desktop\Test\log.txt"*/"map.txt", string.Join('\n', mapToText)); // Writes array to text file
            }
            catch (Exception e)
            {
                Console.Write("Could not create log because ");
                Console.WriteLine(e.Message);
            }
            Console.WriteLine("done file");
            Thread.Sleep(1000);
            listener.Stop();
            serialPort1.Close();
            //throw new Exception();
        }


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

        static void GoBackLevel(int _map)
        {
            if (_map != currentMap)
            {
                toPosX = 25;
                toPosZ = 25;

                foundWay = false;
                FindFrom((byte)posX, (byte)posZ);
                foundWay = false;
                driveWay.ForEach(num => Console.WriteLine(num + " , "));

                for (int i = 0; i < maps[currentMap].GetLength(0); i++)
                {
                    for (int j = 0; j < maps[currentMap].GetLength(1); j++)
                    {
                        WriteMapBit(i, j, mapSearched, false);
                    }
                }
            }

            while (driveWay.Count > 0)
            {
                TurnTo(TileToDirection(driveWay[0]));
                driveWay.RemoveAt(0);
                Drive();
            }

            TurnTo(3);
            Drive();
            DownRamp();

            if (_map != currentMap)
            {
                Console.WriteLine("Cannot find map");
            }
        }

        static bool FindPathTo(int _toX, int _toZ)
        {
            // Test each cell for options
            if (_toX == posX && _toZ == posZ)
                return true;

            toPosX = _toX;
            toPosZ = _toZ;

            foundWay = false;
            skipNext = false;
            returningFromGoal = false;
            shortenAvailable = false;
            FindFrom((byte)posX, (byte)posZ);

            if ((driveWay.Count > saveWay.Count || driveWay.Count == 0) && saveWay.Count > 0)
            {
                driveWay = new List<byte[]>(saveWay);
            }

            //driveWay.ForEach(num => Debug.Log(num.X + " , " + num.Z + " ; "));
            Console.WriteLine($"FINDPATHTO {_toX},{_toZ} from {posX},{posZ} AT {driveWay.Count} LENGTH");

            for (int i = 0; i < maps[currentMap].GetLength(0); i++)
            {
                for (int j = 0; j < maps[currentMap].GetLength(1); j++)
                {
                    WriteMapBit(i, j, mapSearched, false);
                    WriteMapBit(i, j, inDriveWay, false);
                }
            }

            saveWay.Clear();
            if (driveWay.Count == 0 && _toX != posX && _toZ != posZ)
            {
                Console.WriteLine("!!!!!!Could not find path!!!!!!!!!!");
                return false;
            }

            return true;
        }

        static void FindFrom(byte _onX, byte _onZ)
        {
            WriteMapBit(_onX, _onZ, inDriveWay, true);

            if (Math.Abs(_onZ - toPosZ) >= Math.Abs(_onX - toPosX))
            {
                if (_onZ <= toPosZ)
                {
                    FindPZ(_onX, _onZ);
                    returningFromGoal = false;
                }
                else
                {
                    FindNZ(_onX, _onZ);
                    returningFromGoal = false;
                }

                if (_onX <= toPosX)
                {
                    FindPX(_onX, _onZ);
                    returningFromGoal = false;
                }
                else
                {
                    FindNX(_onX, _onZ);
                    returningFromGoal = false;
                }

                if (_onZ > toPosZ)
                {
                    FindPZ(_onX, _onZ);
                    returningFromGoal = false;
                }
                else
                {
                    FindNZ(_onX, _onZ);
                    returningFromGoal = false;
                }

                if (_onX > toPosX)
                {
                    FindPX(_onX, _onZ);
                    returningFromGoal = false;
                }
                else
                {
                    FindNX(_onX, _onZ);
                    returningFromGoal = false;
                }
            }
            else
            {
                if (_onX <= toPosX)
                {
                    FindPX(_onX, _onZ);
                    returningFromGoal = false;
                }
                else
                {
                    FindNX(_onX, _onZ);
                    returningFromGoal = false;
                }

                if (_onZ <= toPosZ)
                {
                    FindPZ(_onX, _onZ);
                    returningFromGoal = false;
                }
                else
                {
                    FindNZ(_onX, _onZ);
                    returningFromGoal = false;
                }

                if (_onX > toPosX)
                {
                    FindPX(_onX, _onZ);
                    returningFromGoal = false;
                }
                else
                {
                    FindNX(_onX, _onZ);
                    returningFromGoal = false;
                }

                if (_onZ > toPosZ)
                {
                    FindPZ(_onX, _onZ);
                    returningFromGoal = false;
                }
                else
                {
                    FindNZ(_onX, _onZ);
                    returningFromGoal = false;
                }
            }
        }

        static void SearchCell(byte _onX, byte _onZ)
        {
            if (_onX == toPosX && _onZ == toPosZ)
            {
                Console.WriteLine("Found " + driveWay.Count + " long");

                if ((driveWay.Count < saveWay.Count || saveWay.Count == 0) && driveWay.Count > 0)
                {
                    saveWay = new List<byte[]>(driveWay);
                    if (saveWay.Count == Math.Abs(posX - toPosX) + Math.Abs(posZ - toPosZ))
                    {
                        foundWay = true;
                    }
                }
                skipNext = true;
                returningFromGoal = true;
                return;
            }

            bool returning = returningFromGoal;
            returningFromGoal = false;

            WriteMapBit(_onX, _onZ, inDriveWay, true);
            //Better way when these are equal?
            if (Math.Abs(_onZ - toPosZ) >= Math.Abs(_onX - toPosX))
            {
                if (_onZ <= toPosZ)
                {
                    FindPZ(_onX, _onZ);
                }
                else
                {
                    FindNZ(_onX, _onZ);
                }

                if (_onX <= toPosX)
                {
                    FindPX(_onX, _onZ);
                }
                else
                {
                    FindNX(_onX, _onZ);
                }

                if (_onZ > toPosZ)
                {
                    FindPZ(_onX, _onZ);
                }
                else
                {
                    FindNZ(_onX, _onZ);
                }

                if (_onX > toPosX)
                {
                    FindPX(_onX, _onZ);
                }
                else
                {
                    FindNX(_onX, _onZ);
                }
            }
            else
            {
                if (_onX <= toPosX)
                {
                    FindPX(_onX, _onZ);
                }
                else
                {
                    FindNX(_onX, _onZ);
                }

                if (_onZ <= toPosZ)
                {
                    FindPZ(_onX, _onZ);
                }
                else
                {
                    FindNZ(_onX, _onZ);
                }

                if (_onX > toPosX)
                {
                    FindPX(_onX, _onZ);
                }
                else
                {
                    FindNX(_onX, _onZ);
                }

                if (_onZ > toPosZ)
                {
                    FindPZ(_onX, _onZ);
                }
                else
                {
                    FindNZ(_onX, _onZ);
                }
            }

            WriteMapBit(_onX, _onZ, inDriveWay, false);
            returningFromGoal = returning || returningFromGoal;
            if (!returningFromGoal)
            {
                WriteMapBit(_onX, _onZ, mapSearched, true);
            }

            skipNext = false;
        }

        static void FindNZ(byte _onX, byte _onZ)
        {
            if (skipNext)
            {
                return;
            }
            if (shortenAvailable)
            {
                if (shortenToX == _onX && shortenToZ == _onZ)
                {
                    shortenAvailable = false;
                }
                else
                {
                    return;
                }
            }
            if (!foundWay && _onZ - 1 >= 0 /*&& !ReadMapBit(_onX, _onZ, shortestPath)*/) //If there is a position one step +Z
            {
                if (ReadMapBit(_onX, _onZ - 1, explored) && !ReadMapBit(_onX, _onZ - 1, mapSearched) && !ReadMapBit(_onX, _onZ, 0) && !ReadMapBit(_onX, _onZ - 1, inDriveWay))
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
            if (shortenAvailable)
            {
                if (shortenToX == _onX && shortenToZ == _onZ)
                {
                    shortenAvailable = false;
                }
                else
                {
                    return;
                }
            }
            if (!foundWay && _onZ + 1 < maps[currentMap].GetLength(1)) //If there is a position one step -Z
            {
                if (ReadMapBit(_onX, _onZ + 1, explored) && !ReadMapBit(_onX, _onZ + 1, mapSearched) && !ReadMapBit(_onX, _onZ, 2) && !ReadMapBit(_onX, _onZ + 1, inDriveWay))
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
            if (shortenAvailable)
            {
                if (shortenToX == _onX && shortenToZ == _onZ)
                {
                    shortenAvailable = false;
                }
                else
                {
                    return;
                }
            }
            if (!foundWay && _onX + 1 < maps[currentMap].GetLength(1)) //If there is a position one step +X
            {
                if (ReadMapBit(_onX + 1, _onZ, explored) && !ReadMapBit(_onX + 1, _onZ, mapSearched) && !ReadMapBit(_onX, _onZ, 3) && !ReadMapBit(_onX + 1, _onZ, inDriveWay))
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
            if (shortenAvailable)
            {
                if (shortenToX == _onX && shortenToZ == _onZ)
                {
                    shortenAvailable = false;
                }
                else
                {
                    return;
                }
            }

            if (!foundWay && _onX - 1 >= 0) //If there is a position one step -X
            {
                if (ReadMapBit(_onX - 1, _onZ, explored) && !ReadMapBit(_onX - 1, _onZ, mapSearched) && !ReadMapBit(_onX, _onZ, 1) && !ReadMapBit(_onX - 1, _onZ, inDriveWay))
                {
                    driveWay.Add(new byte[] { (byte)(_onX - 1), _onZ });
                    SearchCell((byte)(_onX - 1), _onZ);
                    if (!foundWay)
                        driveWay.RemoveAt(driveWay.Count - 1);
                }
            }
        }

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
