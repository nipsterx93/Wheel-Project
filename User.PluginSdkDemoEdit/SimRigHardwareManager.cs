// -------------------------------------------------------------------------

// FILE: SimRigHardwareManager.cs

// VERSION: Fix errori 17

// -------------------------------------------------------------------------

using System;

using System.IO.Ports;

using System.Linq;

using System.Threading.Tasks;

using SharpDX.DirectInput;



namespace SimRIG

{

    public class HardwareInputEventArgs : EventArgs

    {

        public string CommandType { get; set; }

        public string RawValue { get; set; }

        public int EncoderIndex { get; set; }

        public int DirectionOrState { get; set; }

    }



    /// <summary>

    /// Gestisce in totale isolamento le comunicazioni con le periferiche hardware del volante SimRig.

    /// Include la lettura delle schede seriali e del joystick via DirectInput.

    /// </summary>

    public class SimRigHardwareManager

    {

        private SerialPort _serialInput = null;

        private SerialPort _serialLeds = null;

        private string _inputBuffer = "";

        private readonly object _bufferLock = new object();



        // DirectInput

        private DirectInput _directInput;

        private Joystick _joystick;

        private bool _deviceAcquired = false;



        public bool[] RawButtons { get; private set; } = new bool[128];

        public int[] RawAxes { get; private set; } = new int[8];



        public bool IsInputConnected => _serialInput != null && _serialInput.IsOpen;

        public bool IsLedsConnected => _serialLeds != null && _serialLeds.IsOpen;

        public string InputPortName => IsInputConnected ? _serialInput.PortName : "N/A";

        public string LedsPortName => IsLedsConnected ? _serialLeds.PortName : "N/A";



        private const string SH_MODE_PREFIX = "WMODE:";

        private const string SH_MSG_PREFIX = "WMSG:";

        private const string SH_VAL_PREFIX = "WVAL:";

        private const string SH_IDX_PREFIX = "WIDX:";

        private const string SH_WENC_PREFIX = "WENC:";

        private const string SH_WPUSH_PREFIX = "WPUSH:";



        public event EventHandler<HardwareInputEventArgs> OnHardwareInputReceived;

        public event EventHandler OnHardwareConnected;



        public SimRigHardwareManager()

        {

            InitializeDirectInput();

        }



        // -------------------------------------------------------------------------

        // DIRECT INPUT (JOYSTICK)

        // -------------------------------------------------------------------------

        private void InitializeDirectInput()

        {

            try

            {

                _directInput = new DirectInput();

                // Cerca un device che contiene "Arduino" o "Leonardo" nel nome (l'ATmega32U4)

                var devices = _directInput.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AttachedOnly);

                var deviceInstance = devices.FirstOrDefault(d =>

                    d.InstanceName.IndexOf("Arduino", StringComparison.OrdinalIgnoreCase) >= 0 ||

                    d.InstanceName.IndexOf("Leonardo", StringComparison.OrdinalIgnoreCase) >= 0);



                if (deviceInstance != null)

                {

                    _joystick = new Joystick(_directInput, deviceInstance.InstanceGuid);

                    _joystick.SetCooperativeLevel(IntPtr.Zero, CooperativeLevel.Background | CooperativeLevel.NonExclusive);

                    _joystick.Acquire();

                    _deviceAcquired = true;

                }

            }

            catch { /* Ignora se non trovato, fallback gestito dal Polling */ }

        }



        /// <summary>

        /// Legge lo stato attuale dei bottoni e degli assi. Deve essere chiamato ad ogni frame (es. DataUpdate).

        /// </summary>

        public void PollJoystick()

