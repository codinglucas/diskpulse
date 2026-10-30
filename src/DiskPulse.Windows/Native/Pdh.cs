using System.Runtime.InteropServices;

namespace DiskPulse.Windows.Native;

/// <summary>P/Invoke declarations for pdh.dll (Performance Data Helper).</summary>
internal static class Pdh
{
    public const uint ErrorSuccess = 0x00000000;
    public const uint PdhMoreData = 0x800007D2;
    public const uint PdhNoData = 0x800007D5;
    public const uint PdhCstatusValidData = 0x00000000;
    public const uint PdhCstatusNewData = 0x00000001;
    public const uint PdhCstatusNoObject = 0xC0000BB8;
    public const uint PdhCstatusNoCounter = 0xC0000BB9;

    public const uint PdhFmtDouble = 0x00000200;

    [DllImport("pdh.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    public static extern uint PdhOpenQueryW(string? dataSource, IntPtr userData, out IntPtr query);

    /// <summary>
    /// Adds a counter by its English path, whatever the system display language.
    /// Wildcards in the path are kept; they are expanded on each collection.
    /// </summary>
    [DllImport("pdh.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    public static extern uint PdhAddEnglishCounterW(IntPtr query, string fullCounterPath, IntPtr userData, out IntPtr counter);

    [DllImport("pdh.dll", ExactSpelling = true)]
    public static extern uint PdhCollectQueryData(IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    public static extern uint PdhGetFormattedCounterArrayW(
        IntPtr counter, uint format, ref uint bufferSize, out uint itemCount, IntPtr itemBuffer);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    public static extern uint PdhGetCounterInfoW(
        IntPtr counter, [MarshalAs(UnmanagedType.U1)] bool retrieveExplainText, ref uint bufferSize, IntPtr buffer);

    [DllImport("pdh.dll", ExactSpelling = true)]
    public static extern uint PdhCloseQuery(IntPtr query);

#pragma warning disable CS0649, CS0169 // Fields are filled in by the marshaller.

    /// <summary>PDH_FMT_COUNTERVALUE read with PDH_FMT_DOUBLE (the union's double member).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PdhFmtCounterValueDouble
    {
        public uint CStatus;
        public double DoubleValue;
    }

    /// <summary>PDH_FMT_COUNTERVALUE_ITEM_W.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PdhFmtCounterValueItemDouble
    {
        public IntPtr Name;
        public PdhFmtCounterValueDouble Value;
    }

    /// <summary>Leading fields of PDH_COUNTER_INFO_W, up to and including szFullPath.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PdhCounterInfoHeader
    {
        private uint _length;
        private uint _type;
        private uint _cVersion;
        private uint _cStatus;
        private int _scale;
        private int _defaultScale;
        private IntPtr _userData;
        private IntPtr _queryUserData;
        public IntPtr FullPath;
    }

#pragma warning restore CS0649, CS0169
}
