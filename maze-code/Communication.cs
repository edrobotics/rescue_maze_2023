using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SerialConsole
{
    internal partial class Program
    {
        //******** Communication ********

        static readonly SerialPort serialPort1 = new("/dev/ttyS0", 9600, Parity.None, 8, StopBits.One);
        static readonly TcpListener listener = new(System.Net.IPAddress.Any, 4242);
        static TcpClient client;

        //******** Rescue Kits ********

        static bool dropKits = false;
        static int dropAmount = 0;
        static char dropSide = 'l';
        static int kitsLeft = 12;

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
                    byte[] _buffer = new byte[128];
                    stream.Read(_buffer, 0, _buffer.Length);
                    int _recv = 0;

                    foreach (byte _b in _buffer)
                    {
                        if (_b != 0)
                        {
                            _recv++;
                        }
                    }

                    string _recivedData = Encoding.UTF8.GetString(_buffer, 0, _recv);

                    if (_recivedData.Contains('k') && !dropKits) //&& map bit is false?
                    {
                        //If dropkits - check how many and if its the same as the already activated one do nothing
                        dropKits = true;
                        dropAmount = int.Parse(_recivedData.Substring(_recivedData.IndexOf('k') + 1, 1));
                        dropSide = _recivedData[_recivedData.IndexOf('k') + 2];
                        Log($"_server_: found {dropAmount}", true);
                    }
                    _recivedData = "";
                    //sw.WriteLine("Done");
                    //sw.Flush();
                }
                catch (Exception e)
                {
                    LogException(e);
                    Log("_server_: Client failure, retrying...", true);
                }
                //listener.Stop();
            }
        }


        // ********************************** Driving ********************************** 

        static void Drive()
        {
            //Send command and recive data
            locationUpdated = false;
            CheckAndDropKits();

            if (reset)
                return;

            Log("Driving", true);

            string _recived;
            string _interruptRec = "";

            serialPort1.WriteLine("!d");
            Thread.Sleep(10);

            while (serialPort1.BytesToRead == 0)
            {
                Thread.Sleep(20);
            }

            if (serialPort1.ReadLine().Contains("!l"))
            {
                Reset();
                return;
            }

            serialPort1.DiscardInBuffer();
            Thread.Sleep(20);


            while (serialPort1.BytesToRead == 0)
            {
                Thread.Sleep(20);
                if (dropKits && !maps[currentMap].ReadBit(posX, posZ, BitLocation.victim))
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
                Thread.Sleep(1000); //DO NOT REMOVE
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

            //*** Interpret data ***

            try
            {
                if (_recived[0] != '!')
                {
                    _recived = _recived.Remove(0, _recived.IndexOf('!'));
                }

                if (_recived[1] != 'a')
                {
                    throw new Exception("Wrong answer format - no a ");
                }
            }
            catch (Exception e)
            {
                Log($"{_recived}; Something went wrong, retrying", true);
                LogException(e);
                
                SensorCheck();
                if (!frontPresent && !ReadNextTo(BitLocation.blackTile, Directions.front))
                {
                    Drive();
                    return;
                }
                else
                {
                    UpdateMapFull();
                    if (driveWay.Count > 0)
                    {
                        Log("Nav via driveWay failed");
                        byte[] toPos = driveWay[0];
                        driveWay = new List<byte[]>(PathTo(toPos[0], toPos[1]));
                    }
                    return;
                }
            }

            try
            {
                //    Console.Write("recived: ");
                //    Console.WriteLine(_recived); //!a,v,0
                string[] _driveInfo = _recived.Split(',');

                if (_driveInfo[2].Contains('1')) //Char instead
                {
                    Log($"recived ramp: {_recived}", true);//,horisontell,vertikal
                    RampDriven(direction);

                    if (_driveInfo[1].Contains('c'))
                    {
                        WriteHere(BitLocation.checkPointTile, true);
                        Log("New Checkp", true);
                        ResetSinceCheckpoint();

                        AddSinceCheckPoint();
                    }
                    else if (_driveInfo[1].Contains('b'))
                    {
                        WriteHere(BitLocation.blueTile, true);
                    }
                }
                else
                {
                    if (_driveInfo[1].Contains('s')) //Did not move a tile
                    {
                        Log($"recived black tile: {_recived}", true);

                        WriteNextTo(BitLocation.blackTile, true, Directions.front);
                        WriteNextTo(BitLocation.explored, true, Directions.front);
                    }
                    else //Moved a tile
                    {

                        UpdateLocation();
                        UpdateMap();

                        if (_driveInfo[1].Contains('c'))
                        {
                            Log($"recived checkpoint: {_recived}", true);
                            WriteHere(BitLocation.checkPointTile, true);

                            if (ReadHere(BitLocation.checkPointTile))
                            {
                                Log("New Checkp", true);
                                ResetSinceCheckpoint();
                            }
                            AddSinceCheckPoint();
                        }
                        else if (_driveInfo[1].Contains('b'))
                        {
                            Log($"recived blue tile: {_recived}", true);
                            WriteHere(BitLocation.blueTile, true);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log($"{_recived} -problem colour", true);
                LogException(e);
            }

            Thread.Sleep(10);

            serialPort1.DiscardInBuffer();
            serialPort1.DiscardOutBuffer();

            Thread.Sleep(10);

            //SensorCheck();
            CheckAndDropKits();
        }
        /*
                static void DriveDeaf()
                {
                    string _recived;

                    serialPort1.WriteLine("!d");
                    Thread.Sleep(100);

                    while (serialPort1.BytesToRead == 0)
                    {
                        Thread.Sleep(20);
                    }

                    if (serialPort1.ReadLine().Contains("!l"))
                    {
                        Reset();
                        return;
                    }

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
                            Log($"Something went wrong, retrying, {_recived}", true);
                            SensorCheck();
                            if (!frontPresent && !ReadNextTo(BitLocation.blackTile, Directions.front))
                            {
                                DriveDeaf();
                            }
                            else
                            {
                                return;
                            }
                        }
                    }
                    catch (Exception e)
                        {
                            LogException(e);
                        Log($"Something went very wrong, retrying; {_recived}", true);
                        SensorCheck();
                        if (!frontPresent && ReadNextTo(BitLocation.blackTile, Directions.front))
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

                    //SensorCheck();
                    CheckAndDropKits();
                }*/


        // ********************************** Turning ********************************** 

        static void Turn(char _direction)
        {
            if (reset)
                return;
            if (_direction == 'l')
            {
                Log("Turning left", true);
                SerialComm("!t,l"); //turn left
                UpdateDirection(1);
            }
            else if (_direction == 'r')
            {
                Log("Turning right", true);
                SerialComm("!t,r"); //turn right
                UpdateDirection(-1);
            }
            else
            {
                Log("Turn - WRONG DIRECTION", true);
            }
            //SensorCheck();
            CheckAndDropKits();
        }

        static void TurnTo(int _toDirection)
        {
            if (reset)
                return;
            Log($"Turn to {_toDirection}", true);
            _toDirection = FixDirection(_toDirection);

            if (FixDirection(direction - _toDirection) != 3)
            {
                while (_toDirection != direction)
                {
                    Turn('r'); //turn right
                }
            }
            else
            {
                while (_toDirection != direction)
                {
                    Turn('l'); //turn left
                }
            }
            //SensorCheck();
            CheckAndDropKits();
        }


        // ********************************** Serial Communication ********************************** 

        static void SensorCheck()
        {
            if (reset)
                return;
            Log("checking sensors", true);
            string _sensorInfo = SerialComm("!w");
            Thread.Sleep(30);

            try
            {
                if (_sensorInfo[1] == 'a')
                {
                    //'0' = 00110000, '1' = 00110001
                    byte _sensorData = byte.Parse(_sensorInfo.Split(',')[1]);

                    rightPresent = (_sensorData & 0b1) == 1;
                    leftPresent = ((_sensorData >> 1) & 0b1) == 1;
                    frontPresent = ((_sensorData >> 2) & 0b1) == 1;
                    Log($"Sensor data: (front,left,right){(frontPresent ? "1" : "0") + (leftPresent ? "1" : "0") + (rightPresent ? "1" : "0")}", true);
                }
                else
                {
                    Log($"{_sensorInfo} - Sensorcheck error - incorrect format recived, trying again", true);
                    SensorCheck();
                }
            }
            catch (Exception e)
            {
                LogException(e);
                if (_sensorInfo.Contains("!l"))
                {
                    return;
                }
                Log($"{_sensorInfo} - Sensorcheck error - incorrect format recived, trying again", true);
                SensorCheck();
            }
        }

        static void CheckAndDropKits()
        {
            if (reset)
                return;

            if (dropKits && !ReadHere(BitLocation.victim))
            {
                if (kitsLeft < dropAmount)
                    dropAmount = kitsLeft;
                Log($"Dropping {dropAmount} kits {dropSide}", true);
                string _recived = SerialComm($"!k,{dropAmount},{dropSide},0");
                kitsLeft -= dropAmount;
                Log($"Recived: {_recived}", false);
                try
                {
                    if (_recived.Contains("!s")) //Normal kit dropping
                    {
                        if (_recived[1] == 's')
                        {
                            WriteHere(BitLocation.victim, true);
                        }
                        else
                        {
                            Log($"{_recived} -problem with kit step", true);
                        }
                    }
                    else if (_recived.Contains("!a")) //In interrupt
                    {
                        if (_recived.Split(',')[1].Contains('0'))
                        {
                            WriteHere(BitLocation.victim, true);
                        }
                        else if (_recived.Split(',')[1].Contains('1'))//Drove step
                        {
                            if (!locationUpdated)
                            {
                                UpdateLocation();
                                locationUpdated = true;
                            }
                            WriteHere(BitLocation.victim, true);
                        }
                        else
                        {
                            Log($"{_recived} -problem with kit step", true);
                        }
                    }
                }
                catch (Exception e)
                {
                    LogException(e);
                    if (_recived.Contains("!l"))
                    {
                        return;
                    }
                    Log($"{_recived} --WRONG", true);
                }
                Thread.Sleep(20);
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
            Log("interrupting", true);
            string _recived = SerialComm("!i");
            Thread.Sleep(100);
            return _recived;
        }

        static string SerialComm(string _send)
        {
            if (reset)
                return "!l";

            serialPort1.WriteLine(_send);
            Thread.Sleep(10);

            while (serialPort1.BytesToRead == 0)
            {
                Thread.Sleep(20);
            }

            string _recived = serialPort1.ReadLine();

            if (_recived.Contains("!l"))
            {
                Reset();
                return _recived;
            }

            Log(_recived, false);
            try
            {
                if (_recived[0] != '!')
                {
                    _recived = _recived.Remove(0, _recived.IndexOf('!'));
                }

                if (_recived[1] != 's' && _recived[1] != 'a' && _recived[1] != 'l')
                {
                    Log("Something went wrong, retrying", true);
                    _recived = SerialComm(_send);
                }
            }
            catch (Exception e)
            {
                LogException(e);
                Log("Something went very wrong, retrying", true);
                _recived = SerialComm(_send);
            }

            serialPort1.DiscardInBuffer();
            serialPort1.DiscardOutBuffer();
            Thread.Sleep(20);
            return _recived;
        }

    }
}
