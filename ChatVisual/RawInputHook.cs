using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ChatVisual
{
    internal class RawInputHook
    {

        // First call version - accepts IntPtr for null
        [DllImport("User32.dll", SetLastError = true)]
        public static extern uint GetRawInputDeviceList(
            IntPtr pRawInputDeviceList, // becuase we need to pass null to this to get the number of device (field 2)
            ref uint puiNumDevices,
            uint cbSize
        );

        // Second call version - accepts real array
        [DllImport("User32.dll", SetLastError = true)]
        public static extern uint GetRawInputDeviceList(
            [Out] RawInputDeviceList[] pRawInputDeviceList, // now this is the array that we can get the actual device list
            ref uint puiNumDevices,
            uint cbSize
        );

        // get the device info
        [DllImport("User32.dll", SetLastError = true)]
        public static extern uint GetRawInputDeviceInfo(
            IntPtr hDevice,
            uint uiCommand,
            IntPtr pData,
            ref uint pcbSize
        );

        [DllImport("User32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool RegisterRawInputDevices(
            RawInputDevice[] rawInputDevices,
            uint uiNumDevices,
            uint cbSize
        );

        [DllImport("User32.dll", SetLastError = true)]
        public static extern uint GetRawInputData(
            IntPtr hRawInput,
            uint uiCommand,
            [Out] IntPtr pData,
            ref uint pcbSize,
            uint cbSizeHeader
        );


        // set up the hook to intercept event from window (keystroke, mouse click, etc)
        // lpfn is a pointer to hook procedure, which is a callback function that processes the events.
        // so it's a delegate
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        // This is the opposite of SetWindowsHookEx, this is to remove the hook
        // hhk: handle to the hook (this is the return of the SetWindowsHookEx)
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);


        // Pass the event to the next hook in the chain
        // this is where we can swallow the event (by not calling this function) or pass it to the next hook (by calling this function)
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);


        // 
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);


        // This is the function to simulate a keystrokes, mouse motions, and button clicks (yeah literally simulate or synthesize it)
        [DllImport("User32.dll", SetLastError = true)]
        private static extern uint SendInput(
            uint cInputs, // number of the input in the array below
            tagINPUT[] pInputs, // the array of the input we want to simulate
            int cbSize  // the size of the tagINPUT structure (we can get this by Marshal.SizeOf<tagINPUT>()
        );




        // The STRUCT

        // This is the struct to read the lParam from LowLevelKeyboardProc 
        [StructLayout(LayoutKind.Sequential)]
        public struct KBDLLHOOKSTRUCT
        {
            public uint vkCode; // the key code represent the sort of event (pls read here: https://learn.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes_
            public uint scanCode; // the hardware scan code for the key
            public uint flags; // ????
            public uint time; // the timestamp for this event
            public UIntPtr dwExtraInfo;
        }


        [StructLayout(LayoutKind.Sequential)]
        public struct RawInputDeviceList
        {
            public IntPtr hDevice; // this is handle so it's the pointer (IntPrt)
            public uint dwType;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RawInputDevice
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        // THIS IS THE STRUCT FOR THE GetRawInputData FUNCTION
        // docs: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getrawinputdata

        [StructLayout(LayoutKind.Sequential)]
        public struct RawInputHeader
        {
            public uint dwType;
            public uint dwSize;
            public IntPtr hDevice;
            public IntPtr wParam;
        }

        /*
          [StructLayout(LayoutKind.Explicit)]

        // This Struct is for the GetRawInputData too

        public struct tagRAWMOUSE
        {
            [FieldOffset(0)] public ushort usFlags;       // 2 bytes, starts at byte 0

            // UNION starts at byte 4 - these two overlap in memory
            [FieldOffset(4)] public uint ulButtons;        // 4 bytes, starts at byte 4
            [FieldOffset(4)] public ushort usButtonFlags;  // 2 bytes, also starts at byte 4
            [FieldOffset(6)] public ushort usButtonData;   // 2 bytes, starts at byte 6

            [FieldOffset(8)] public uint ulRawButtons;       // 4 bytes
            [FieldOffset(12)] public int lLastX;              // 4 bytes
            [FieldOffset(16)] public int lLastY;              // 4 bytes
            [FieldOffset(20)] public uint ulExtraInformation; // 4 bytes
        }
        */

        // THIS IS for the RAWHID struct, which is part of the UNION for the RawInput
        [StructLayout(LayoutKind.Sequential)]
        public struct tagRAWHID
        {
            public uint dwSizeHid;
            public uint dwCount;
            public byte bRawData;
        }

        // THIS IS for the tagRawMouse struct, which is part of the UNION for the RawInput
        [StructLayout(LayoutKind.Sequential)]
        public struct RawMouseButtonsData
        {
            public ushort usButtonFlags;
            public ushort usButtonData;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct RawMouseButtonsUnion
        {
            [FieldOffset(0)] public uint ulButtons;
            [FieldOffset(0)] public RawMouseButtonsData data;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct tagRAWMOUSE
        {
            public ushort usFlags;
            public RawMouseButtonsUnion buttons;
            public uint ulRawButtons;
            public int lLastX;
            public int lLastY;
            public uint ulExtraInformation;
        }

        // The tagRawKeyboard is part of the UNION for the RawInput
        [StructLayout(LayoutKind.Sequential)]
        public struct tagRawKeyBoard
        {
            public ushort MakeCode;
            public ushort Flags;
            public ushort Reserved;
            public ushort VKey;
            public uint Message;
            public uint ExtraInformation;
        }

        // Now we build the UNION for the RawInput
        [StructLayout(LayoutKind.Explicit)]
        public struct RawInputUnion
        {

            [FieldOffset(0)] public tagRAWMOUSE mouse;
            [FieldOffset(0)] public tagRawKeyBoard keyboard;
            [FieldOffset(0)] public tagRAWHID hid;
        }

        // This Struct is for the GetRawInputData too
        [StructLayout(LayoutKind.Sequential)]
        public struct RawInput
        {
            public RawInputHeader header;
            public RawInputUnion data;
        }



        // This Struct is for the SendInput function. SendInput function need an array of these
        // docs: https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-input

        // NOW THIS IS NEW:
        // INSTEAD of doing [FieldOffset(4)] public tagKEYBDINPUT ki; we will implement it similar to C++ (write all the UNION out instead of just pick one)

        // We would need a MouseInput struct (we DON'T NEED THIS FOR NOW. BUT IT'S PART OF THE STRUCT for the UNION)
        [StructLayout(LayoutKind.Sequential)]
        public struct tagMOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        // This is the struct for the INPUT structure (Right below). This is the one I actually use
        // The struct contain information for the simulated keyboard event
        [StructLayout(LayoutKind.Sequential)]
        public struct tagKEYBDINPUT
        {
            public ushort wVk;  // the virtual key code (represent the key we want to simulate)
            public ushort wScan; // when we don't have the keycode to describe the event then we have this wScan code
            public uint dwFlags;  // settings for the wSCan
            public uint time; // the timestamp for this event to happen. If null the system will pick 1 for us
            public UIntPtr dwExtraInfo; // additional value associated with the keystroke
        }


        // THIS IS FOR THE UNION in the INPUT STRUCT (similar to the tagMOUSEINPUT we don't need this for now but it's part of the UNION)
        [StructLayout(LayoutKind.Sequential)]
        public struct tagHARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamR;
        }


        // Then now we build the UNION struct for the tagInput struct
        [StructLayout(LayoutKind.Explicit)]
        public struct tagInputUnion
        {
            [FieldOffset(0)] public tagMOUSEINPUT mi;
            [FieldOffset(0)] public tagKEYBDINPUT ki;
            [FieldOffset(0)] public tagHARDWAREINPUT hi;
        }

        // Then this is the actual tagINPUT struct for the SendInput function. This is the one we actually use.
        // This struct contain information for the simulated event (mouse, keyboard, or hardware)

        [StructLayout(LayoutKind.Sequential)]
        public struct tagINPUT
        {
            public uint type; // the type of the input (mouse, keyboard, or hardware)
            public tagInputUnion tagInputUnion; // the actual input data (the union of mouse, keyboard, and hardware input)
        }


        // RANDOM FIELDS

        // this is to handle the Mouse Hook
        // basically we add this hook to HwndSource
        // docs: https://learn.microsoft.com/en-us/dotnet/api/system.windows.interop.hwndsource.addhook?view=windowsdesktop-10.0
        // But basically the hook accept a function look like this: https://learn.microsoft.com/en-us/dotnet/api/system.windows.interop.hwndsourcehook?view=windowsdesktop-10.0 
        // the delegate you can consider it like a prop (basically all the parameter and return type need to be the same as delegate). You can change the name
        private const int WM_INPUT = 0x00FF;
        private int countingEvent = 0;


        // pointer that point to the target device 
        private IntPtr targetDeviceHandle = IntPtr.Zero;

        //
        private IntPtr hwnd;

        // delegate for the hook procedure
        // more info about this delegate: https://learn.microsoft.com/en-us/previous-versions/windows/desktop/legacy/ms644985(v=vs.85)
        // nCode: only have 1 value = 0. But technically we have to check for nCode (nCode >= 0)
        // if nCode < 0 that mean the window told us this is system message and must be passed to CallNextHookEx

        // wParam and lParam are pointer size that contain extra info window gave us (so those are not pointer)
        // in this case: WPARAM has info about the event (like keydown, keyup, etc)
        // LPARAM has info about the key (like which key is it)
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        // we need to define the proce. Cause we would need it even after the InstallMacroHook done.
        // Cause window need to call this function every time we get event from the hook chain.
        // So we need to make sure this function is still exist in the memory (not garbage collected) after the InstallMacroHook done. So we need to make it a field of the class
        private LowLevelKeyboardProc lowLevelKeyboardProc;

        // the handle of our HandleRawInput
        private IntPtr macroHookHandle;

        // the flag for RegisterDevice to the app run in background
        private const uint RIDEV_NOLEGACY = 0x00000030;
        private const uint RIDEV_INPUTSINK = 0x00000100;

        // the handle for window
        private HwndSource _source;
        private HwndSourceHook _sourceHook;



        // OUR METHODS

        // this is the function that our hook would call when it receive event from the hook chain
        private IntPtr LowLevelKeyboardFilter(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode < 0)
            {
                // this mean the window told us this is system message and must be passed to CallNextHookEx
                return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
            }
            else
            {
                // we can decide to swallow these event of keep passing them

                // Okay we have a big problem: we only know the key, but not the device from the wParam and lParam -> don't know if it's our macro or not
                // but the RawInput earlier can tell the device, but then it can't stop the event
                // so the genius plan. We track the timestamp that the last macro was push down
                // because RawInput happen before the LowLevel Hook, it a macro was press down about 15ms before it reach here: it's very likely (like 99.999%) that key is the macro key


                // we check if the key is a macro key (F1 - F9) and if the timestamp is within 15ms of the last macro key event
                long now = Environment.TickCount;

                Console.WriteLine("Low Level Hook got event! This is the data:");

                // Now we read from the lParam to get the actual key info (which key is it, etc)
                KBDLLHOOKSTRUCT kbdStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

                // THIS IS AN INSANELY COMPLICATED CONDITION
                // but in general, it check if the event is injected (not from physical keyboard)
                // why: because the current logic is: all the event come from macro would be injected (we stop everything from F1 - F9 at low level)
                // but the RawInput reject them IF THEY FROM THE MACRO
                // but the way the condition works involve confusing bitwise calculation (because this flag is confusing)
                // learn more: https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-kbdllhookstruct 
                // LIMITATION: this is not 100% accurate, stuff like the on-screen keyboad also inject event (but it's good enough for now)
                bool isInjected = (kbdStruct.flags & 0x10) != 0;
                if (isInjected)
                {
                    return CallNextHookEx(macroHookHandle, nCode, wParam, lParam);
                }

                // we swallow the event if it's F1 - F9  and are not injected event
                // 

                if (kbdStruct.vkCode >= 112 && kbdStruct.vkCode <= 120) // F1-F9 only
                {
                    return (IntPtr)1; // swallow
                }

                return CallNextHookEx(macroHookHandle, nCode, wParam, lParam); // everything else passes

            }
        }

        // INSTALL HOOK METHOD
        private void InstallMacroHook()
        {
            // Id for the WH_KEYBOARD_LL -> what we need
            int idHook = 13;

            // the delegate (the hook procedure).
            // we simply need to declare it to get the pointer to it (which is what SetWindowsHookEx need)

            // since it's a delegate (a callback) we need to pass the actual function that match the delegate (the function is HandleRawInput)
            lowLevelKeyboardProc = new LowLevelKeyboardProc(LowLevelKeyboardFilter);

            // then we need the handle to this module (or the module contain the callback).
            // in this case then IT IS THIS CLASS
            IntPtr currModuleHandle = GetModuleHandle(null); // passing null mean we want the handle of the current module (the exe that run this code)

            // dwThreadId
            // passing threadId = 0 mean we want to hook all the thread in the system (global hook).
            uint newModuleHandle = 0;

            // calling the SetWindowsHookEx to install the hook
            // it return the macroHookHandle. We can use this to remove the hook later
            macroHookHandle = SetWindowsHookEx(idHook, lowLevelKeyboardProc, currModuleHandle, newModuleHandle);


        }


        // LIST DEVICES METHOD
        // this method is to list all the device that we have.
        // We will call this method in the constructor to see all the device we have and get the handle of the mouse that we want to listen to

        private (RawInputDeviceList[] deviceList, uint result) ListDevice()
        {
            uint dwSize = (uint)Marshal.SizeOf<RawInputDeviceList>();
            uint deviceCount = 0;
            // we first get the number of devices
            GetRawInputDeviceList(IntPtr.Zero, ref deviceCount, dwSize);

            RawInputDeviceList[] deviceList = new RawInputDeviceList[(int)deviceCount];

            // step 2: then we simply need to pass the device count to the 2nd call
            uint result = GetRawInputDeviceList(deviceList, ref deviceCount, dwSize);

            return (deviceList, result);

        }


        // REGISTER DEVICES METHOD
        // This function register our device with window
        // basically told window: "hey I want to listen to all the input of the device to be here
        private bool RegisterDevice()
        {
            // we first need to make sure we get the size of the structure first
            uint cbSize = (uint)Marshal.SizeOf<RawInputDevice>();
            uint uiNumDevices = 1;
            RawInputDevice[] rawInputDevices = new RawInputDevice[(int)uiNumDevices];





            // we need to populate the struct for ourself. Since the uiNumDevices = 1 (only 1 element)
            rawInputDevices[0].usUsagePage = 0x0001; // mouse class driver and mapped driver
            rawInputDevices[0].usUsage = 0x0006; // no idea???
            rawInputDevices[0].dwFlags = RIDEV_INPUTSINK; // get data even when the app is not focused
            rawInputDevices[0].hwndTarget = hwnd;


            Console.WriteLine("Window handle: " + hwnd);

            rawInputDevices[0].hwndTarget = hwnd;

            bool isRegisterSuccessful = RegisterRawInputDevices(
                rawInputDevices,
                uiNumDevices,
                cbSize
            );


            return isRegisterSuccessful;

        }


        // 
        private bool FindTargetDevice(RawInputDeviceList[] deviceList)
        {
            // let see all the device
            foreach (RawInputDeviceList device in deviceList)
            {

                uint pcbSize = 0;

                Console.WriteLine("This is the device Info");
                Console.WriteLine(device.dwType);
                Console.WriteLine(device.hDevice);

                // Then we call the GetRawInputDeviceInfo to extract device info
                GetRawInputDeviceInfo(
                    device.hDevice, // handle to the raw input device
                    0x20000007, // getting device name
                    IntPtr.Zero,
                    ref pcbSize

                );


                // now the pcbSize is the size of the buffer we need. We need to create the buffer
                // and pointer to it

                IntPtr buffer = Marshal.AllocHGlobal((int)pcbSize); // this return a pointer to the buffer with size pcbSize
                GetRawInputDeviceInfo(
                    device.hDevice,
                    0x20000007,
                    buffer,
                    ref pcbSize
                );

                Console.WriteLine(Marshal.PtrToStringAnsi(buffer)); // conver the buffer to string to read it

                // this mean we set our target to the Mouse Dell
                // VID_413C is the vendor ID for Dell, so this is to make sure we only listen to the mouse from Dell
                string bufferString = Marshal.PtrToStringAnsi(buffer);
                if (bufferString.Contains("VID_1189") && bufferString.Contains("MI_00")) // this is innfo of our macro
                {
                    targetDeviceHandle = device.hDevice;
                    // release that memory back to use we're done
                    Marshal.FreeHGlobal(buffer);

                    return true;

                }

                Marshal.FreeHGlobal(buffer);


            }
            return false;

        }



        // Method to inject the keyboard event from RawInput with SendInput
        private void InjectKeyboardEvent(ushort vKey, bool isKeyUp)
        {
            tagINPUT[] tagINPUTs = new tagINPUT[1];

            tagINPUTs[0].type = 1; // 1 for keyboard input
            tagINPUTs[0].tagInputUnion.ki = new tagKEYBDINPUT
            {
                wVk = vKey,
                wScan = 0,
                dwFlags = isKeyUp ? 0x0002u : 0x0000u, // KEYEVENTF_KEYUP for key up, 0 for key down
                time = 0,
                dwExtraInfo = UIntPtr.Zero
            };

            uint sent = SendInput(1, tagINPUTs, Marshal.SizeOf<tagINPUT>());

            if (sent != tagINPUTs.Length)
            {
                Console.WriteLine("SendInput failed or was blocked.");
            }

        }



        // this is the method that we will add to the hook.
        // This is the method that will be called every time we get a WM_INPUT message (which is when we get input from the mouse)
        // basically we match the delegate here: https://learn.microsoft.com/en-us/dotnet/api/system.windows.interop.hwndsourcehook?view=windowsdesktop-10.0
        private IntPtr HandleRawInput(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {

            if (msg == WM_INPUT)
            {
                Console.WriteLine("Got WM_INPUT number " + countingEvent);
                countingEvent += 1;

                // so now we have the input from the hook. It come from WM_INPUT
                // but we need a way to get actual mouse data
                // and we pass the lParam to GetRawInputData function to get the actual mouse data


                // Let write our GetRawInputData

                // Pass 1: we need to get the size of pcbSize
                uint uiCommand = 0x10000003;
                uint pcbSize = 0;
                uint cbSizeHeader = (uint)Marshal.SizeOf<RawInputHeader>();
                Console.WriteLine("Header size: " + Marshal.SizeOf<RawInputHeader>());

                GetRawInputData(lParam, uiCommand, IntPtr.Zero, ref pcbSize, cbSizeHeader);

                // Pass 2: now we actually have the pcbSize
                IntPtr pData = Marshal.AllocHGlobal((int)pcbSize);
                Console.WriteLine("pcbSize: " + pcbSize); // add this

                GetRawInputData(lParam, uiCommand, pData, ref pcbSize, cbSizeHeader);

                // Marshal.PtrToStructure basically meant: starting at the beginning (since pData is a pointer to the beginning of the buffer)
                // Eead the memory the size of RawInputHeader and convert it to the RawInputHeader struct
                RawInputHeader header = Marshal.PtrToStructure<RawInputHeader>(pData);

                // extract information for the RawInput header (this use to determine if it's the event we want)
                uint headerdwType = header.dwType; // 0 is mouse, 1 is keyboard, 2 is HID
                IntPtr headerhDevice = header.hDevice; // this is the handle to the device that generate this event. We can compare this with our targetDeviceHandle to see if it's the event we want
                IntPtr headerwParam = header.wParam; // extr info 


                if (headerdwType == 1) // this mean that the event come from keyboard and it's F1-F9
                {

                    // ✅ ADD THIS - breaks the infinite loop
                    if (headerhDevice == IntPtr.Zero)
                    {
                        Console.WriteLine("Injected event detected, ignoring.");
                        Marshal.FreeHGlobal(pData);
                        return IntPtr.Zero;
                    }



                    // then now we need to move the pointer from the head to right after the header
                    // so now the pointer is at the beginning of the RawInputUnion (which is the actual data we want)
                    IntPtr payloadPtr = IntPtr.Add(pData, Marshal.SizeOf<RawInputHeader>());

                    // then we read the data from the payloadPtr (we know for sure that this is the keyboard data)
                    tagRawKeyBoard tagRawKeyBoard = Marshal.PtrToStructure<tagRawKeyBoard>(payloadPtr);

                    // extract the data from the tagRawKeyBoard

                    // each key has a code (full list https://learn.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes)
                    // the code is in hexadecimal. So 0x70 = hex is 112 in decimal, which is the code for F1
                    ushort keyboardVkey = tagRawKeyBoard.VKey;
                    ushort keyboardFlags = tagRawKeyBoard.Flags; // this is to determine if it's keydown or keyup. If the least significant bit is 1, it's keyup. If it's 0, it's keydown



                    // now we now that the rawInput has 2 fields: header and mouse
                    // dwType == 1 -> come from keyboard
                    // rawInput.keyboard.Flags == 0 -> only keydown event
                    bool isTargetKey = keyboardVkey >= 0x70 && keyboardVkey <= 0x78; // F1-F9
                    bool isMacroDevice = headerhDevice == targetDeviceHandle;
                    bool isKeyUp = (keyboardFlags & 0x0001) != 0; // RI_KEY_BREAK

                    if (isTargetKey) // only do all these processing if it's the targetkey
                    {

                        if (isMacroDevice)
                        {
                            Console.WriteLine("GGoott  kkeeyy from target device!");

                            // Keep it swallowed.
                            // Run your macro logic here.
                            // DO NOT call SendInput here.
                        }
                        else
                        {
                            Console.WriteLine("Got kkeeyy from another keyboard. Reinjecting....");


                            InjectKeyboardEvent(keyboardVkey, isKeyUp);

                            // Set the flag to indicate we're injecting
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Got input from other device or other type!");
                }

                // release that memory back to use we're done
                Marshal.FreeHGlobal(pData);


            }

            return IntPtr.Zero;
        }


        // Clean up function to remove the hook when we're done
        public void Shutdown()
        {
            if (macroHookHandle != IntPtr.Zero)
            {
                bool ok = UnhookWindowsHookEx(macroHookHandle);

                if (!ok)
                {
                    int error = Marshal.GetLastWin32Error();
                    Console.WriteLine("UnhookWindowsHookEx failed: " + error);
                }

                macroHookHandle = IntPtr.Zero;
            }
        }




        // OUR CONSTRUCTOR
        public RawInputHook(Window window)
        {

            // Let install the hook:
            // This is the hook to read and intercept the input events from window
            InstallMacroHook();


            // this is literally mean handle to the window
            hwnd = new WindowInteropHelper(window).Handle;

            Console.WriteLine("Hey the Raw Input Constructor is running");

            // We first list our devices
            (RawInputDeviceList[] deviceList, uint result) = ListDevice();

            if (result == uint.MaxValue)  // this is actually the uint -1 in the doc
            {
                Console.WriteLine("Failed to get device list");
                int errorCode = Marshal.GetLastWin32Error();
                string errorMessage = new Win32Exception(errorCode).Message;
                Console.WriteLine($"Error {errorCode}: {errorMessage}");

                return;
            }
            else
            {
                // then we scan through the device list and find it
                bool isFoundTarget = FindTargetDevice(deviceList);
                if (isFoundTarget)
                {
                    Console.WriteLine("Found target device with handle: " + targetDeviceHandle);
                }
                else
                {
                    Console.WriteLine("Target device not found");
                    return;
                }



                // Then we need to register that device
                bool isRegisterSuccessful = RegisterDevice();

                if (isRegisterSuccessful)
                {
                    Console.WriteLine("Register device succesfully");
                }
                else
                {
                    Console.WriteLine("Register device NOT succesful");
                    int errorCode = Marshal.GetLastWin32Error();
                    Console.WriteLine("Register Success " + errorCode);

                    return;
                }



                // then now we need the HwndSource (this is so confusing so pls look this up)
                // but basically this capture all the message sent to THIS WINDOW (and not other application)
                // Also if you wondering if this hook receive the message first or our LowLevel receive first -> then the low level receive first
                // The only reason the message for RawInput come first is because Raw Input process the message SO FAST it comes out first
                _source = HwndSource.FromHwnd(hwnd);
                _sourceHook = HandleRawInput;
                _source.AddHook(_sourceHook);

            }

        }

    }
}

