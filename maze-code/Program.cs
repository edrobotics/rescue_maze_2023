﻿using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Net.Sockets;
using System.Text;

namespace SerialConsole
{
    internal class Program
    {
        /*static byte[,] byteMap = new byte[50, 50];
        static int explored = 4;
        static int mapSearched = 5;
        static int toPosX;
        static int toPosZ;
        static int startPosX = 25;
        static int startPosZ = 25;
        static int posX = 25;
        static int posZ = 25;
        static bool foundWay = false;
        static bool shortenAvailable;
        static int shortenToX;
        static int shortenToZ;
        static List<int> driveWay = new List<int>();*/
        //ANOTHER LIST TO FIND SHORTEST PATH?

        static int direction = 0;

        static bool dropKits = false;
        static int dropAmount;

        static bool frontPresent;
        static bool leftPresent;
        static bool rightPresent;
        /*static bool wallPZ;
        static bool wallNX;
        static bool wallNZ;
        static bool wallPX;*/

        static SerialPort serialPort1 = new SerialPort("COM3", 9600, Parity.None, 8, StopBits.One); //Edit stopbits? //"/dev/ttyUSB0" for pi, "COM3" or "COM5" for testing
        static TcpListener listener = new TcpListener(System.Net.IPAddress.Any, 4242);
        static TcpClient client;

        static void Main(string[] args)
        {
            Thread.Sleep(100);

            StartUp();

            //Main loop
            while (true)
            {
                Turnlogic();
                //if (driveWay.Count == 0)
                //{
                //}
                //else
                //{
                //    TurnTo(driveWay[0]);
                //}
                Drive();
                Thread.Sleep(10);
            }
        }

        static void StartUp()
        {
            OpenPort:
            try
            {
                serialPort1.Open();
            }
            catch
            {
                Console.WriteLine("Cannot open port");
                Thread.Sleep(500);
                goto OpenPort;
            }
            listener.Start();
            Console.WriteLine("Waiting for connection...");
            client = listener.AcceptTcpClient();
            Console.WriteLine("Client accepted");

            Thread.Sleep(3000);
            serialPort1.DiscardInBuffer();
            serialPort1.DiscardOutBuffer();

            //SerialComm("!w");
            //Update map, set all map bits to 0
            //for (int i = 0; i < byteMap.GetLength(0); i++)
            //{
            //    for (int j = 0; j < byteMap.GetLength(1); j++)
            //    {
            //        byteMap[i, j] = 0b00000000;
            //    }
            //}

            //UpdateMap(true);
            Thread.Sleep(1000);
            Thread t = new Thread(ServerLoop);
            t.Start();
        }

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

                    byte[] buffer = new byte[1024];
                    stream.Read(buffer, 0, buffer.Length);
                    int recv = 0;

                    foreach (byte b in buffer)
                    {
                        if (b != 0)
                        {
                            recv++;
                        }
                    }

                    string recivedData = Encoding.UTF8.GetString(buffer, 0, recv);

                    if (recivedData.Contains("11") && !dropKits) //&& map bit is false?
                    {
                        //If dropkits - check how many and if its the same as the already activated one do nothing
                        dropKits = true;
                        Console.WriteLine("found 1");
                        dropAmount = 1;
                    }
                    if (recivedData.Contains("22") && !dropKits)
                    {
                        //If dropkits - check how many and if its the same as the already activated one do nothing
                        dropKits = true;
                        Console.WriteLine("found 2");
                        dropAmount = 2;
                    }
                    recivedData = "";
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

            if (dropKits)
            {
                DropKits();
            }
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
        }

        static void DropKits()
        {
            if (dropKits)
            {
                Console.WriteLine($"Dropping {dropAmount} kits");
                dropKits = false;
                SerialComm($"!k,{dropAmount}");
            }
        }

        static void Interrupt()
        {
            Console.WriteLine("interrupting");
            SerialComm("!i");
            Thread.Sleep(100);
        }
        
        static void Resume()
        {
            Console.WriteLine("resuming");
            Thread.Sleep(100);
            SerialComm("!r");
        }

        static void Drive()
        {
            Console.WriteLine("Driving");
            SerialComm("!d"); //Drive 30 cm
            Thread.Sleep(100);
            //UpdateLocation();
            //UpdateMap(false);
            //Console.WriteLine(posX + " , " + posZ);
            if (dropKits)
            {
                DropKits();
            }
        }

        static void SensorCheck()
        {
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

            if (dropKits)
            {
                DropKits();
            }
        }

