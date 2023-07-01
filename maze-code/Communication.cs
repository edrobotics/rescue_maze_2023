// |||| NAVIGATION - Code for communication stuff and related ||||
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using EnumCommand;
using Mapping;

namespace SerialConsole
{
    internal partial class Program
    {
        #region Variables, enums and objects

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
            /// <summary>!d, turboSpeed('1'/'0')</summary>
            [Command("!d")]
            Drive = 'd',
            /// <summary>!w</summary>
            [Command("!w")]
            SensorCheck = 'w',
            ///<summary>!k, amount(int), side('l'/'r'), turnBack('1'/'0')</summary>
            [Command("!k")]
            DropKits = 'k',
            /// <summary>!t, direction('l'/'r')</summary>
            [Command("!t")]
            Turn = 't',
            /// <summary>!i </summary>
            [Command("!i")]
            Interrupt = 'i'
        }

        enum RecivedCommands
        {
            [Command("!a")]
            Answer = 'a',
            [Command("!s")]
            Success = 's',
            [Command("!l")]
            LOP = 'l',
            [Command(",i")]
            Interrupt = 'i',
            [Command("!c")]
            Cancelled = 'c',
            [Command("!f")]
            Failed = 'f',
            [Command(",c")]
            Calibration
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
        static int lastDropped = 0;
        #endregion vars, enums, objs

        #region VictimComm
        // ********************************** Socket server ********************************** 

        static void ServerLoop()
        {
            NetworkStream stream = client.GetStream();
            //StreamReader sr = new StreamReader(client.GetStream());
            //StreamWriter sw = new StreamWriter(client.GetStream());
            while (!exit)
            {
                Delay(10);
                try
                {
                    if (!client.Connected)
                    {
                        client = listener.AcceptTcpClient();
                        stream = client.GetStream();
                    }

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
                        client = listener.AcceptTcpClient();
                        stream = client.GetStream();
                    }
                }
                //listener.Stop();
            }
        }
        #endregion

        #region MotionComm
        // ********************************** Driving ********************************** 

