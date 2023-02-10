using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ChatCommands.Chat.Commands;
using CoreLib.Util;
using HarmonyLib;
using Iced.Intel;
using Code = Iced.Intel.Code;

namespace ChatCommands.Chat
{
    public static class MapUI_Patch
    {
        public static IntPtr constAddr;

        public static float bigRevealRadius
        {
            set
            {
                if (constAddr != IntPtr.Zero)
                {
                    int bits = BitConverter.SingleToInt32Bits(value);
                    Marshal.WriteInt32(constAddr, bits);
                }
            }
        }

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
       private static extern bool VirtualProtect(IntPtr lpAddress, IntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);

       private static bool VirtualProtect(IntPtr lpAdress, IntPtr dwSize, ProtectMode flNewProtect, out ProtectMode lpflOldProtect)
       {
           bool result = VirtualProtect(lpAdress, dwSize, (uint)flNewProtect, out uint oldProtect);
           lpflOldProtect = (ProtectMode)oldProtect;
           return result;
       }

       [NativeTranspilerPatch(typeof(MapUI), nameof(MapUI.revealDistance), MethodType.Getter)]
       public static List<Instruction> Transpiler(List<Instruction> instructions)
       {
           for (int i = 0; i < instructions.Count; i++)
           {
               var inst = instructions[i];
               if (inst.Code == Code.Movss_xmm_xmmm32)
               {
                   IntPtr address = (IntPtr)inst.IPRelativeMemoryAddress;
                   ChatCommandsPlugin.logger.LogDebug($"Target Addess: {address}");

                   if (VirtualProtect(address, (IntPtr)8, ProtectMode.PAGE_READWRITE, out ProtectMode protect))
                   {
                       int value = Marshal.ReadInt32(address);
                       float fvalue = BitConverter.Int32BitsToSingle(value);
                       ChatCommandsPlugin.logger.LogDebug($"Old value: {fvalue}, got pointer!");
                       constAddr = address;
                   }
               }
           }

           return instructions;
       }
    }
}