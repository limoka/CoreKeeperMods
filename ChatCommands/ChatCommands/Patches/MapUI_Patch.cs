using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using ChatCommands.Chat.Commands;
using CoreLib.Util;
using HarmonyLib;

#if IL2CPP
using Iced.Intel;
using Code = Iced.Intel.Code;
#endif


namespace ChatCommands.Chat
{
#if !IL2CPP
    [HarmonyPatch]
#endif
    public static class MapUI_Patch
    {
#if IL2CPP
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
#else
        public static float bigRevealRadius;

        [HarmonyPatch(typeof(MapUI), nameof(MapUI.revealDistance), MethodType.Getter)]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            CodeMatcher matcher = new CodeMatcher(instructions)
                .MatchForward(false, new CodeMatch(OpCodes.Ldc_R4, 12f));

            var labels = matcher.Labels;
            
            matcher.SetInstruction(Transpilers.EmitDelegate(() => bigRevealRadius));
            matcher.Labels = labels;

            return matcher.InstructionEnumeration();
        }
#endif
    }
}