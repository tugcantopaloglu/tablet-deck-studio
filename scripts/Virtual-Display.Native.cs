// Read-only Windows CCD topology probe. No driver or display configuration writes.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace TabletDeck.Diagnostics
{
    public sealed class DisplayPath
    {
        public string SourceKey { get; set; }
        public string SourceName { get; set; }
        public string MonitorName { get; set; }
        public string MonitorDevicePath { get; set; }
        public string AdapterDevicePath { get; set; }
        public uint TargetId { get; set; }
        public uint OutputTechnology { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool HasSourceMode { get; set; }
        public bool SharesDesktopSource { get; set; }
        public bool IndirectOutputHint { get; set; }
    }

    public static class DisplayTopology
    {
        [StructLayout(LayoutKind.Sequential)] private struct Luid { public uint Low; public int High; }
        [StructLayout(LayoutKind.Sequential)] private struct Header { public uint Type, Size; public Luid Adapter; public uint Id; }
        [StructLayout(LayoutKind.Sequential)] private struct Source { public Luid Adapter; public uint Id, ModeIndex, Status; }
        [StructLayout(LayoutKind.Sequential)] private struct Target
        {
            public Luid Adapter;
            public uint Id, ModeIndex, Technology, Rotation, Scaling, RefreshNumerator, RefreshDenominator, Scanline;
            public int Available;
            public uint Status;
        }
        [StructLayout(LayoutKind.Sequential)] private struct Path { public Source Source; public Target Target; public uint Flags; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct SourceName
        {
            public Header Header;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name;
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct TargetName
        {
            public Header Header;
            public uint Flags, Technology;
            public ushort Manufacturer, Product;
            public uint Connector;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string Name;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DevicePath;
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct AdapterName
        {
            public Header Header;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DevicePath;
        }
        [DllImport("user32.dll")] private static extern int GetDisplayConfigBufferSizes(uint flags, out uint paths, out uint modes);
        [DllImport("user32.dll")] private static extern int QueryDisplayConfig(uint flags, ref uint paths, IntPtr pathData, ref uint modes, IntPtr modeData, IntPtr topology);
        [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")] private static extern int GetSourceName(ref SourceName value);
        [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")] private static extern int GetTargetName(ref TargetName value);
        [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")] private static extern int GetAdapterName(ref AdapterName value);

        public static DisplayPath[] ReadActive()
        {
            const uint activePaths = 2;
            const int modeSize = 64; // DISPLAYCONFIG_MODE_INFO (16-byte header, 48-byte union).
            int pathSize = Marshal.SizeOf(typeof(Path));
            if (pathSize != 72) throw new InvalidOperationException("Unexpected Windows CCD structure size.");
            for (int attempt = 0; attempt < 4; attempt++)
            {
                uint pathCount, modeCount;
                int status = GetDisplayConfigBufferSizes(activePaths, out pathCount, out modeCount);
                if (status != 0) throw new Win32Exception(status, "Cannot read the active Windows display topology.");
                if (pathCount > 1024 || modeCount > 8192) throw new InvalidOperationException("Windows returned an unreasonable display topology size.");
                IntPtr paths = Marshal.AllocHGlobal(Math.Max(1, checked((int)pathCount * pathSize)));
                IntPtr modes = IntPtr.Zero;
                try
                {
                    modes = Marshal.AllocHGlobal(Math.Max(1, checked((int)modeCount * modeSize)));
                    status = QueryDisplayConfig(activePaths, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
                    if (status == 122) continue; // Topology changed between the size query and data query.
                    if (status != 0) throw new Win32Exception(status, "Cannot read the active Windows display topology; run in the local interactive session.");
                    var result = new List<DisplayPath>();
                    for (int i = 0; i < pathCount; i++)
                    {
                        Path path = (Path)Marshal.PtrToStructure(IntPtr.Add(paths, i * pathSize), typeof(Path));
                        var source = new SourceName { Header = new Header { Type = 1, Size = (uint)Marshal.SizeOf(typeof(SourceName)), Adapter = path.Source.Adapter, Id = path.Source.Id } };
                        var target = new TargetName { Header = new Header { Type = 2, Size = (uint)Marshal.SizeOf(typeof(TargetName)), Adapter = path.Target.Adapter, Id = path.Target.Id } };
                        var adapter = new AdapterName { Header = new Header { Type = 4, Size = (uint)Marshal.SizeOf(typeof(AdapterName)), Adapter = path.Source.Adapter } };
                        int sourceStatus = GetSourceName(ref source);
                        int targetStatus = GetTargetName(ref target);
                        int adapterStatus = GetAdapterName(ref adapter);
                        var item = new DisplayPath
                        {
                            SourceKey = path.Source.Adapter.High.ToString("X8") + path.Source.Adapter.Low.ToString("X8") + ":" + path.Source.Id,
                            SourceName = sourceStatus == 0 ? source.Name : null,
                            MonitorName = targetStatus == 0 ? target.Name : null,
                            MonitorDevicePath = targetStatus == 0 ? target.DevicePath : null,
                            AdapterDevicePath = adapterStatus == 0 ? adapter.DevicePath : null,
                            TargetId = path.Target.Id,
                            OutputTechnology = path.Target.Technology,
                            IndirectOutputHint = path.Target.Technology == 16 || path.Target.Technology == 17
                        };
                        if (path.Source.ModeIndex < modeCount)
                        {
                            IntPtr mode = IntPtr.Add(modes, checked((int)path.Source.ModeIndex * modeSize));
                            if (Marshal.ReadInt32(mode) == 1) // DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE
                            {
                                item.Width = Marshal.ReadInt32(mode, 16);
                                item.Height = Marshal.ReadInt32(mode, 20);
                                item.X = Marshal.ReadInt32(mode, 28);
                                item.Y = Marshal.ReadInt32(mode, 32);
                                item.HasSourceMode = true;
                            }
                        }
                        result.Add(item);
                    }
                    foreach (DisplayPath item in result)
                    {
                        int count = 0;
                        foreach (DisplayPath other in result) if (other.SourceKey == item.SourceKey) count++;
                        item.SharesDesktopSource = count > 1;
                    }
                    return result.ToArray();
                }
                finally
                {
                    if (modes != IntPtr.Zero) Marshal.FreeHGlobal(modes);
                    Marshal.FreeHGlobal(paths);
                }
            }
            throw new InvalidOperationException("Windows display topology kept changing; retry once it is stable.");
        }
    }
}
