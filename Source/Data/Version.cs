using Jamiras.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RATools.Data
{
    public static class Version
    {
        public static readonly SoftwareVersion MinimumVersion = new SoftwareVersion(0, 30);
        public static readonly SoftwareVersion Uninitialized = new SoftwareVersion(0, 0);

        /// <summary>
        /// 0.76 - 21 Jun 2019
        ///  Flags: AndNext
        /// </summary>
        public static readonly SoftwareVersion _0_76 = new SoftwareVersion(0, 76);

        /// <summary>
        /// 0.77 - 30 Nov 2019
        ///  Flags: AddAddress, Measured
        ///  Sizes: TByte
        ///  ValueFormats: TimeMinutes, TimeSecsAsMins
        /// </summary>
        public static readonly SoftwareVersion _0_77 = new SoftwareVersion(0, 77);

        /// <summary>
        /// 0.78 - 18 May 2020
        ///  Flags: MeasuredIf, OrNext
        ///  Operators: Multiply, Divide, BitwiseAnd
        ///  Sizes: BitCount
        /// </summary>
        public static readonly SoftwareVersion _0_78 = new SoftwareVersion(0, 78);

        /// <summary>
        /// 0.79 - 22 May 2021
        ///  Flags: ResetNextIf, Trigger, SubHits
        ///  Other: RichPresence collapsed lookups
        /// </summary>
        public static readonly SoftwareVersion _0_79 = new SoftwareVersion(0, 79);

        /// <summary>
        /// 1.0 - 29 Jan 2022
        ///  Flags: MeasuredPercent
        ///  Sizes: WordBE, TByteBE, DWordBE, Float, MBF32
        ///  ValueFormats: Float1-6
        ///  Other: RichPresence built-in macros
        /// </summary>
        public static readonly SoftwareVersion _1_0 = new SoftwareVersion(1, 0);

        /// <summary>
        /// 1.1 - 15 Nov 2022
        ///  Operators: BitwiseXor
        ///  Sizes: MBF32LE
        ///  Otehr: Local code notes
        /// </summary>
        public static readonly SoftwareVersion _1_1 = new SoftwareVersion(1, 1);

        /// <summary>
        /// 1.3 - TBD
        ///  Sizes: FloatBE
        ///  ValueFormats: Tens, Hundreds, Thousands, Fixed1-3
        ///  Other: AchievementTypes
        /// </summary>
        public static readonly SoftwareVersion _1_3 = new SoftwareVersion(1, 3);
    }
}