        static string SerialComm(string _send)
        {
            serialPort1.WriteLine(_send);
            Thread.Sleep(100);

            while (serialPort1.BytesToRead == 0)
            {
                if (_send.Contains("!d") && dropKits)
                {
                    //Interrupt(); //Recive data to see if we have went more than half
                    DropKits();
                    //Resume();
                }
                Thread.Sleep(10);
            }
            //int _bytes = serialPort1.BytesToRead;
            //char[] _buffer = new char[_bytes];
            Thread.Sleep(100);
            string _buffer = serialPort1.ReadLine();

            if (_buffer[0] != '!')
            {
                try
                {
                    _buffer = _buffer.Remove(0, _buffer.IndexOf('!'));
                }
                catch
                {
                    Console.WriteLine("SerialComm error - incorrect format recived - no '!'");
                }
            }

            //Console.WriteLine(_buffer);
            while (_buffer.IndexOf(' ') != -1)
            {
                _buffer = _buffer.Remove(_buffer.IndexOf(' '), 1);
            }

            try
            {
                if (_buffer[1] != 's' && _buffer[1] != 'a')
                {
                    Console.WriteLine("Something went wrong, retrying");
                    Console.WriteLine(_buffer);
                    SerialComm(_send);
                }
            }
            catch
            {
                Console.WriteLine(_buffer);
                Console.WriteLine("Something went very wrong, retrying");
                SerialComm(_send);
            }

            serialPort1.DiscardInBuffer();
            serialPort1.DiscardOutBuffer();
            Thread.Sleep(100);
            return _buffer;
        }

        static void Turnlogic()
        {
            SensorCheck();
            while (!leftPresent || frontPresent)
            {
                if (!leftPresent)
                {
                    Turn('l');
                    Thread.Sleep(500);
                    Drive();
                }
                else if (frontPresent)
                {
                    Turn('r');
                    Thread.Sleep(100);
                }
                SensorCheck();
                Thread.Sleep(10);
            }
        }

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

