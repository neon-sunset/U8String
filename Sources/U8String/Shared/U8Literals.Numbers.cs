using System.Diagnostics;
using System.Runtime.InteropServices;

namespace U8.Shared;

static partial class U8Literals
{
    internal static class Numbers
    {
        internal const int Length = 256;

        internal static readonly U8String[] Values =
        [
            u8('0'), u8('1'), u8('2'), u8('3'), u8('4'), u8('5'), u8('6'), u8('7'),
            u8('8'), u8('9'), u8("10"), u8("11"), u8("12"), u8("13"), u8("14"), u8("15"),
            u8("16"), u8("17"), u8("18"), u8("19"), u8("20"), u8("21"), u8("22"), u8("23"),
            u8("24"), u8("25"), u8("26"), u8("27"), u8("28"), u8("29"), u8("30"), u8("31"),
            u8("32"), u8("33"), u8("34"), u8("35"), u8("36"), u8("37"), u8("38"), u8("39"),
            u8("40"), u8("41"), u8("42"), u8("43"), u8("44"), u8("45"), u8("46"), u8("47"),
            u8("48"), u8("49"), u8("50"), u8("51"), u8("52"), u8("53"), u8("54"), u8("55"),
            u8("56"), u8("57"), u8("58"), u8("59"), u8("60"), u8("61"), u8("62"), u8("63"),
            u8("64"), u8("65"), u8("66"), u8("67"), u8("68"), u8("69"), u8("70"), u8("71"),
            u8("72"), u8("73"), u8("74"), u8("75"), u8("76"), u8("77"), u8("78"), u8("79"),
            u8("80"), u8("81"), u8("82"), u8("83"), u8("84"), u8("85"), u8("86"), u8("87"),
            u8("88"), u8("89"), u8("90"), u8("91"), u8("92"), u8("93"), u8("94"), u8("95"),
            u8("96"), u8("97"), u8("98"), u8("99"), u8("100"), u8("101"), u8("102"), u8("103"),
            u8("104"), u8("105"), u8("106"), u8("107"), u8("108"), u8("109"), u8("110"), u8("111"),
            u8("112"), u8("113"), u8("114"), u8("115"), u8("116"), u8("117"), u8("118"), u8("119"),
            u8("120"), u8("121"), u8("122"), u8("123"), u8("124"), u8("125"), u8("126"), u8("127"),
            u8("128"), u8("129"), u8("130"), u8("131"), u8("132"), u8("133"), u8("134"), u8("135"),
            u8("136"), u8("137"), u8("138"), u8("139"), u8("140"), u8("141"), u8("142"), u8("143"),
            u8("144"), u8("145"), u8("146"), u8("147"), u8("148"), u8("149"), u8("150"), u8("151"),
            u8("152"), u8("153"), u8("154"), u8("155"), u8("156"), u8("157"), u8("158"), u8("159"),
            u8("160"), u8("161"), u8("162"), u8("163"), u8("164"), u8("165"), u8("166"), u8("167"),
            u8("168"), u8("169"), u8("170"), u8("171"), u8("172"), u8("173"), u8("174"), u8("175"),
            u8("176"), u8("177"), u8("178"), u8("179"), u8("180"), u8("181"), u8("182"), u8("183"),
            u8("184"), u8("185"), u8("186"), u8("187"), u8("188"), u8("189"), u8("190"), u8("191"),
            u8("192"), u8("193"), u8("194"), u8("195"), u8("196"), u8("197"), u8("198"), u8("199"),
            u8("200"), u8("201"), u8("202"), u8("203"), u8("204"), u8("205"), u8("206"), u8("207"),
            u8("208"), u8("209"), u8("210"), u8("211"), u8("212"), u8("213"), u8("214"), u8("215"),
            u8("216"), u8("217"), u8("218"), u8("219"), u8("220"), u8("221"), u8("222"), u8("223"),
            u8("224"), u8("225"), u8("226"), u8("227"), u8("228"), u8("229"), u8("230"), u8("231"),
            u8("232"), u8("233"), u8("234"), u8("235"), u8("236"), u8("237"), u8("238"), u8("239"),
            u8("240"), u8("241"), u8("242"), u8("243"), u8("244"), u8("245"), u8("246"), u8("247"),
            u8("248"), u8("249"), u8("250"), u8("251"), u8("252"), u8("253"), u8("254"), u8("255")
        ];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsInRange(nint value)
        {
            return (nuint)value < Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static U8String GetValueUnchecked(nint value)
        {
            Debug.Assert((nuint)value < Length);
            return Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(Values), value);
        }
    }
}
