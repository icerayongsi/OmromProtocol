using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace AdientTorque
{
    static public class IsConnect
    {
        public static bool PLC { get; set; }
        public static bool Camera { get; set; }
    }

    internal class FINProtocolV3 : IDisposable
    {
        private readonly string _ipAddress;
        private readonly int _port;
        private UdpClient _client;
        private bool _isDisposed;

        private const int DEFAULT_TIMEOUT = 5000;

        private static FINProtocolV3 _instance;
        private static readonly object _lock = new object();

        public static FINProtocolV3 Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new FINProtocolV3();
                        }
                    }
                }
                return _instance;
            }
        }

        private FINProtocolV3() : this(OmromProtocol.Properties.Settings.Default.PLC_IP, int.Parse(OmromProtocol.Properties.Settings.Default.PLC_PORT)) { }

        public FINProtocolV3(string ipAddress, int port)
        {
            _ipAddress = ipAddress;
            _port = port;
            _client = new UdpClient();
            _isDisposed = false;
        }

        public byte[] SendFINSCommand(byte[] finsCommand, int timeoutMilliseconds = DEFAULT_TIMEOUT)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(FINProtocolV3));

            try
            {
                _client.Client.SendTimeout = timeoutMilliseconds;
                _client.Client.ReceiveTimeout = timeoutMilliseconds;

                _client.Connect(_ipAddress, _port);

                _client.Send(finsCommand, finsCommand.Length);

                IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

                bool hasTimedOut = false;
                Timer timeoutTimer = new Timer(_ =>
                {
                    hasTimedOut = true;
                    try { _client.Close(); } catch { /* Ignore errors */ }
                }, null, timeoutMilliseconds, Timeout.Infinite);

                try
                {
                    byte[] response = _client.Receive(ref remoteEndPoint);

                    IsConnect.PLC = true;
                    IsConnect.Camera = true;
                    return response;
                }
                catch (SocketException)
                {
                    if (hasTimedOut)
                    {
                        throw new TimeoutException($"Receiving response from PLC at {_ipAddress}:{_port} timed out after {timeoutMilliseconds}ms");
                    }
                    throw;
                }
                finally
                {
                    timeoutTimer.Dispose();

                    if (hasTimedOut)
                    {
                        _client = new UdpClient();
                    }
                }
            }
            catch (SocketException ex)
            {
                IsConnect.PLC = false;
                IsConnect.Camera = false;
                throw new Exception($"Failed to communicate with PLC at {_ipAddress}:{_port}. {ex.Message}", ex);
            }
            catch (TimeoutException)
            {
                IsConnect.PLC = false;
                IsConnect.Camera = false;
                _client = new UdpClient();
                throw;
            }
            catch (Exception ex)
            {
                IsConnect.PLC = false;
                IsConnect.Camera = false;
                _client = new UdpClient();
                throw new Exception($"Error communicating with PLC: {ex.Message}", ex);
            }
        }

        public bool TestConnection(int timeoutMilliseconds = 2000)
        {
            if (_isDisposed)
                return false;

            try
            {
                ReadData(0, 1, timeoutMilliseconds);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public byte[] CreateFINSReadCommand(ushort address, ushort itemsToRead)
        {
            byte[] command = new byte[18];

            // FINS Header
            command[0] = 0x80;  // ICF (Information Control Field): Use Gateway, Command Type
            command[1] = 0x00;  // RSV (Reserved): Not used
            command[2] = 0x02;  // GCT (Gateway Count): Number of gateways passed (2 in this case)
            command[3] = 0x00;  // DNA (Destination Network Address): Local network (0x00)
            command[4] = 0x00;  // DA1 (Destination Node Address): Node address (0x01)
            command[5] = 0x00;  // DA2 (Destination Unit Address): Unit address (0x00)
            command[6] = 0x00;  // SNA (Source Network Address): Local network (0x00)
            command[7] = 0x01;  // SA1 (Source Node Address): Node address (0x01)
            command[8] = 0x00;  // SA2 (Source Unit Address): Unit address (0x00)
            command[9] = 0x1a;  // SID (Service ID): Arbitrary value (0x1a)

            // FINS Command
            command[10] = 0x01; // MRC (Main Request Code): Memory Area Read (0x01)
            command[11] = 0x01; // SRC (Sub Request Code): Sub-command code (0x01)

            // Command Data
            command[12] = 0x82; // Memory Area Code: DM (Data Memory) (0x82)
            command[13] = (byte)(address >> 8);  // Beginning Address (High Byte)
            command[14] = (byte)(address & 0xFF); // Beginning Address (Low Byte)
            command[15] = 0x00; // Bit Position: 0 (Word access)
            command[16] = (byte)(itemsToRead >> 8); // Number of items to read (High Byte)
            command[17] = (byte)(itemsToRead & 0xFF); // Number of items to read (Low Byte)

            return command;
        }

        public byte[] ReadData(ushort address, ushort itemsToRead, int timeoutMilliseconds = DEFAULT_TIMEOUT)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(FINProtocolV3));

            try
            {
                byte[] command = CreateFINSReadCommand(address, itemsToRead);
                byte[] response = SendFINSCommand(command, timeoutMilliseconds);

                int dataLength = itemsToRead * 2;
                byte[] data = new byte[dataLength];

                if (response.Length >= 14 + dataLength)
                {
                    Array.Copy(response, 14, data, 0, dataLength);
                }
                else
                {
                    throw new InvalidOperationException($"Response size ({response.Length}) is too small for requested data ({dataLength} bytes)");
                }

                return data;
            }
            catch (Exception ex) when (!(ex is ObjectDisposedException || ex is InvalidOperationException || ex is TimeoutException))
            {
                throw new Exception($"Error reading data from PLC: {ex.Message}", ex);
            }
        }

        public byte[] CreateFINSWriteCommand(ushort address, byte[] data)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("Data cannot be null or empty", nameof(data));

            ushort itemsToWrite = (ushort)(data.Length / 2);
            if (data.Length % 2 != 0)
                throw new ArgumentException("Data length must be even (word-aligned)", nameof(data));

            byte[] command = new byte[18 + data.Length];

            // FINS Header
            command[0] = 0x80;  // ICF: Use Gateway, Command Type
            command[1] = 0x00;  // RSV: Not used
            command[2] = 0x02;  // GCT: Number of gateways passed
            command[3] = 0x00;  // DNA: Local network
            command[4] = 0x00;  // DA1: Node address
            command[5] = 0x00;  // DA2: Unit address
            command[6] = 0x00;  // SNA: Local network
            command[7] = 0x01;  // SA1: Node address
            command[8] = 0x00;  // SA2: Unit address
            command[9] = 0x1a;  // SID: Service ID

            // FINS Command
            command[10] = 0x01; // MRC: Memory Area Write
            command[11] = 0x02; // SRC: Write command

            // Command Data
            command[12] = 0x82; // Memory Area Code: DM (Data Memory)
            command[13] = (byte)(address >> 8);     // Beginning Address (High Byte)
            command[14] = (byte)(address & 0xFF);   // Beginning Address (Low Byte)
            command[15] = 0x00; // Bit Position: 0 (Word access)
            command[16] = (byte)(itemsToWrite >> 8);    // Number of items (High Byte)
            command[17] = (byte)(itemsToWrite & 0xFF);  // Number of items (Low Byte)

            Array.Copy(data, 0, command, 18, data.Length);

            return command;
        }

        public void WriteData(ushort address, byte[] data, int timeoutMilliseconds = DEFAULT_TIMEOUT)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(FINProtocolV3));

            try
            {
                byte[] command = CreateFINSWriteCommand(address, data);
                byte[] response = SendFINSCommand(command, timeoutMilliseconds);

                if (response.Length < 14)
                    throw new InvalidOperationException("Response from PLC is too short");

                if (response[12] != 0x00 || response[13] != 0x00)
                    throw new InvalidOperationException($"PLC returned error code: 0x{response[12]:X2}{response[13]:X2}");
            }
            catch (Exception ex) when (!(ex is ObjectDisposedException || ex is InvalidOperationException || ex is TimeoutException))
            {
                throw new Exception($"Error writing data to PLC: {ex.Message}", ex);
            }
        }

        public void WriteWord(ushort address, ushort value, int timeoutMilliseconds = DEFAULT_TIMEOUT)
        {
            try
            {
                byte[] data = new byte[2];
                data[0] = (byte)(value >> 8);
                data[1] = (byte)(value & 0xFF);
                WriteData(address, data, timeoutMilliseconds);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error writing data to PLC: {ex.Message}", ex);
            }

        }

        public void WriteWords(ushort startAddress, ushort[] values, int timeoutMilliseconds = DEFAULT_TIMEOUT)
        {
            if (values == null || values.Length == 0)
                throw new ArgumentException("Values array cannot be null or empty", nameof(values));

            byte[] data = new byte[values.Length * 2];
            for (int i = 0; i < values.Length; i++)
            {
                data[i * 2] = (byte)(values[i] >> 8);      // High byte
                data[i * 2 + 1] = (byte)(values[i] & 0xFF); // Low byte
            }
            WriteData(startAddress, data, timeoutMilliseconds);
        }

        public void WriteString(ushort startAddress, ushort endAddress, string text, bool reverse = false, int timeoutMilliseconds = DEFAULT_TIMEOUT)
        {
            if (string.IsNullOrEmpty(text))
                throw new ArgumentException("Text cannot be null or empty", nameof(text));

            if (endAddress < startAddress)
                throw new ArgumentException("End address must be greater than or equal to start address");

            ResetData(startAddress, endAddress, timeoutMilliseconds);

            int availableWords = endAddress - startAddress + 1;
            int maxLength = availableWords * 2;

            if (text.Length > maxLength) text = text.Substring(0, maxLength);
            if (text.Length % 2 == 1) text += "\0";

            byte[] stringBytes = System.Text.Encoding.ASCII.GetBytes(text);
            byte[] data = new byte[stringBytes.Length];

            if (reverse)
            {
                int lastIndex = stringBytes.Length - (stringBytes.Length % 2 == 0 ? 2 : 1);
                for (int i = 0; i < lastIndex; i += 2)
                {
                    data[i] = stringBytes[i + 1];
                    data[i + 1] = stringBytes[i];
                }

                if (stringBytes.Length % 2 == 1)
                {
                    data[stringBytes.Length - 1] = stringBytes[stringBytes.Length - 1];
                }
                else
                {
                    data[stringBytes.Length - 2] = stringBytes[stringBytes.Length - 1];
                    data[stringBytes.Length - 1] = stringBytes[stringBytes.Length - 2];
                }
            }
            else
            {
                Array.Copy(stringBytes, data, stringBytes.Length);
            }

            WriteData(startAddress, data, timeoutMilliseconds);
        }

        public void ResetData(ushort startAddress, ushort endAddress, int timeoutMilliseconds = DEFAULT_TIMEOUT)
        {
            if (endAddress < startAddress)
                throw new ArgumentException("End address must be greater than or equal to start address");

            int wordCount = endAddress - startAddress + 1;
            byte[] data = new byte[wordCount * 2];

            WriteData(startAddress, data, timeoutMilliseconds);
        }

        public void Disconnect()
        {
            if (!_isDisposed && _client != null)
            {
                _client.Close();
                _client = null;
                IsConnect.PLC = false;
                IsConnect.Camera = false;
            }
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                Disconnect();
                _isDisposed = true;
                GC.SuppressFinalize(this);
            }
        }

        ~FINProtocolV3()
        {
            Dispose();
        }
    }
}