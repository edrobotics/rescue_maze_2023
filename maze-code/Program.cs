using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO.Ports;
using System.Net.Sockets;
using System.Text;

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


        //******** Localization ********

        static readonly int startPosX = 25;
        static readonly int startPosZ = 25;
        static int toPosX;
        static int toPosZ;
        static int shortenToX;
        static int shortenToZ;
        static int posX = 25;
        static int posZ = 25;

        static int direction = 0;

        static bool foundWay = false;
        static bool shortenAvailable;
        static readonly List<int> driveWay = new();

        static bool locationUpdated;

        static bool frontPresent;
        static bool leftPresent;
        static bool rightPresent;
        static bool wallPZ;
        static bool wallNX;
        static bool wallNZ;
        static bool wallPX;

        //******** Mapping & Checkpoints ********

        //static readonly ushort[,] mainMap = new ushort[50, 50]; //W = 0, A = 1, S = 2, D = 3, explored = 4, mapSearched = 5, victim  = 6, ramp = 7, black tile = 8, checkp = 9, blue = 10 -more? obstacles?
        static readonly List<string> sinceCheckpoint = new();
        static readonly List<ushort[,]> maps = new();
        static readonly List<string> mapInfo = new();
        static int currentMap = 0;

        //******** Rescue Kits ********

        static bool dropKits = false;
        static int dropAmount;
        static char dropSide;
        static int kitsLeft = 12;


        //******** Communication ********

        static readonly SerialPort serialPort1 = new("/dev/ttyUSB0", 9600, Parity.None, 8, StopBits.One); //Edit stopbits? //"/dev/ttyUSB0" for pi, "COM3" or "COM5" for testing
        static readonly TcpListener listener = new(System.Net.IPAddress.Any, 4242);
        static TcpClient client;

        //check for binary switch reset - in serialcomm + drive?,
        //Test (+finish?) sinceCheckpoint
        //Send to auriga if there is likely an obstacle present?
        //Compare map to sensor and vision data?
        //If we are in a dead end OR at every turn - look for map openings? Store divergefrompath coords?
        //See how many (driveway = 0 && map[explored}) tiles - if many we are likely going around a floating wall
        //Multiple map fixsa
        //ADD LIST FOR SHORTEST PATH?

        // ********************************** Main Navigation Loop ********************************** 

        static void Main()
        {
            Thread.Sleep(100);

            StartUp();

            //Main loop
            while (true)
            {
                Turnlogic();
                Drive();
                Thread.Sleep(10);
            }
        }

        static void StartUp()
        {
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
                        serialPort1.PortName = _port;
                        serialPort1.Open();
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
            serialPort1.DiscardInBuffer();
            serialPort1.DiscardOutBuffer();

            SerialComm("!w");
            //set all map bits to 0, update map
            currentMap = 0;
            direction = 0;
            maps.Add(new ushort[50, 50]);
            AddMapInfo(25, 25, direction, currentMap);

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

            Thread.Sleep(1000);
            Thread t = new(ServerLoop);
            t.Start();
        }

        static void Turnlogic()
        {
            SensorCheck();
            CheckAndDropKits();
            if (driveWay.Count == 0)
            {
                while (!leftPresent || frontPresent || ReadInFront(posX, posZ, blackTile))
                {
                    if (!leftPresent)
                    {
                        Turn('l');
                        Thread.Sleep(500);
                        Drive();
                    }
                    else if (frontPresent || ReadInFront(posX, posZ, blackTile))
                    {
                        Turn('r');
                        Thread.Sleep(100);
                    }
                    Thread.Sleep(10);
                }
            }
            else
            {
                TurnTo(driveWay[0]);
                driveWay.RemoveAt(0);
            }
        }

        // ********************************** Socket server ********************************** 

        static void ServerLoop() // I ONLY READ DATA THAT IS NOT 0
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
                    Console.WriteLine("Something went wrong...");
                }
                //listener.Stop();
            }
        }

        // ********************************** Driving ********************************** 

        static void Drive()
        {
            locationUpdated = false;
            CheckAndDropKits();

            Console.WriteLine("Driving");

            serialPort1.WriteLine("!d");
            Thread.Sleep(100);

            string _recived;
            string _interruptRec = "";

            while (serialPort1.BytesToRead == 0)
            {
                Thread.Sleep(20);
            }

            _recived = serialPort1.ReadLine();
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
                    if (!frontPresent || !ReadInFront(posX, posZ, blackTile))
                    {
                        Drive();
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
                if (!frontPresent || !ReadInFront(posX, posZ, blackTile))
                {
                    Drive();
                }
                else
                {
                    return;
                }
            }

            try
            {
                if (_recived.Split(',')[2] == "1")
                {
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

                        if (_recived.Split(',')[1] == "c")
                        {
                            WriteMapBit(posX, posZ, checkPointTile, true);

                            if (ReadMapBit(posX, posZ, checkPointTile))
                            {
                                Console.WriteLine("New Checkp");
                                ResetSinceCheckpoint();
                            }
                            AddSinceCheckPoint();
                        }
                        else if (_recived.Split(',')[1] == "b")
                        {
                            WriteMapBit(posX, posZ, blueTile, true);
                        }

                        WriteMapBit(posX, posZ, ramp, true);
                    }
                    else
                    {
                        TurnTo(direction - 2);
                        DriveBlind();
                    }
                }
                else
                {
                    if (_recived.Split(',')[1] == "s") //Did not move a tile
                    {
                        try
                        {
                            WriteInFront(posX, posZ, blackTile, true);
                        }
                        catch
                        {
                            Console.WriteLine("Error - no black tile in front!!!");
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

                        if (_recived.Split(',')[1] == "c")
                        {
                            WriteMapBit(posX, posZ, checkPointTile, true);

                            if (ReadMapBit(posX, posZ, checkPointTile))
                            {
                                Console.WriteLine("New Checkp");
                                ResetSinceCheckpoint();
                            }
                            AddSinceCheckPoint();
                        }
                        else if (_recived.Split(',')[1] == "b")
                        {
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

        static void DriveBlind()
        {
            locationUpdated = false;
            CheckAndDropKits();

            serialPort1.WriteLine("!d");
            Thread.Sleep(100);

            string _recived;
            string _interruptRec = "";

            while (serialPort1.BytesToRead == 0)
            {
                Thread.Sleep(20);
            }

            serialPort1.DiscardInBuffer();
            Thread.Sleep(200);

            while (serialPort1.BytesToRead == 0)
            {
                Thread.Sleep(20);
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
                    if (!frontPresent || !ReadInFront(posX, posZ, blackTile))
                    {
                        DriveBlind();
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
                if (!frontPresent || ReadInFront(posX, posZ, blackTile))
                {
                    DriveBlind();
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
            Console.WriteLine("checking sensors");
            string _sensorInfo = SerialComm("!w");
            try
            {
                if (_sensorInfo[1] == 'a')
                {
                    byte _sensorData = (byte)_sensorInfo.Split(',')[1][0];

                    rightPresent = (_sensorData & 0b1) == 1;
                    leftPresent = ((_sensorData >> 1) & 0b1) == 1;
                    frontPresent = ((_sensorData >> 2) & 0b1) == 1;
                    Console.WriteLine((frontPresent ? "1" : "0") + (leftPresent ? "1" : "0") + (rightPresent ? "1" : "0"));
                }
                else
                {
                    Console.WriteLine(_sensorInfo + " - Sensorcheck error - incorrect format recived, trying again");
                    SensorCheck();
                }
            }
            catch
            {
                Console.WriteLine(_sensorInfo + " - Sensorcheck error - incorrect format recived, retrying");
                SensorCheck();
            }
        }

        static void CheckAndDropKits()
        {
            if (dropKits && !ReadMapBit(posX, posZ, victim))
            {
                if (kitsLeft < dropAmount)
                    dropAmount = 0;
                Console.WriteLine($"Dropping {dropAmount} kits");
                string recived = SerialComm($"!k,{dropAmount},{dropSide}");
                kitsLeft -= dropAmount;
                try
                {
                    if (recived[1] == 's' || recived.Split(',')[1].Contains('0'))
                    {
                        WriteMapBit(posX, posZ, victim, true);
                    }
                    else if (recived.Split(',')[1].Contains('1'))
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
                        Console.Write(recived);
                        Console.WriteLine(" -problem with kit step");
                    }
                }
                catch
                {
                    Console.Write(recived);
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
            Console.WriteLine("interrupting");
            string _recived = SerialComm("!i");
            Console.WriteLine(_recived);
            Thread.Sleep(100);
            return _recived;
        }

        static string SerialComm(string _send)
        {
            serialPort1.WriteLine(_send);

            while (serialPort1.BytesToRead == 0)
            {
                Thread.Sleep(20);
            }

            Thread.Sleep(50);
            string _recived = serialPort1.ReadLine();

            try
            {
                if (_recived[0] != '!')
                {
                    _recived = _recived.Remove(0, _recived.IndexOf('!'));
                }

                if (_recived[1] != 's' && _recived[1] != 'a')
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

        static void UpdateDirection(int leftTurns)
        {
            direction += leftTurns;

            while (direction > 3)
            {
                direction -= 4;
            }
            while (direction < 0)
            {
                direction += 4;
            }
        }

        static void UpRamp(int _rampDirection)
        {
            maps.Add(new ushort[50, 50]);
            currentMap = maps.Count - 1;
            AddMapInfo(posX, posZ, _rampDirection, currentMap);
            posX = 25;
            posZ = 25;
        }

        static void DownRamp()
        {
            if (currentMap != 0)
            {
                string[] _info = mapInfo[currentMap].Split(',');
                posX = int.Parse(_info[0]);
                posZ = int.Parse(_info[1]);
                direction = int.Parse(_info[2]);
                currentMap = 0;
                UpdateDirection(0);
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
        }

        static void UpdateMap()//Behind - Always false when driving normally, always(?) true at start, CHANGE IF DIMENSION 3 ON MAP IS CHANGED?
        {
            SensorCheck();

            if (!ReadMapBit(posX, posZ, explored)) // WASD, forward left back right
            {
                if (direction == 0)//something wrong?
                {
                    SensorCheck();
                    wallPZ = frontPresent; //front is front
                    wallNX = leftPresent;
                    wallNZ = false;
                    wallPX = rightPresent;
                }
                if (direction == 1)
                {
                    SensorCheck();
                    wallPZ = rightPresent; //right is front = Z
                    wallNX = frontPresent;
                    wallNZ = leftPresent;
                    wallPX = false;
                }
                if (direction == 2)
                {
                    SensorCheck();
                    wallPZ = false; //back is front
                    wallNX = rightPresent;
                    wallNZ = frontPresent;
                    wallPX = leftPresent;
                }
                if (direction == 3)
                {
                    SensorCheck();
                    wallPZ = leftPresent; //left is front
                    wallNX = false;
                    wallNZ = rightPresent;
                    wallPX = frontPresent;
                }

                WriteMapBit(posX, posZ, 0, wallPZ);
                WriteMapBit(posX, posZ, 1, wallNX);
                WriteMapBit(posX, posZ, 2, wallNZ);
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

            //if (posX == 28 && posZ == 23)
            //{
            //    FindPathTo(startPosX, startPosZ);
            //}

            Console.Write(posX + " , " + posZ + ": ");

            for (ushort i = 15; i >= 3; i--)
            {
                Console.Write(ReadMapBit(posX, posZ, i) ? "1" : "0");
            }
            Console.Write(ReadMapBit(posX, posZ, 2) ? "1" : "0");
            Console.Write(ReadMapBit(posX, posZ, 1) ? "1" : "0");
            Console.Write(ReadMapBit(posX, posZ, 0) ? "1" : "0");
            Console.WriteLine();
        }


        // ********************************** Checkpoints ********************************** 

        static void Reset()
        {
            try
            {
                string[] _checkpointXZ = sinceCheckpoint[0].Split(',');
                posX = int.Parse(_checkpointXZ[0]);
                posZ = int.Parse(_checkpointXZ[1]);
                currentMap = int.Parse(_checkpointXZ[2]);
                direction = int.Parse(_checkpointXZ[3]);

                for (int i = 0; i < sinceCheckpoint.Count; i++)
                {
                    string[] _coords = sinceCheckpoint[0].Split(',');
                    if (ReadMapBit(posX, posZ, blackTile))
                    {
                        maps[int.Parse(_coords[2])][int.Parse(_coords[0]), int.Parse(_coords[1])] = 0;
                        WriteMapBit(int.Parse(_coords[0]), int.Parse(_coords[1]), victim, true, int.Parse(_coords[2]));
                    }
                    else
                    {
                        maps[int.Parse(_coords[2])][int.Parse(_coords[0]), int.Parse(_coords[1])] = 0;
                    }
                    sinceCheckpoint.RemoveAt(0);
                }
            }
            catch
            {
                Console.WriteLine("Reset error");
            }
        }

        static void AddMapInfo(int _fromX, int _fromZ, int _rampDirection, int _map)
        {
            mapInfo.Add($"{_fromX},{_fromZ},{_rampDirection}");
        }

        static void AddSinceCheckPoint()
        {
            if (!ReadMapBit(posX, posZ, explored))
            {
                if (sinceCheckpoint.Count == 0)
                {
                    sinceCheckpoint.Add($"{posX},{posZ},{currentMap},{direction}");
                }
                else
                {
                    sinceCheckpoint.Add($"{posX},{posZ},{currentMap}");
                }
            }
            else if (sinceCheckpoint.Count == 0)
            {
                sinceCheckpoint.Add($"{posX},{posZ},{currentMap},{direction}");
            }
        }

        static void ResetSinceCheckpoint()
        {
            sinceCheckpoint.Clear();
        }


        // ********************************** Read And Write Map ********************************** 

        static bool ReadMapBit(int _x, int _z, int _read)
        {
            return ((maps[currentMap][_x, _z] >> _read) & 0b1) == 1;
        }

        static void WriteMapBit(int _x, int _z, int _write, bool _value)
        {
            ushort _t = (ushort)(0b1 << _write);
            if (_value && !ReadMapBit(0, 0, _write))
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
            if (_value && !ReadMapBit(0, 0, _write))
            {
                maps[_mapIndex][_x, _z] = (ushort)(maps[0][_x, _z] | (_t));
            }
            else
            {
                maps[_mapIndex][_x, _z] = (ushort)(maps[0][_x, _z] & ~_t);
            }
        }

        static void WriteInFront(int _x, int _z, int _write, bool value)
        {
            if (direction == 0)
            {
                WriteMapBit(_x, _z - 1, _write, value);
            }
            else if (direction == 1)
            {
                WriteMapBit(_x - 1, _z, _write, value);
            }
            else if (direction == 2)
            {
                WriteMapBit(_x, _z + 1, _write, value);
            }
            else if (direction == 3)
            {
                WriteMapBit(_x + 1, _z, _write, value);
            }
            else
            {
                Console.WriteLine("Direction error ");
            }
        }

        static bool ReadInFront(int _x, int _z, int _read)
        {
            if (direction == 0)
            {
                return ReadMapBit(_x, _z - 1, _read);
            }
            else if (direction == 1)
            {
                return ReadMapBit(_x - 1, _z, _read);
            }
            else if (direction == 2)
            {
                return ReadMapBit(_x, _z + 1, _read);
            }
            else if (direction == 3)
            {
                return ReadMapBit(_x + 1, _z, _read);
            }
            else
            {
                Console.WriteLine("Direction error ");
            }
            return false;
        }

        // ********************************** Map Navigation ********************************** 

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
            driveWay.ForEach(num => Console.WriteLine(num + " , "));

            for (int i = 0; i < maps[currentMap].GetLength(0); i++)
            {
                for (int j = 0; j < maps[currentMap].GetLength(1); j++)
                {
                    WriteMapBit(i, j, mapSearched, false);
                }
            }
        }

        static void GoBackLevel(int _map)
        {
            if (_map != currentMap)
            {
                toPosX = 25;
                toPosZ = 25;

                foundWay = false;
                FindExploredCells(posX, posZ);
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
                TurnTo(driveWay[0]);
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

        static void FindExploredCells(int _onX, int _onZ)
        {
            if (_onX == toPosX && _onZ == toPosZ)
            {
                foundWay = true;
            }
            Console.WriteLine("ok " + _onX + " " + _onZ);
            Console.WriteLine("found " + !foundWay);
            Console.WriteLine("mapbit " + !ReadMapBit(_onX, _onZ, 5));
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
    }
}