        /*static void UpdateLocation() //ONLY USE AFTER YOU KNOW THERE IS A NEW CELL
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

            if (posX > byteMap.GetLength(0))
            {
                posX = byteMap.GetLength(0);
                Console.WriteLine("Error PosX");
            }
            if (posX < 0)
            {
                posX = 0;
                Console.WriteLine("Error posX");
            }
            if (posZ > byteMap.GetLength(1))
            {
                posZ = byteMap.GetLength(1);
                Console.WriteLine("Error PosZ");
            }
            if (posZ < 0)
            {
                posZ = 0;
                Console.WriteLine("Error posZ");
            }
        }

        static void UpdateMap(bool _backPresent)//Behind - Always false when driving normally, always(?) true at start, CHANGE IF DIMENSION 3 ON MAP IS CHANGED?
        {
            SensorCheck();

            if (!ReadMapBit(posX, posZ, explored)) // WASD, forward left back right
            {
                if (direction == 0)//something wrong?
                {
                    SensorCheck();
                    wallPZ = frontPresent; //front is front
                    wallNX = leftPresent;
                    wallNZ = _backPresent;
                    wallPX = rightPresent;
                }
                if (direction == 1)
                {
                    SensorCheck();
                    wallPZ = rightPresent; //right is front = Z
                    wallNX = frontPresent;
                    wallNZ = leftPresent;
                    wallPX = _backPresent;
                }
                if (direction == 2)
                {
                    SensorCheck();
                    wallPZ = _backPresent; //back is front
                    wallNX = rightPresent;
                    wallNZ = frontPresent;
                    wallPX = leftPresent;
                }
                if (direction == 3)
                {
                    SensorCheck();
                    wallPZ = leftPresent; //left is front
                    wallNX = _backPresent;
                    wallNZ = rightPresent;
                    wallPX = frontPresent;
                }

                SetMapBit(posX, posZ, 0b00000001, wallPZ);
                SetMapBit(posX, posZ, 0b00000010, wallNX);
                SetMapBit(posX, posZ, 0b00000100, wallNZ);
                SetMapBit(posX, posZ, 0b00001000, wallPX);
                SetMapBit(posX, posZ, 0b00010000, true);

                if (posZ - 1 >= 0 && !ReadMapBit(posX, posZ - 1, explored)) //If there is an unexplored position one +Z
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
                }
            }
            Console.WriteLine(posX + " , " + posZ + ": " + (ReadMapBit(posX, posZ, 0) ? "1" : "0") + (ReadMapBit(posX, posZ, 1) ? "1" : "0") + (ReadMapBit(posX, posZ, 2) ? "1" : "0") + (ReadMapBit(posX, posZ, 3) ? "1" : "0") + (ReadMapBit(posX, posZ, 4) ? "1" : "0") + (ReadMapBit(posX, posZ, 5) ? "1" : "0"));

            if (posX == 28 && posZ == 23)
            {
                FindPathTo(startPosX, startPosZ);
            }
        }

        static bool ReadMapBit(int _x, int _z, int read)
        {
            return ((byteMap[_x, _z] >> read) & 0b1) == 1;
        }

        static void SetMapBit(int _x, int _z, byte _bit, bool _value)
        {
            if (_value)
            {
                byteMap[_x, _z] = (byte)(byteMap[_x, _z] | _bit);
            }
            else
            {
                byteMap[_x, _z] = (byte)(byteMap[_x, _z] & ~_bit);
            }
        }

        static void FindPathTo(int _toX, int _toZ)
        {
            // Test each cell for options

            toPosX = _toX;
            toPosZ = _toZ;

            foundWay = false;
            FindExploredCells(posX, posZ);
            foundWay = false;
            driveWay.ForEach(num => Console.WriteLine(num + " , "));

            for (int i = 0; i < byteMap.GetLength(0); i++)
            {
                for (int j = 0; j < byteMap.GetLength(1); j++)
                {
                    SetMapBit(i, j, 0b00100000, false);
                }
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
                SetMapBit(_onX, _onZ, 0b00100000, true);

                //if (!foundWay && _onZ - 1 >= 0) //If there is a position one step +Z
                //{
                //    if (ReadMapBit(_onX, _onZ - 1, explored) && !ReadMapBit(_onX, _onZ - 1, mapSearched) && !ReadMapBit(_onX, _onZ, 0))
                //    {
                //        driveWay.Add(0);
                //        FindExploredCells(_onX, _onZ - 1);
                //        if (!foundWay)
                //            driveWay.RemoveAt(driveWay.Count - 1);
                //    }
                //}
                //if (!foundWay && _onZ + 1 < byteMap.GetLength(1)) //If there is a position one step -Z
                //{
                //    if (ReadMapBit(_onX, _onZ + 1, explored) && !ReadMapBit(_onX, _onZ + 1, mapSearched) && !ReadMapBit(_onX, _onZ, 2))
                //    {
                //        driveWay.Add(2);
                //        FindExploredCells(_onX, _onZ + 1);
                //        if (!foundWay)
                //            driveWay.RemoveAt(driveWay.Count - 1);
                //    }
                //}
                //if (!foundWay && _onX + 1 < byteMap.GetLength(0)) //If there is a position one step +X
                //{
                //    if (ReadMapBit(_onX + 1, _onZ, explored) && !ReadMapBit(_onX + 1, _onZ, mapSearched) && !ReadMapBit(_onX, _onZ, 3))
                //    {
                //        driveWay.Add(3);
                //        FindExploredCells(_onX + 1, _onZ);
                //        if (!foundWay)
                //            driveWay.RemoveAt(driveWay.Count - 1);
                //    }
                //}
                //if (!foundWay && _onX - 1 >= 0) //If there is a position one step -X
                //{
                //    Console.WriteLine(!ReadMapBit(_onX, _onZ, 1)); //THINKS ITS A WALL HERE

                //    if (ReadMapBit(_onX - 1, _onZ, explored) && !ReadMapBit(_onX - 1, _onZ, mapSearched) && !ReadMapBit(_onX, _onZ, 1))
                //    {
                //        driveWay.Add(1);
                //        FindExploredCells(_onX - 1, _onZ);
                //        if (!foundWay)
                //            driveWay.RemoveAt(driveWay.Count - 1);
                //    }
                //}

                if (_onZ > toPosZ)
                {
                    if (!foundWay && _onZ - 1 >= 0) //If there is a position one step +Z
                    {
                        if (ReadMapBit(_onX, _onZ - 1, explored) && !ReadMapBit(_onX, _onZ - 1, mapSearched) && !ReadMapBit(_onX, _onZ, 0))
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
                    if (!foundWay && _onZ + 1 < byteMap.GetLength(1)) //If there is a position one step -Z
                    {
                        if (ReadMapBit(_onX, _onZ + 1, explored) && !ReadMapBit(_onX, _onZ + 1, mapSearched) && !ReadMapBit(_onX, _onZ, 2))
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
                    if (!foundWay && _onX + 1 < byteMap.GetLength(0)) //If there is a position one step +X
                    {
                        if (ReadMapBit(_onX + 1, _onZ, explored) && !ReadMapBit(_onX + 1, _onZ, mapSearched) && !ReadMapBit(_onX, _onZ, 3))
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
                        if (ReadMapBit(_onX - 1, _onZ, explored) && !ReadMapBit(_onX - 1, _onZ, mapSearched) && !ReadMapBit(_onX, _onZ, 1))
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
                        if (ReadMapBit(_onX, _onZ - 1, explored) && !ReadMapBit(_onX, _onZ - 1, mapSearched) && !ReadMapBit(_onX, _onZ, 0))
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
                    if (!foundWay && _onZ + 1 < byteMap.GetLength(1)) //If there is a position one step -Z
                    {
                        if (ReadMapBit(_onX, _onZ + 1, explored) && !ReadMapBit(_onX, _onZ + 1, mapSearched) && !ReadMapBit(_onX, _onZ, 2))
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
                    if (!foundWay && _onX + 1 < byteMap.GetLength(0)) //If there is a position one step +X
                    {
                        if (ReadMapBit(_onX + 1, _onZ, explored) && !ReadMapBit(_onX + 1, _onZ, mapSearched) && !ReadMapBit(_onX, _onZ, 3))
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
                        if (ReadMapBit(_onX - 1, _onZ, explored) && !ReadMapBit(_onX - 1, _onZ, mapSearched) && !ReadMapBit(_onX, _onZ, 1))
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
                    SetMapBit(_onX, _onZ, 0b00100000, false);
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

                if (_onZ + 1 < byteMap.GetLength(1)) //If there is a position one step -Z
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

                if (_onX + 1 < byteMap.GetLength(0)) //If there is a position one step +X
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
        }*/
    }
}