        static void Drive(bool _checkRamps, bool _turboDrive)
        {
            if (ReadHere(BitLocation.checkPointTile)) //Update checkpoint direction upon leaving
            {
                try
                {
                    Log($"Updating checkpoint direction", true);
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
            CheckAndDropKits(true, true);

            if (reset)
                return;

            Log("Driving", true);

            //Raw, as drive has higher risk of failure and failures need to be handled differently to avoid getting stuck
            string _recived = SerialRaw(SendCommands.Drive.GetCommand(_turboDrive ? '1' : '0'), true, true);

            if (reset)
                return;

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
                if (_recived.Contains(RecivedCommands.LOP.GetCommand()))
                    return;
                Log($"{_recived}; Something went wrong", true);
                LogException(e);
                
                SensorCheck();
                if (reset) return;

                Log("There is " + (frontPresent ? "" : "not ") + "a wall in front", true);
                Log($"(frontPresent = {frontPresent})", true);
                if (!frontPresent && !ReadNextTo(BitLocation.blackTile, Directions.front))
                {
                    Log("Retrying", true);
                    Drive(_checkRamps, false);
                    return;
                }
                else
                {
                    Log("Updating map", true);
                    UpdateMapFull(true);
                    if (driveWay.Count > 0)
                    {
                        Log("Nav via driveWay failed", true);
                        //byte[] toPos = driveWay.Last();
                        //FindPathHere(toPos[0], toPos[1]);
                        driveWay.Clear(); //We will find new since crosstile was not removed and mapwayback will be updated
                    }
                    return;
                }
            }

            FloorHandler(_recived, _checkRamps);

            Delay(10);
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
                    RampDriven(direction, int.Parse(_driveInfo[3]), int.Parse(_driveInfo[4])); //Handles the ramp, creates a new map and recalculates position and height

                    UpdateMap();
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
                    }
                }

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
            catch (Exception e)
            {
                Log($"{_recived} -floor problem", true);
                LogException(e);
            }
        }


        // ********************************** Turning ********************************** 

        static void Turn(char _direction)
        {
            if (reset) return;
            int _startDir = direction;
            if (_direction == 'l')
            {
                Log("Turning left", true);
                SerialComm(SendCommands.Turn.GetCommand('l'), true, false); //turn left
                if (!reset)
                    UpdateDirection(1);
            }
            else if (_direction == 'r')
            {
                Log("Turning right", true);
                SerialComm(SendCommands.Turn.GetCommand('r'), true, false); //turn right
                if (!reset)
                    UpdateDirection(-1);
            }
            else
            {
                Log($"Turn - WRONG DIRECTION; {_direction}", true);
            }

#warning check this
            dropKits = CheckKitSides(dropSide, _startDir, _direction);
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
                    CheckAndDropKits(false, true);
                    if (_toDirection != direction)
                        Turn('r'); //turn right
                    if (reset) return;
                }
            }
            else
            {
                while (_toDirection != direction)
                {
                    CheckAndDropKits(false, true);
                    if (_toDirection != direction)
                        Turn('l'); //turn left
                    if (reset) return;
                }
            }
            CheckAndDropKits(true, true);
        }
        #endregion

        #region MiscComm
        // ********************************** Other Serial Commands ********************************** 

        static void SensorCheck()
        {
            if (reset) return;
            Log("checking sensors", true);
            string _sensorInfo = SerialComm(SendCommands.SensorCheck.GetCommand(), false, false);
            if (reset) return;

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
                    Delay(10);
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

        /// <summary>
        /// Checks if there are kits to drop, and if so, it drops kits
        /// </summary>
        /// <param name="_turnBack">Whether we should turn back again</param>
        static void CheckAndDropKits(bool _turnBack, bool _useWallSafeguard)
        {
            if (reset)
                return;

            if (dropKits && !ReadHere(BitLocation.victim))
            {
                if (!CheckKitSide(dropSide) && _useWallSafeguard) //If there is not a wall on the kit side
                {
                    dropKits = false;
                    return;
                }

                if (kitsLeft < dropAmount)
                    dropAmount = kitsLeft;
                lastDropped = dropAmount; //Save just in case vision sends a new
                Log($"Dropping {dropAmount} kits {dropSide}", true);

                string _recived = SerialComm(SendCommands.DropKits.GetCommand(dropAmount, dropSide, _turnBack ? '1' : '0'), true, false);
                if (reset)
                    return;

                kitsLeft -= lastDropped;
                Log($"Recived: {_recived}", false);
                try
                {
                    if (_recived.Contains(RecivedCommands.Success.GetCommand()))
                    {
                        WriteHere(BitLocation.victim, true);
                        if (!_turnBack && lastDropped > 0)
                        {
                            KitDirectionUpdate(dropSide);
                        }
                    }
                    else
                    {
                        throw new Exception("No success sent");
                    }
                }
                catch (Exception e)
                {
                    if (_recived.Contains(RecivedCommands.LOP.GetCommand()))
                    {
                        Log("reset: " + e.Message, true);
                        return;
                    }
                    LogException(e);
                    Log($"{_recived} --WRONG", true);
                }
            }
            dropKits = false;
        }

        static void DropInterrupt()
        {
            if (reset)
                return;

            if (dropKits && !ReadHere(BitLocation.victim))
            {
                if (!CheckKitSide(dropSide)) //If there is not a wall on the kit side
                {
                    dropKits = false;
                    return;
                }

                if (kitsLeft < dropAmount)
                    dropAmount = kitsLeft;
                Log($"Dropping {dropAmount} kits {dropSide} from interrupt", true);
                lastDropped = dropAmount; //Save just in case vision sends a new

                try
                {
                    serialPort1.WriteLine(SendCommands.DropKits.GetCommand(dropAmount, dropSide, '1'));
                    Log("Sending: " + SendCommands.DropKits.GetCommand(dropAmount, dropSide, '1'), false);
                    kitsLeft -= lastDropped;
                    WriteHere(BitLocation.victim, true);

                    Delay(5);
                }
                catch (Exception e)
                {
                    if (reset) return;
                    LogException(e);
                }
            }
            else if (dropKits)
            {
                Log("Already discovered victim here", false);
            }
            else
            {
                Log("NO DROPKIT IN DROP INTERRUPT", false);
                errors++;
            }
            dropKits = false;
        }

        static string Interrupt()
        {
            if (reset)
                return RecivedCommands.LOP.GetCommand();
            Log("interrupting", true);
            string _recived = SerialComm(SendCommands.Interrupt.GetCommand(), false, false);
            Log("interrupted", false);
            Delay(100, true);
            return _recived;
        }
        #endregion

        #region BaseComm
        // ********************************** Serial Communication ********************************** 

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
            if (reset) return RecivedCommands.LOP.GetCommand();

            string _recived = SerialRaw(_send, _doubleWait, _interruptable);
            if (reset) return RecivedCommands.LOP.GetCommand();

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
                Delay(10, true);
                Log("Something went very wrong, retrying", true);
                goto StartComm;
            }

            return _recived;
        }

        /// <summary>
        /// Sends a command serially and returns answer without modifications or checks except reset check
        /// </summary>
        /// <returns>recived message</returns>
        static string SerialRaw(string _send, bool _doubleWait, bool _interruptable)
        {
            if (!_send.Contains('!')) return "";

            do //if it takes too long, we send again
            {
                Log($"Sending {_send}", false);
                serialPort1.WriteLine(_send);
                Delay(10);

                for (int i = 0; i < 250; i++)//50 iterations gives ~1 s.
                {
                    Delay(20);
                    if (serialPort1.BytesToRead != 0)
                    {
                        break;
                    }
                }
            } while (serialPort1.BytesToRead == 0);

            string _recived = serialPort1.ReadLine();

            if (_recived.Contains(RecivedCommands.LOP.GetCommand()))
            {
                Reset();
                return _recived;
            }

            bool _checkDropAgain = false;
            //If the double wait, we wait again but with no time limit since we know we were heard.
            if (_doubleWait)
            {
                if (_interruptable) //If it should be interruptable, we do some extra things:
                {
                    string _interruptRec = "";
                    while (serialPort1.BytesToRead == 0)
                    {
                        Delay(20);
                        if (dropKits)
                        {
                            #region InterruptHandling
                            if (!maps[currentMap].ReadBit(posX, posZ, BitLocation.victim) && !_checkDropAgain)
                            {
                                if (CheckKitSide(dropSide)) //Wall on the kit side, we have not already tried
                                {
                                    _interruptRec = Interrupt();
                                    if (_interruptRec.Contains(RecivedCommands.Interrupt.GetCommand()) && !_interruptRec.Contains(RecivedCommands.Cancelled.GetCommand()) && !_interruptRec.Contains(RecivedCommands.Failed.GetCommand()))
                                    {
                                        try
                                        {
                                            if (_interruptRec.Split(',')[2].Contains('1'))//Drove step
                                            {
                                                if (!locationUpdated)
                                                {
                                                    UpdateLocation();
                                                    locationUpdated = true;
                                                }

                                                if (ReadHere(BitLocation.victim))
                                                {
                                                    Log("Returning from kit dropping, sending !w", false);
                                                    serialPort1.WriteLine("!w"); //Any command returns to drive
                                                }
                                                else
                                                {
                                                    if (ReadHere(BitLocation.explored) && CheckKitSide(dropSide))
                                                    {
                                                        DropInterrupt();
                                                        Log("Kit drop, moved step", false);
                                                    }
                                                    else
                                                    {
                                                        Log("Try kit dropping later", false);
                                                        _checkDropAgain = true;
                                                    }
                                                }
                                            }
                                            else if (_interruptRec.Split(',')[2].Contains('0'))//Did not drive step
                                            {
                                                DropInterrupt();
                                                Log("Kit drop, did not move step", false);
                                            }
                                            else
                                            {
                                                throw new Exception("No interrupt 1 or 0");
                                            }

                                            Delay(20);
                                        }
                                        catch (Exception e)
                                        {
                                            LogException(e);
                                        }
                                    }
                                    else
                                    {
                                        if (!_interruptRec.Contains(RecivedCommands.Failed.GetCommand()) && !_interruptRec.Contains(RecivedCommands.Cancelled.GetCommand()))
                                            break;
                                    }
                                }
                                else //No wall, but we might be on the next tile or something so we should check again when we are done
                                {
                                    _checkDropAgain = true;
                                }

                                if (_checkDropAgain)
                                serialPort1.WriteLine("!w"); //Any command returns to drive
                            }
                            dropKits = false;
                            #endregion Interrupthandling
                        }
                    }

                    if (serialPort1.BytesToRead == 0)
                    {
                        _recived = _interruptRec;
                        Delay(1000, true); //DO NOT REMOVE, otherwise serial comm breaks
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
                        Delay(20);
                    }

                    _recived = serialPort1.ReadLine();
                    //if (dropKits) Log("! _ ! _ ! _ FORGOT A KIT _ ! _ ! _ !", true);
                    //dropKits = false;
                }

                if (_checkDropAgain)
                {
                    SensorCheck();
                    if (reset) return RecivedCommands.LOP.GetCommand();

                    dropKits = true;
                    Log("Checking kit again, dropside checking", true);
                    switch (dropSide) //If there is a wall to where the kits are, drop kits
                    {
                        case 'l':
                            if (leftPresent)
                                CheckAndDropKits(true, false);
                            else
                                Log("Forgot kit l, no wall", true);
                            break;
                        case 'r':
                            if (rightPresent)
                                CheckAndDropKits(true, false);
                            else
                                Log("Forgot kit r, no wall", true);
                            break;
                        default:
                            Log("Error kit dir drop side", true);
                            break;
                    }
                    dropKits = false;
                }

                if (_recived.Contains(RecivedCommands.LOP.GetCommand()))
                {
                    Reset();
                }
            }

            Delay(10, true);
            Log($"recived:{_recived}", false);
            return _recived;
        }
        #endregion
    }
}
