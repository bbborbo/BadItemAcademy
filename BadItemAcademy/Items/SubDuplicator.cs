using BadItemAcademy.Modules;
using MonoMod.Cil;
using System;
using System.Collections.Generic;
using System.Text;

namespace BadItemAcademy
{
    public partial class BadItemAcademyPlugin
    {
        internal static int _SuplicatorTempExtension = 80; //10
        public static void RehabSuplicator()
        {
            IL.RoR2.Items.DuplicatorBehavior.CalculateNewItemDecayDuration += ModifySuplicatorExtension;
        }

        private static void ModifySuplicatorExtension(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            bool b1 = c.TryGotoNext(MoveType.Before,
                x => x.MatchLdarg(1),
                x => x.MatchLdcI4(out _),
                x => x.MatchMul()
                );
            if (!b1)
            {
                DebugBreakpoint(nameof(ModifySuplicatorExtension), 1);
                return;
            }
            c.Index++;
            c.Next.Operand = Bindings.SuplicatorTempExtension.Value;
        }
    }
}
