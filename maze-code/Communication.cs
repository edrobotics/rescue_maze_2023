using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using EnumCommand;

namespace SerialConsole
{
    internal partial class Program
    {
        enum TileFloors
        {
            Blue = 'b',
            Black = 's',
            CheckPoint = 'c',
            White = 'v',
            Ramp = '1'
        }

        enum SendCommands
        {
            [Command("!d")]
            Drive,
            [Command("!w")]
            SensorCheck,
            [Command("!k")]
            DropKits,
            [Command("!t")]
            Turn,
            [Command("!i")]
            Interrupt
        }

        enum RecivedCommands
        {
            [Command("!a")]
            Answer,
            [Command("!s")]
            Success,
            [Command("!l")]
            LOP,
            [Command("i")]
            Interrupt
        }

        //******** Communication ********

        static readonly SerialPort serialPort1 = new("/dev/ttyS0", 9600, Parity.None, 8, StopBits.One);
        static readonly TcpListener listener = new(System.Net.IPAddress.Any, 4242);
        static TcpClient client;

        //******** Rescue Kits ********

        static volatile bool dropKits = false;
        static volatile int dropAmount = 0;
        static volatile char dropSide = 'l';
        static volatile int kitsLeft = 12;

        #region VictimComm
        // ********************************** Socket server ********************************** 

        static void ServerLoop()
        {
            NetworkStream stream = client.GetStream();
            //StreamReader sr = new StreamReader(client.GetStream());
            //StreamWriter sw = new StreamWriter(client.GetStream());
            while (!exit)
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

                    if (_recivedData.Contains('k') && !dropKits) //Danger as these might be modified and read at the same time, which is why they are volatile
                    {
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
                    if (!client.Connected)
                    {
                        stream = listener.AcceptTcpClient().GetStream();
                    }
                }
                //listener.Stop();
            }
        }
        #endregion

        #region SerialComm
        // ********************************** Driving ********************************** 

        static void Drive(bool _checkRamps)
        {
            if (sinceCheckpoint.Count == 1) //Update checkpoint direction upon leaving
            {
                try
                {
                    Log($"Updating checkpoint direction, this tile is {(ReadHere(BitLocation.checkPointTile) ? "" : "not ")}checkp.", true);
                    sinceCheckpoint[0][3] = (byte)direction; 
                }
                catch (Exception e) 
                {
                    LogException(e); 
                    Log("-Could not update checkpoint direction", true);
                }
            }
            //Send command and recive data
            locationUpdated = false;
            CheckAndDropKits(true);

            if (reset)
                return;

            Log("Driving", true);

            //Raw, as drive has higher risk of failure and failures need to be handled differently to avoid getting stuck
            string _recived = SerialRaw(SendCommands.Drive.GetCommand(), true, true);

            //*** Interpret data ***

            try
            {
                if (_recived[0] != '!')
                {
                    _recived = _recived.Remove(0, _recived.IndexOf('!'));
                }

                if (!_recived.Contains(RecivedCommands.Answer.GetCommand()))
                {
                    throw new Exception("Wrong answer format - no a ");
                }
            }
            catch (Exception e) //Something is wrong, check sensors and try again or update map
            {
                Log($"{_recived}; Something went wrong", true);
                LogException(e);
                
                SensorCheck();
                Log("There is " + (frontPresent ? "" : "not ") + "a wall in front", true);
                Log($"(;frontPresent = {frontPresent})", true);
                if (!frontPresent && !ReadNextTo(BitLocation.blackTile, Directions.front))
                {
                    Log("Retrying", true);
                    Drive(_checkRamps);
                    return;
                }
                else
                {
                    Log("Updating map", true);
                    UpdateMapFull();
                    if (driveWay.Count > 0)
                    {
                        Log("Nav via driveWay failed");
                        byte[] toPos = driveWay.Last();
                        driveWay = new List<byte[]>(PathTo(toPos[0], toPos[1]));
                    }
                    return;
                }
            }

            FloorHandler(_recived, _checkRamps);

            Thread.Sleep(10);

            serialPort1.DiscardInBuffer();
            serialPort1.DiscardOutBuffer();

            Thread.Sleep(10);
        }

