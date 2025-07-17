using AdientTorque;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

namespace OmromProtocol
{
    internal class App
    {
        public static FINProtocolV3 PLC { get; set; }
    }

    public partial class Form1 : Form
    {
        private byte[] PLCData;

        public Form1()
        {
            InitializeComponent();

            Task.Run(() =>
            {
                string ipAddress = Properties.Settings.Default.PLC_IP;
                int port = int.Parse(Properties.Settings.Default.PLC_PORT);

                App.PLC?.Dispose();
                App.PLC = new FINProtocolV3(ipAddress, port);
                bool plcReconnected = App.PLC.TestConnection(3000);

                if (plcReconnected) IsConnect.PLC = true;
                else IsConnect.PLC = false;
            });

            System.Threading.Timer _ = new System.Threading.Timer(ReadData, null, 0, 5);
        }

        public enum FixturesSide
        {
            None,
            LH,
            RH
        }

        public static class PLC
        {
            public static bool Blink { get; set; }                          // D1000 0 = OFF, 1 = ON
            public static bool Camera { get; set; }                         // D1001 0 = OFF, 1 = ON
            public static int FixtureRun { get; set; }                      // D1002 1 = FIX 1 OB , 2 = FIX 2 IB , 3 = FIX 3 IB  , 4 = FIX 4 OB

            public static bool TriggerFixure1_OB { get; set; }              // D1006 0 = OFF, 1 = ON
            public static bool TriggerFixure1_IB { get; set; }              // D1007 0 = OFF, 1 = ON
            public static bool TriggerFixure2_OB { get; set; }              // D1009 0 = OFF, 1 = ON
            public static bool TriggerFixure2_IB { get; set; }              // D1008 0 = OFF, 1 = ON    

            // Float values
            public static float ToqueValueFixure1_OB { get; set; }           // D1010 = LOW , D1011 = HIGH
            public static float ToqueValueFixure1_IB { get; set; }          // D1012 = LOW , D1013 = HIGH
            public static float ToqueValueFixure2_OB { get; set; }          // D1014 = LOW , D1015 = HIGH
            public static float ToqueValueFixure2_IB { get; set; }          // D1016 = LOW , D1017 = HIGH
            public static float ToqueAngle { get; set; }                     // D1018 = LOW , D1019 = HIGH
            public static float CycleTime { get; set; }                     // D1020 = LOW , D1021 = HIGH

            public static int JobNoStanley { get; set; }                    // D1022 (0 - 99)
            public static int Print { get; set; }                           // D1023 0 = IDLE, 1 = PRINT LH, 2 = PRINT RH

            // NG
            public static bool TriggerNGFixture1_OB { get; set; } // D1024
            public static bool TriggerNGFixture1_IB { get; set; }  // D1025
            public static bool TriggerNGFixture2_OB { get; set; } // D1026
            public static bool TriggerNGFixture2_IB { get; set; } // D1027

            // Station 1 = LH , 2 = RH
            public static FixturesSide Fixture1_OB_SIDE { get; set; }       // D1030
            public static FixturesSide Fixture1_IB_SIDE { get; set; }       // D1031
            public static FixturesSide Fixture2_OB_SIDE { get; set; }       // D1032
            public static FixturesSide Fixture2_IB_SIDE { get; set; }       // D1033

            public static string Pathname { get; set; }                     // D1031 - D1048
        }

        private const int PLC_START_ADDRESS = 1000;
        private const int PLC_START_LIMIT_READ_ADDRESS = 200;

        private void ReadData(object state)
        {
            if (!IsConnect.PLC) return;
            Task.Run(() => PLCData = App.PLC.ReadData(PLC_START_ADDRESS, PLC_START_LIMIT_READ_ADDRESS));

            if (PLCData != null)
            {
                PLC.Blink = Utilty.ReadWord(PLCData, 0, true) == 1;
                PLC.Camera = Utilty.ReadWord(PLCData, 1, true) == 1;
                PLC.FixtureRun = Utilty.ReadWord(PLCData, 2, true);

                PLC.TriggerFixure1_OB = Utilty.ReadWord(PLCData, 6, true) == 1;
                PLC.TriggerFixure1_IB = Utilty.ReadWord(PLCData, 7, true) == 1;
                PLC.TriggerFixure2_OB = Utilty.ReadWord(PLCData, 8, true) == 1;
                PLC.TriggerFixure2_IB = Utilty.ReadWord(PLCData, 9, true) == 1;

                PLC.ToqueValueFixure1_OB = Utilty.ToFloat(PLCData, 11, 10);
                PLC.ToqueValueFixure1_IB = Utilty.ToFloat(PLCData, 13, 12);
                PLC.ToqueValueFixure2_OB = Utilty.ToFloat(PLCData, 15, 14);
                PLC.ToqueValueFixure2_IB = Utilty.ToFloat(PLCData, 17, 16);
                PLC.ToqueAngle = Utilty.ToFloat(PLCData, 19, 18);
                PLC.CycleTime = Utilty.ToFloat(PLCData, 21, 20);

                PLC.TriggerNGFixture1_OB = Utilty.ReadWord(PLCData, 24, true) == 1;
                PLC.TriggerNGFixture1_IB = Utilty.ReadWord(PLCData, 25, true) == 1;
                PLC.TriggerNGFixture2_OB = Utilty.ReadWord(PLCData, 26, true) == 1;
                PLC.TriggerNGFixture2_IB = Utilty.ReadWord(PLCData, 27, true) == 1;

                PLC.JobNoStanley = Utilty.ReadWord(PLCData, 22, true);
                PLC.Print = Utilty.ReadWord(PLCData, 23, true);

                PLC.Fixture1_OB_SIDE = Utilty.ReadWord(PLCData, 30, true) == 1 ? FixturesSide.LH : Utilty.ReadWord(PLCData, 30, true) == 2 ? FixturesSide.RH : FixturesSide.None;
                PLC.Fixture1_IB_SIDE = Utilty.ReadWord(PLCData, 31, true) == 1 ? FixturesSide.LH : Utilty.ReadWord(PLCData, 31, true) == 2 ? FixturesSide.RH : FixturesSide.None;
                PLC.Fixture2_OB_SIDE = Utilty.ReadWord(PLCData, 32, true) == 1 ? FixturesSide.LH : Utilty.ReadWord(PLCData, 32, true) == 2 ? FixturesSide.RH : FixturesSide.None;
                PLC.Fixture2_IB_SIDE = Utilty.ReadWord(PLCData, 33, true) == 1 ? FixturesSide.LH : Utilty.ReadWord(PLCData, 33, true) == 2 ? FixturesSide.RH : FixturesSide.None;
                PLC.Pathname = ReadText(PLCData, 34, 43, true);
            }
        }

        private string ReadText(byte[] data, int startIndex, int endIndex, bool reverse = false)
        {
            string result = string.Empty;

            startIndex *= 2;
            endIndex *= 2;
            for (int i = 0; i < endIndex; i++)
            {
                int num = startIndex + i * 2;
                string text = Utilty.ConvertHexToAscii(Utilty.ToWord(new byte[2]
                {
                    data[num + 1],
                    data[num]
                }, reverse));
                if (text != "\0" || text != "\0\0")
                {
                    result += text;
                }
            }

            string cleaned_s = Regex.Replace(result, @"[^\u0020-\u007E]|QB8", "");

            return cleaned_s;
        }
    }
}
