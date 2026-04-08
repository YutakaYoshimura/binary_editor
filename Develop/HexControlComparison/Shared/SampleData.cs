using System;
using System.Text;

namespace HexControlComparison
{
    // ─────────────────────────────────────────────
    //  EDS 風サンプルバイナリのレイアウト（64 bytes）
    // ─────────────────────────────────────────────
    //  オフセット | サイズ | 内容
    //  0x00       |  4     | Magic: "EDS\0"         (読み取り専用)
    //  0x04       |  2     | VersionMajor  (uint16 LE)
    //  0x06       |  2     | VersionMinor  (uint16 LE)
    //  0x08       |  4     | DeviceID      (uint32 LE)
    //  0x0C       |  4     | BaudRate      (uint32 LE)
    //  0x10       |  1     | NodeID        (uint8)
    //  0x11       |  4     | Reserved      (読み取り専用)
    //  0x15       | 16     | DeviceName    (ASCII)
    // ─────────────────────────────────────────────

    internal enum ParamType { Bytes, UInt8, UInt16LE, UInt32LE, AsciiString }

    internal class ParameterDef
    {
        public string    Name       { get; private set; }
        public int       Offset     { get; private set; }
        public int       Size       { get; private set; }
        public ParamType Type       { get; private set; }
        public bool      IsReadOnly { get; private set; }

        public ParameterDef(string name, int offset, int size, ParamType type)
        {
            Name       = name;
            Offset     = offset;
            Size       = size;
            Type       = type;
            IsReadOnly = (type == ParamType.Bytes);
        }

        public string TypeLabel
        {
            get
            {
                switch (Type)
                {
                    case ParamType.UInt8:       return "UInt8";
                    case ParamType.UInt16LE:    return "UInt16 LE";
                    case ParamType.UInt32LE:    return "UInt32 LE";
                    case ParamType.AsciiString: return "ASCII";
                    default:                    return "Bytes (読み取り専用)";
                }
            }
        }

        public string ReadRawBytes(byte[] data)
        {
            if (Offset + Size > data.Length) return "---";
            var sb = new StringBuilder();
            for (int i = 0; i < Size; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(data[Offset + i].ToString("X2"));
            }
            return sb.ToString();
        }
    }

    internal static class SampleData
    {
        public static readonly ParameterDef[] Parameters = new ParameterDef[]
        {
            new ParameterDef("Magic",        0x00, 4,  ParamType.Bytes),
            new ParameterDef("VersionMajor", 0x04, 2,  ParamType.UInt16LE),
            new ParameterDef("VersionMinor", 0x06, 2,  ParamType.UInt16LE),
            new ParameterDef("DeviceID",     0x08, 4,  ParamType.UInt32LE),
            new ParameterDef("BaudRate",     0x0C, 4,  ParamType.UInt32LE),
            new ParameterDef("NodeID",       0x10, 1,  ParamType.UInt8),
            new ParameterDef("Reserved",     0x11, 4,  ParamType.Bytes),
            new ParameterDef("DeviceName",   0x15, 16, ParamType.AsciiString),
        };

        public static byte[] Create()
        {
            byte[] d = new byte[64];
            d[0] = 0x45; d[1] = 0x44; d[2] = 0x53; d[3] = 0x00;   // "EDS\0"
            d[4] = 0x01; d[5] = 0x00;                               // VersionMajor = 1
            d[6] = 0x05; d[7] = 0x00;                               // VersionMinor = 5
            d[8] = 0x34; d[9] = 0x12; d[10] = 0x00; d[11] = 0x00;  // DeviceID = 0x1234
            d[12] = 0x90; d[13] = 0xD0; d[14] = 0x03; d[15] = 0x00; // BaudRate = 250000
            d[16] = 0x01;                                            // NodeID = 1
            byte[] name = Encoding.ASCII.GetBytes("DEVICE_01");
            Array.Copy(name, 0, d, 0x15, name.Length);
            return d;
        }
    }
}