        static void FloorHandler(string _recived, bool _checkRamps)
        {
            try
            {
                Log($"Recived: {_recived}", false);
                string[] _driveInfo = _recived.Split(',');

                if (_driveInfo[2].Contains((char)TileFloors.Ramp) && _checkRamps && !_driveInfo[1].Contains((char)TileFloors.Black)) //Went up a ramp
                {
                    Log($"recived ramp: {_recived}", true);//,horisontell,vertikal
                    RampDriven(direction);

                    if (_driveInfo[1].Contains((char)TileFloors.CheckPoint))
                    {
                        WriteHere(BitLocation.checkPointTile, true);
                        Log("New Checkp", true);
                        VisitedCheckpoint();
                    }
                    else if (_driveInfo[1].Contains((char)TileFloors.Blue))
                    {
                        WriteHere(BitLocation.blueTile, true);
                    }
                }
                else
                {
                    if (_driveInfo[1].Contains((char)TileFloors.Black)) //Did not move a tile because of black tile
                    {
                        Log($"recived black tile: {_recived}", true);
                        if (locationUpdated)
                        {
                            for (int i = 0; i < 3; i++) Log("!!Direction updated already, bad!!", true);
                            if (direction == 0) posZ += 1;
                            if (direction == 1) posX += 1;
                            if (direction == 2) posZ -= 1;
                            if (direction == 3) posX -= 1;
                        }

                        WriteNextTo(BitLocation.blackTile, true, Directions.front);
                        WriteNextTo(BitLocation.explored, true, Directions.front);
                    }
                    else //Moved a tile
                    {

                        UpdateLocation();
                        UpdateMap();

                        if (_driveInfo[1].Contains((char)TileFloors.CheckPoint))
                        {
                            Log($"recived checkpoint: {_recived}", true);
                            WriteHere(BitLocation.checkPointTile, true);

                            VisitedCheckpoint();
                        }
                        else if (_driveInfo[1].Contains((char)TileFloors.Blue))
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
        }


        // ********************************** Turning ********************************** 

        static void Turn(char _direction)
        {
            if (reset)
                return;
            if (_direction == 'l')
            {
                Log("Turning left", true);
                SerialComm(SendCommands.Turn.GetCommand("l"), false, false); //turn left
                UpdateDirection(1);
            }
            else if (_direction == 'r')
            {
                Log("Turning right", true);
                SerialComm(SendCommands.Turn.GetCommand("r"), false, false); //turn right
                UpdateDirection(-1);
            }
            else
            {
                Log("Turn - WRONG DIRECTION", true);
            }
            //SensorCheck();
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
                    CheckAndDropKits(false);
                    if (_toDirection != direction)
                        Turn('r'); //turn right
                }
            }
            else
            {
                while (_toDirection != direction)
                {
                    CheckAndDropKits(false);
                    if (_toDirection != direction)
                        Turn('l'); //turn left
                }
            }
            //SensorCheck();
            CheckAndDropKits(true);
        }


        // ********************************** Serial Communication ********************************** 

        static void SensorCheck()
        {
            if (reset)
                return;
            Log("checking sensors", true);
            string _sensorInfo = SerialComm(SendCommands.SensorCheck.GetCommand(), false, false);
            Thread.Sleep(30);

            try
            {
                if (_sensorInfo.Contains(RecivedCommands.Answer.GetCommand()))
                {
                    //'0' = 00110000, '1' = 00110001
                    byte _sensorData = byte.Parse(_sensorInfo.Split(',')[1]);

                    rightPresent = (_sensorData & 0b1) == 1;
                    leftPresent = ((_sensorData >> 1) & 0b1) == 1;
                    frontPresent = ((_sensorData >> 2) & 0b1) == 1;
                    Log($"Sensor data: (front,left,right){(frontPresent ? "1" : "0") + (leftPresent ? "1" : "0") + (rightPresent ? "1" : "0")}", false);
                }
                else
                {
                    Thread.Sleep(10);
                    Log($"{_sensorInfo} - Sensorcheck error - incorrect format recived, trying again", true);
                    SensorCheck();
                }
            }
            catch (Exception e)
            {
                LogException(e);
                if (_sensorInfo.Contains(RecivedCommands.LOP.GetCommand()))
                {
                    return;
                }
                Log($"{_sensorInfo} - Sensorcheck error - incorrect format recived, trying again", true);
                SensorCheck();
            }
            Log($"left:{leftPresent}, front:{frontPresent}, right:{rightPresent}", true);
        }

        static void CheckAndDropKits(bool _turnBack)
        {
            if (reset)
                return;

            if (dropKits && !ReadHere(BitLocation.victim))
            {
                if (kitsLeft < dropAmount)
                    dropAmount = kitsLeft;
                Log($"Dropping {dropAmount} kits {dropSide}", true);

                string _recived = SerialComm(SendCommands.DropKits.GetCommand($"{dropAmount},{dropSide},{(_turnBack ? '1' : '0')}"), false, false);

                kitsLeft -= dropAmount;
                Log($"Recived: {_recived}", false);
                try
                {
                    if (_recived.Contains(RecivedCommands.Success.GetCommand())) //Normal kit dropping
                    {
                        WriteHere(BitLocation.victim, true);
                        if (!_turnBack) KitDirectionUpdate(dropSide);
                    }
                    else if (_recived.Contains(RecivedCommands.Answer.GetCommand())) //In interrupt
                    {
                        if (_recived.Split(',')[1].Contains('0')) //Did not drive step
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
                    if (_recived.Contains(RecivedCommands.LOP.GetCommand()))
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
                return RecivedCommands.LOP.GetCommand();
            Log("interrupting", true);
            string _recived = SerialComm(SendCommands.Interrupt.GetCommand(), false, false);
            Thread.Sleep(100);
            return _recived;
        }

        /// <summary>
        /// Sends a command serially and handles errors in answer
        /// </summary>
        /// <param name="_send">The command that should be sent</param>
        /// <param name="_doubleWait">If we should wait for a start in the beginning</param>
        /// <param name="_interruptable">If we should be able to interrupt the command to drop kits</param>
        /// <returns>The recived answer</returns>
        static string SerialComm(string _send, bool _doubleWait, bool _interruptable)
        {
            if (!_send.Contains('!')) return "";
        StartComm:
            if (reset)
                return RecivedCommands.LOP.GetCommand();

            string _recived = SerialRaw(_send, _doubleWait, _interruptable);
            
            try
            {
                if (_recived[0] != '!')
                {
                    _recived = _recived.Remove(0, _recived.IndexOf('!'));
                }

                if (!_recived.Contains(RecivedCommands.Success.GetCommand()) && !_recived.Contains(RecivedCommands.Answer.GetCommand()))
                {
                    throw new Exception("No a or s in answer");
                }
            }
            catch (Exception e)
            {
                LogException(e);
                Thread.Sleep(10);
                Log("Something went very wrong, retrying", true);
                goto StartComm;
            }

            serialPort1.DiscardInBuffer();
            serialPort1.DiscardOutBuffer();
            Thread.Sleep(20);
            return _recived;
        }

        static string SerialRaw(string _send, bool _doubleWait, bool _interruptable)
        {
            if (!_send.Contains('!')) return "";
            do //if it takes too long, we send again
            {
                Log($"Sending {_send}", false);
                serialPort1.WriteLine(_send);
                Thread.Sleep(10);

                for (int i = 0; i < 500; i++)//50 iterations gives ~1 s.
                {
                    Thread.Sleep(20);
                    if (serialPort1.BytesToRead != 0)
                    {
                        break;
                    }
                }
                Log("iteration done", false);
            } while (serialPort1.BytesToRead == 0);


            string _recived = serialPort1.ReadLine();

            if (_recived.Contains(RecivedCommands.LOP.GetCommand()))
            {
                Reset();
                return _recived;
            }

            //If the double wait, we wait again but with no time limit since we know we were heard.
            if (_doubleWait)
            {
                if (_interruptable) //If it should be interruptable, we do some extra things:
                {
                    string _interruptRec = "";
                    while (serialPort1.BytesToRead == 0)
                    {
                        Thread.Sleep(20);
                        if (dropKits && !maps[currentMap].ReadBit(posX, posZ, BitLocation.victim))
                        {
                            _interruptRec = Interrupt();
                            if (_interruptRec.Contains(RecivedCommands.Interrupt.GetCommand()))
                            {
                                CheckAndDropKits(true);
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
                        Thread.Sleep(1000); //DO NOT REMOVE, otherwise serial comm breaks
                    }
                    else
                    {
                        _recived = serialPort1.ReadLine();
                    }
                }
                else //If not, normal wait and recive
                {
                    while (serialPort1.BytesToRead == 0)
                    {
                        Thread.Sleep(20);
                    }

                    _recived = serialPort1.ReadLine();
                }

                if (_recived.Contains(RecivedCommands.LOP.GetCommand()))
                {
                    Reset();
                    return _recived;
                }
            }

            Log(_recived, false);
            return _recived;
        }
        #endregion
    }
}
