using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

internal static class RoadRashInputFixLauncher
{
    static readonly string GameDirectory = AppDomain.CurrentDomain.BaseDirectory;
    const string GameExe = "RoadRash95.exe";
    const uint PreferredImageBase = 0x400000;
    const uint CodeCaveRva = 0x636DF;
    const int CodeCaveLength = 145;
    const uint PageExecuteReadWrite = 0x40;
    const uint CreateSuspended = 0x4;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct StartupInfo
    {
        public int cb;
        public string lpReserved;
        public string lpDesktop;
        public string lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars;
        public int dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct ProcessInfo
    {
        public IntPtr hProcess, hThread;
        public uint dwProcessId, dwThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct JoyInfoEx
    {
        public uint size, flags, x, y, z, r, u, v, buttons, buttonNumber, pov, reserved1, reserved2;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct ProcessBasicInformation32
    {
        public IntPtr Reserved1, PebBaseAddress, Reserved2, UniqueProcessId, Reserved3;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CreateProcess(string application, StringBuilder commandLine, IntPtr processAttributes,
        IntPtr threadAttributes, bool inheritHandles, uint creationFlags, IntPtr environment,
        string currentDirectory, ref StartupInfo startupInfo, out ProcessInfo processInfo);

    [DllImport("kernel32.dll", SetLastError = true)] static extern bool ReadProcessMemory(IntPtr process,
        IntPtr address, byte[] buffer, int size, out IntPtr read);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool WriteProcessMemory(IntPtr process,
        IntPtr address, byte[] buffer, int size, out IntPtr written);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool VirtualProtectEx(IntPtr process,
        IntPtr address, UIntPtr size, uint newProtection, out uint oldProtection);
    [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr VirtualAllocEx(IntPtr process,
        IntPtr address, UIntPtr size, uint allocationType, uint protection);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool FlushInstructionCache(IntPtr process,
        IntPtr address, UIntPtr size);
    [DllImport("kernel32.dll", SetLastError = true)] static extern uint ResumeThread(IntPtr thread);
    [DllImport("kernel32.dll", SetLastError = true)] static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
    [DllImport("user32.dll", EntryPoint = "PostMessageA", SetLastError = true)] static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("winmm.dll")] static extern uint joyGetNumDevs();
    [DllImport("winmm.dll")] static extern uint joyGetPosEx(uint id, ref JoyInfoEx info);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool TerminateProcess(IntPtr process, uint exitCode);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    [DllImport("ntdll.dll")] static extern int NtQueryInformationProcess(IntPtr process, int infoClass,
        ref IntPtr info, int infoLength, out int returnLength);
    [DllImport("ntdll.dll")] static extern int NtQueryInformationProcess(IntPtr process, int infoClass,
        out ProcessBasicInformation32 info, int infoLength, out int returnLength);

    static void Add(List<byte> b, params byte[] values) { b.AddRange(values); }
    static void AddU32(List<byte> b, uint value) { b.AddRange(BitConverter.GetBytes(value)); }
    static uint RemoteVa(uint imageBase, uint preferredVa) { return imageBase + preferredVa - PreferredImageBase; }
    static IntPtr Ptr(uint value) { return new IntPtr(unchecked((long)value)); }

    static byte[] NearJump(uint from, uint to, int span)
    {
        int displacement = unchecked((int)(to - (from + 5)));
        var b = new List<byte> { 0xE9 };
        b.AddRange(BitConverter.GetBytes(displacement));
        while (b.Count < span) b.Add(0x90);
        return b.ToArray();
    }

    static void SetRel8(List<byte> code, int displacementIndex, int targetIndex)
    {
        int delta = targetIndex - (displacementIndex + 1);
        if (delta < -128 || delta > 127) throw new InvalidOperationException("Short branch is out of range.");
        code[displacementIndex] = unchecked((byte)(sbyte)delta);
    }

    static byte[] BuildFilter(uint cave, uint imageBase)
    {
        var b = new List<byte>();
        Add(b, 0x81,0x7D,0x84,0x05,0x00,0x01,0x00, 0x74,0x09);
        Add(b, 0x81,0x7D,0x84,0x04,0x00,0x01,0x00, 0x75,0x0D);
        Add(b, 0x66,0x8B,0x45,0x8A, 0x66,0x8B,0x4D,0x88);
        b.AddRange(NearJump(cave + (uint)b.Count, RemoteVa(imageBase, 0x46364D), 5));
        b.AddRange(NearJump(cave + (uint)b.Count, RemoteVa(imageBase, 0x463688), 5));
        return b.ToArray();
    }

    static byte[] BuildDpad(uint cave, uint imageBase)
    {
        var b = new List<byte>();
        Add(b, 0x89,0x35); AddU32(b, RemoteVa(imageBase, 0x5CDAF0)); // preserve POV
        Add(b, 0x9C, 0x81,0xFE,0x28,0x23,0x00,0x00); // save flags; compare right
        int rightBranch = b.Count; Add(b, 0x74,0x00);
        Add(b, 0x81,0xFE,0x78,0x69,0x00,0x00); // compare left
        int leftBranch = b.Count; Add(b, 0x74,0x00);
        Add(b, 0x85,0xF6); // compare up = 0
        int upBranch = b.Count; Add(b, 0x74,0x00);
        Add(b, 0x81,0xFE,0x50,0x46,0x00,0x00); // compare down = 18000
        int downBranch = b.Count; Add(b, 0x74,0x00);
        Add(b, 0x9D); b.AddRange(NearJump(cave + (uint)b.Count, RemoteVa(imageBase, 0x463384), 5));
        int rightLabel = b.Count; Add(b, 0x9D,0xC7,0x05); AddU32(b, RemoteVa(imageBase, 0x5CDAD0)); Add(b, 0xFF,0xFF,0x00,0x00);
        b.AddRange(NearJump(cave + (uint)b.Count, RemoteVa(imageBase, 0x463384), 5));
        int leftLabel = b.Count; Add(b, 0x9D,0xC7,0x05); AddU32(b, RemoteVa(imageBase, 0x5CDAD0)); Add(b, 0x00,0x00,0x00,0x00);
        b.AddRange(NearJump(cave + (uint)b.Count, RemoteVa(imageBase, 0x463384), 5));
        int upLabel = b.Count; Add(b, 0x9D,0xC7,0x05); AddU32(b, RemoteVa(imageBase, 0x5CDAD4)); Add(b, 0x00,0x00,0x00,0x00);
        b.AddRange(NearJump(cave + (uint)b.Count, RemoteVa(imageBase, 0x463384), 5));
        int downLabel = b.Count; Add(b, 0x9D,0xC7,0x05); AddU32(b, RemoteVa(imageBase, 0x5CDAD4)); Add(b, 0xFF,0xFF,0x00,0x00);
        b.AddRange(NearJump(cave + (uint)b.Count, RemoteVa(imageBase, 0x463384), 5));
        SetRel8(b, rightBranch + 1, rightLabel);
        SetRel8(b, leftBranch + 1, leftLabel);
        SetRel8(b, upBranch + 1, upLabel);
        SetRel8(b, downBranch + 1, downLabel);
        return b.ToArray();
    }

    static byte[] BuildBluetooth(uint cave, uint imageBase)
    {
        var b = new List<byte>();
        Add(b, 0x56,0x8B,0xF1, 0x80,0x3E,0x01); // push esi; mov esi,ecx; USB ID
        int nonId1 = b.Count; Add(b, 0x75,0x00);
        // Bluetooth minimal ID 1 uses the compact layout, not the DualSense USB layout.
        Add(b, 0x83,0x3D); AddU32(b, RemoteVa(imageBase, 0x5CDA24)); Add(b,0x02);
        int otherType = b.Count; Add(b, 0x75,0x00);
        Add(b, 0x66,0x83,0x3D); AddU32(b, RemoteVa(imageBase, 0x5CDA28)); Add(b,0x4E);
        int notBluetooth = b.Count; Add(b, 0x75,0x00);
        Add(b, 0x53,0xB8,0x01,0x00,0x00,0x00); // push ebx; mov eax,1
        b.AddRange(NearJump(cave + (uint)b.Count, RemoteVa(imageBase, 0x46378B), 5));
        int standard = b.Count; b.AddRange(NearJump(cave + (uint)b.Count, RemoteVa(imageBase, 0x46377C), 5));
        int otherId = b.Count;
        Add(b, 0x80,0x3E,0x31); int rejectId = b.Count; Add(b, 0x75,0x00);
        Add(b, 0x83,0x3D); AddU32(b, RemoteVa(imageBase, 0x5CDA24)); Add(b,0x02);
        int rejectType = b.Count; Add(b, 0x75,0x00);
        Add(b, 0x46); b.AddRange(NearJump(cave + (uint)b.Count, RemoteVa(imageBase, 0x46377C), 5));
        int reject = b.Count; Add(b,0x5E,0xC3);
        SetRel8(b, nonId1 + 1, otherId);
        SetRel8(b, otherType + 1, standard);
        SetRel8(b, notBluetooth + 1, standard);
        SetRel8(b, rejectId + 1, reject);
        SetRel8(b, rejectType + 1, reject);
        return b.ToArray();
    }

    static uint GetImageBase(IntPtr process)
    {
        if (IntPtr.Size == 8)
        {
            IntPtr peb32 = IntPtr.Zero; int returned;
            int status = NtQueryInformationProcess(process, 26, ref peb32, IntPtr.Size, out returned);
            if (status != 0 || peb32 == IntPtr.Zero) throw new InvalidOperationException("Could not read the 32-bit game PEB.");
            byte[] baseBytes = new byte[4]; IntPtr read;
            if (!ReadProcessMemory(process, IntPtr.Add(peb32, 8), baseBytes, 4, out read) || read.ToInt32() != 4)
                throw new InvalidOperationException("Could not read the game's image base.");
            return BitConverter.ToUInt32(baseBytes, 0);
        }
        else
        {
            ProcessBasicInformation32 pbi; int returned;
            int status = NtQueryInformationProcess(process, 0, out pbi, Marshal.SizeOf(typeof(ProcessBasicInformation32)), out returned);
            if (status != 0 || pbi.PebBaseAddress == IntPtr.Zero) throw new InvalidOperationException("Could not read the game PEB.");
            byte[] baseBytes = new byte[4]; IntPtr read;
            if (!ReadProcessMemory(process, IntPtr.Add(pbi.PebBaseAddress, 8), baseBytes, 4, out read) || read.ToInt32() != 4)
                throw new InvalidOperationException("Could not read the game's image base.");
            return BitConverter.ToUInt32(baseBytes, 0);
        }
    }

    static byte[] Read(IntPtr process, uint address, int size)
    {
        byte[] data = new byte[size]; IntPtr count;
        if (!ReadProcessMemory(process, Ptr(address), data, size, out count) || count.ToInt32() != size)
            throw new InvalidOperationException("Could not read the suspended game image.");
        return data;
    }

    static void Write(IntPtr process, uint address, byte[] data)
    {
        IntPtr count;
        if (!WriteProcessMemory(process, Ptr(address), data, data.Length, out count) || count.ToInt32() != data.Length)
            throw new InvalidOperationException("Could not write the in-memory input patch.");
    }

    static void Apply(IntPtr process, uint imageBase)
    {
        uint cave = RemoteVa(imageBase, CodeCaveRva + PreferredImageBase);
        byte[] filter = BuildFilter(cave, imageBase);
        uint dpad = cave + (uint)filter.Length;
        byte[] dpadCode = BuildDpad(dpad, imageBase);
        IntPtr allocation = VirtualAllocEx(process, IntPtr.Zero, new UIntPtr(0x1000), 0x3000, PageExecuteReadWrite);
        if (allocation == IntPtr.Zero || allocation.ToInt64() > UInt32.MaxValue)
            throw new InvalidOperationException("Could not allocate the input patch in the game process.");
        uint bt = (uint)allocation.ToInt64();
        byte[] btCode = BuildBluetooth(bt, imageBase);
        var code = new List<byte>(); code.AddRange(filter); code.AddRange(dpadCode);
        if (code.Count > CodeCaveLength) throw new InvalidOperationException("In-memory code cave is too small.");

        var changes = new List<Tuple<uint, byte[], byte[]>>();
        uint siteFilter = RemoteVa(imageBase, 0x463645), siteDpad = RemoteVa(imageBase, 0x46337E), siteBt = RemoteVa(imageBase, 0x463770);
        byte[] oldFilter = Hex("66 8B 45 8A 66 8B 4D 88");
        var relocatedDpad = new List<byte> { 0x89, 0x35 };
        AddU32(relocatedDpad, RemoteVa(imageBase, 0x5CDAF0));
        byte[] oldDpad = relocatedDpad.ToArray();
        byte[] oldBt = Hex("56 8B F1 80 3E 01 0F 85 48 01 00 00");
        changes.Add(Tuple.Create(siteFilter, oldFilter, NearJump(siteFilter, cave, oldFilter.Length)));
        changes.Add(Tuple.Create(siteDpad, oldDpad, NearJump(siteDpad, dpad, oldDpad.Length)));
        changes.Add(Tuple.Create(siteBt, oldBt, NearJump(siteBt, bt, oldBt.Length)));
        // Force the game's own ClipCursor(NULL) path, including normal cleanup.
        uint siteCursor = RemoteVa(imageBase, 0x46E2E0);
        changes.Add(Tuple.Create(siteCursor, Hex("85 C9 74 64"), Hex("31 C9 74 64")));
        // On WM_ACTIVATEAPP(false), skip only ShowWindow(hwnd, SW_MINIMIZE).
        uint siteDeactivate = RemoteVa(imageBase, 0x43DEEF);
        changes.Add(Tuple.Create(siteDeactivate, Hex("6A 06 57 FF 15"), Hex("EB 07 57 FF 15")));
        foreach (var c in changes) if (!Equal(Read(process, c.Item1, c.Item2.Length), c.Item2))
            throw new InvalidOperationException("Game code signature mismatch at RVA 0x" + (c.Item1 - imageBase).ToString("X") + "; patch was not applied.");
        byte[] padding = Read(process, cave, CodeCaveLength);
        foreach (byte value in padding) if (value != 0xCC) throw new InvalidOperationException("Expected code padding changed; patch was not applied.");

        uint page = (RemoteVa(imageBase, 0x463000) & 0xFFFFF000u);
        uint oldProtect;
        if (!VirtualProtectEx(process, Ptr(page), new UIntPtr(0x1000), PageExecuteReadWrite, out oldProtect))
            throw new InvalidOperationException("Could not make the game code writable.");
        uint cursorPage = RemoteVa(imageBase, 0x46E000) & 0xFFFFF000u;
        uint cursorOldProtect;
        if (!VirtualProtectEx(process, Ptr(cursorPage), new UIntPtr(0x1000), PageExecuteReadWrite, out cursorOldProtect))
        {
            uint ignored; VirtualProtectEx(process, Ptr(page), new UIntPtr(0x1000), oldProtect, out ignored);
            throw new InvalidOperationException("Could not make the cursor code writable.");
        }
        uint focusPage = RemoteVa(imageBase, 0x43D000) & 0xFFFFF000u;
        uint focusOldProtect;
        if (!VirtualProtectEx(process, Ptr(focusPage), new UIntPtr(0x1000), PageExecuteReadWrite, out focusOldProtect))
        {
            uint ignored;
            VirtualProtectEx(process, Ptr(cursorPage), new UIntPtr(0x1000), cursorOldProtect, out ignored);
            VirtualProtectEx(process, Ptr(page), new UIntPtr(0x1000), oldProtect, out ignored);
            throw new InvalidOperationException("Could not make the window focus code writable.");
        }
        try
        {
            Write(process, bt, btCode);
            Write(process, cave, code.ToArray());
            foreach (var c in changes) Write(process, c.Item1, c.Item3);
            if (!FlushInstructionCache(process, Ptr(page), new UIntPtr(0x1000)))
                throw new InvalidOperationException("Could not flush the game instruction cache.");
            if (!FlushInstructionCache(process, Ptr(bt), new UIntPtr((uint)btCode.Length)))
                throw new InvalidOperationException("Could not flush the input patch instruction cache.");
            if (!FlushInstructionCache(process, Ptr(cursorPage), new UIntPtr(0x1000)))
                throw new InvalidOperationException("Could not flush the cursor patch instruction cache.");
            if (!FlushInstructionCache(process, Ptr(focusPage), new UIntPtr(0x1000)))
                throw new InvalidOperationException("Could not flush the focus patch instruction cache.");
        }
        catch
        {
            foreach (var c in changes) { try { Write(process, c.Item1, c.Item2); } catch { } }
            throw;
        }
        finally {
            uint ignored;
            VirtualProtectEx(process, Ptr(focusPage), new UIntPtr(0x1000), focusOldProtect, out ignored);
            VirtualProtectEx(process, Ptr(cursorPage), new UIntPtr(0x1000), cursorOldProtect, out ignored);
            VirtualProtectEx(process, Ptr(page), new UIntPtr(0x1000), oldProtect, out ignored);
        }
    }

    static byte[] Hex(string text)
    {
        string[] parts = text.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        byte[] b = new byte[parts.Length]; for (int i=0;i<parts.Length;i++) b[i]=Convert.ToByte(parts[i],16); return b;
    }
    static bool Equal(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false; for (int i=0;i<a.Length;i++) if (a[i]!=b[i]) return false; return true;
    }

    // The game's video loop waits for messages and may not see a native DualSense.
    // Wake it with WM_NULL; on a fresh joystick button press, post Enter only to
    // this game's foreground video window. Buttons already held must be released.
    static void WakeVideoLoop(IntPtr process, uint imageBase, uint gameProcessId)
    {
        try
        {
            const int maxDevices = 16;
            var known = new bool[maxDevices];
            var previous = new uint[maxDevices];
            var released = new uint[maxDevices];
            uint lastVideo = 0;
            bool skipSent = false;
            while (WaitForSingleObject(process, 50) == 0x102)
            {
                byte[] b;
                try { b = Read(process, RemoteVa(imageBase, 0x5A68E4), 4); }
                catch { return; }
                uint window = BitConverter.ToUInt32(b, 0);
                if (window != lastVideo)
                {
                    Array.Clear(known, 0, known.Length);
                    Array.Clear(previous, 0, previous.Length);
                    Array.Clear(released, 0, released.Length);
                    skipSent = false;
                    lastVideo = window;
                }
                if (window == 0) continue;
                uint videoPid;
                if (GetWindowThreadProcessId(Ptr(window), out videoPid) == 0 || videoPid != gameProcessId) continue;
                PostMessage(Ptr(window), 0, IntPtr.Zero, IntPtr.Zero);
                uint foregroundPid;
                if (GetWindowThreadProcessId(GetForegroundWindow(), out foregroundPid) == 0 || foregroundPid != gameProcessId)
                {
                    Array.Clear(known, 0, known.Length);
                    continue;
                }
                if (skipSent) continue;
                uint count = Math.Min(joyGetNumDevs(), (uint)maxDevices);
                for (uint i = 0; i < count; ++i)
                {
                    var info = new JoyInfoEx();
                    info.size = (uint)Marshal.SizeOf(typeof(JoyInfoEx));
                    info.flags = 0xFF;
                    if (joyGetPosEx(i, ref info) != 0) { known[i] = false; continue; }
                    if (!known[i])
                    {
                        previous[i] = info.buttons;
                        released[i] = ~info.buttons;
                        known[i] = true;
                        continue;
                    }
                    released[i] |= ~info.buttons;
                    uint pressed = info.buttons & ~previous[i] & released[i];
                    previous[i] = info.buttons;
                    if (pressed != 0 && PostMessage(Ptr(window), 0x100, new IntPtr(13), IntPtr.Zero))
                    {
                        skipSent = true;
                        break;
                    }
                }
            }
        }
        catch { /* The game continues normally if the helper cannot monitor it. */ }
    }

    [STAThread]
    static void Main()
    {
        string exe = Path.Combine(GameDirectory, GameExe);
        ProcessInfo pi = new ProcessInfo();
        try
        {
            if (!File.Exists(exe)) throw new FileNotFoundException("Road Rash was not found.", exe);
            StartupInfo si = new StartupInfo(); si.cb = Marshal.SizeOf(typeof(StartupInfo));
            if (!CreateProcess(exe, new StringBuilder("\"" + exe + "\""), IntPtr.Zero, IntPtr.Zero, false,
                CreateSuspended, IntPtr.Zero, GameDirectory, ref si, out pi))
                throw new InvalidOperationException("Windows could not start Road Rash. Error " + Marshal.GetLastWin32Error() + ".");
            uint baseAddress = GetImageBase(pi.hProcess);
            if (baseAddress == 0) throw new InvalidOperationException("The game image base is invalid.");
            Apply(pi.hProcess, baseAddress);
            if (ResumeThread(pi.hThread) == 0xFFFFFFFF) throw new InvalidOperationException("Could not resume Road Rash.");
            WakeVideoLoop(pi.hProcess, baseAddress, pi.dwProcessId);
        }
        catch (Exception ex)
        {
            if (pi.hProcess != IntPtr.Zero) TerminateProcess(pi.hProcess, 1);
            MessageBox.Show("Road Rash input patch was not applied.\r\n\r\n" + ex.Message,
                "Road Rash Input Fix", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            if (pi.hThread != IntPtr.Zero) CloseHandle(pi.hThread);
            if (pi.hProcess != IntPtr.Zero) CloseHandle(pi.hProcess);
        }
    }
}