        {

            if (!_deviceAcquired || _joystick == null) return;



            try

            {

                _joystick.Poll();

                var state = _joystick.GetCurrentState();



                for (int i = 0; i < state.Buttons.Length && i < RawButtons.Length; i++)

                {

                    RawButtons[i] = state.Buttons[i];

                }



                RawAxes[0] = state.X;

                RawAxes[1] = state.Y;

                RawAxes[2] = state.Z;

                RawAxes[3] = state.RotationZ;

                RawAxes[4] = state.RotationX;

                RawAxes[5] = state.RotationY;

            }

            catch

            {

                _deviceAcquired = false;

                try

                {

                    _joystick.Acquire();

                    _deviceAcquired = true;

                }

                catch { }

            }

        }



        // -------------------------------------------------------------------------

        // SERIAL PORTS DISCOVERY E CONNESSIONE

        // -------------------------------------------------------------------------

        public async Task RunDiscoveryAsync()

        {

            CloseSerialPorts();

            string[] ports = SerialPort.GetPortNames();



            foreach (string port in ports)

            {

                try

                {

                    using (SerialPort tempPort = new SerialPort(port, 115200))

                    {

                        tempPort.ReadTimeout = 500;

                        tempPort.WriteTimeout = 500;

                        tempPort.DtrEnable = true;

                        tempPort.RtsEnable = true;

                        tempPort.Open();



                        await Task.Delay(3000);

                        tempPort.DiscardInBuffer();

                        tempPort.DiscardOutBuffer();



                        tempPort.WriteLine("WHO");

                        await Task.Delay(200);



                        string response = tempPort.ReadExisting();

                        if (string.IsNullOrWhiteSpace(response))

                        {

                            tempPort.WriteLine("WHO");

                            await Task.Delay(200);

                            response = tempPort.ReadExisting();

                        }



                        if (response.Contains("ID:SIMRIG_INPUT"))

                        {

                            tempPort.Close();

                            await Task.Delay(200);

                            ConnectInputPort(port);

                        }

                        else if (response.Contains("ID:SIMRIG_LEDS"))

                        {

                            tempPort.Close();

                            await Task.Delay(200);

                            ConnectLedsPort(port);

                        }

                    }

                }

                catch { }

            }

        }



        private void ConnectInputPort(string portName)

        {

            try

            {

                _serialInput = new SerialPort(portName, 115200);

                _serialInput.DtrEnable = true;

                _serialInput.RtsEnable = true;

                _serialInput.Open();

                _serialInput.DataReceived += Input_DataReceived;



                Task.Delay(2000).ContinueWith(t => OnHardwareConnected?.Invoke(this, EventArgs.Empty));

            }

            catch { _serialInput = null; }

        }



        private void ConnectLedsPort(string portName)

        {

            try

            {

                _serialLeds = new SerialPort(portName, 115200);

                _serialLeds.DtrEnable = true;

                _serialLeds.RtsEnable = true;

                _serialLeds.Open();



                Task.Delay(2000).ContinueWith(t => OnHardwareConnected?.Invoke(this, EventArgs.Empty));

            }

            catch { _serialLeds = null; }

        }



        public void CloseSerialPorts()

        {

            if (_serialInput != null)

            {

                try

                {

                    if (_serialInput.IsOpen)

                    {

                        _serialInput.DataReceived -= Input_DataReceived;

                        _serialInput.Close();

                    }

                    _serialInput.Dispose();

                }

                catch { }

                _serialInput = null;

            }



            if (_serialLeds != null)

            {

                try

                {

                    if (_serialLeds.IsOpen) _serialLeds.Close();

                    _serialLeds.Dispose();

                }

                catch { }

                _serialLeds = null;

            }

        }



        public void Shutdown()

        {

            CloseSerialPorts();

            if (_joystick != null)

            {

                try

                {

                    _joystick.Unacquire();

                    _joystick.Dispose();

                }

                catch { }

            }

            if (_directInput != null) _directInput.Dispose();

        }



        // -------------------------------------------------------------------------

        // RICEZIONE DATI DALL'HARDWARE (INPUT SERIALE)

        // -------------------------------------------------------------------------

        private void Input_DataReceived(object sender, SerialDataReceivedEventArgs e)

        {

            if (_serialInput == null || !_serialInput.IsOpen) return;

            try

            {

                string data = _serialInput.ReadExisting();

                lock (_bufferLock) { _inputBuffer += data; }



                string line;

                while (ExtractLineFromInputBuffer(out line))

                {

                    ProcessInputLine(line);

                }

            }

            catch { }

        }



        private bool ExtractLineFromInputBuffer(out string line)

        {

            line = null;

            int newlineIndex = _inputBuffer.IndexOfAny(new char[] { '\n', '\r' });

            if (newlineIndex >= 0)

            {

                line = _inputBuffer.Substring(0, newlineIndex);

                _inputBuffer = _inputBuffer.Substring(newlineIndex + (_inputBuffer[newlineIndex] == '\r' && newlineIndex + 1 < _inputBuffer.Length && _inputBuffer[newlineIndex + 1] == '\n' ? 2 : 1));

                return true;

            }

            return false;

        }



        private void ProcessInputLine(string line)

        {

            line = line.Trim();

            if (string.IsNullOrEmpty(line)) return;



            HardwareInputEventArgs args = new HardwareInputEventArgs();



            if (line.StartsWith(SH_MODE_PREFIX))

            {

                args.CommandType = "MODE";

                args.RawValue = line.Substring(SH_MODE_PREFIX.Length).Trim();

            }

            else if (line.StartsWith(SH_MSG_PREFIX))

            {

                args.CommandType = "MSG";

                args.RawValue = line.Substring(SH_MSG_PREFIX.Length);

            }

            else if (line.StartsWith(SH_VAL_PREFIX))

            {

                args.CommandType = "VAL";

                args.RawValue = line.Substring(SH_VAL_PREFIX.Length);

            }

            else if (line.StartsWith(SH_IDX_PREFIX))

            {

                args.CommandType = "IDX";

                args.RawValue = line.Substring(SH_IDX_PREFIX.Length);

            }

            else if (line.StartsWith(SH_WENC_PREFIX))

            {

                args.CommandType = "ENC";

                string[] parts = line.Substring(SH_WENC_PREFIX.Length).Split(':');

                if (parts.Length == 2 && int.TryParse(parts[0], out int encIdx) && int.TryParse(parts[1], out int dir))

                {

                    args.EncoderIndex = encIdx;

                    args.DirectionOrState = dir;

                }

                else return;

            }

            else if (line.StartsWith(SH_WPUSH_PREFIX))

            {

                args.CommandType = "PUSH";

                string[] parts = line.Substring(SH_WPUSH_PREFIX.Length).Split(':');

                if (parts.Length == 2 && int.TryParse(parts[0], out int encIdx) && int.TryParse(parts[1], out int state))

                {

                    args.EncoderIndex = encIdx;

                    args.DirectionOrState = state;

                }

                else return;

            }

            else return;



            OnHardwareInputReceived?.Invoke(this, args);

        }



        // -------------------------------------------------------------------------

        // INVIO DATI VERSO L'HARDWARE

        // -------------------------------------------------------------------------

        public void SendRawLedCommand(string cmd)

        {

            if (IsLedsConnected) { try { _serialLeds.WriteLine(cmd); } catch { } }

        }



        public void SendInputSystemState(bool enableFuelSystem)

        {

            if (IsInputConnected) { try { _serialInput.WriteLine($"SYS:PIT:{(enableFuelSystem ? 1 : 0)}"); } catch { } }

        }



        public void SendEncoderMapping(int encIdx, int modeIdx)

        {

            if (IsInputConnected) { try { _serialInput.WriteLine($"M:{encIdx}:{modeIdx}"); } catch { } }

        }



        public void SyncInputChip()

        {

            if (IsInputConnected) { try { _serialInput.WriteLine("S"); } catch { } }

        }

    }

}

