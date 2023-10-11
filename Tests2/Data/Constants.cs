using System.Text;

namespace U8Primitives.Tests;

public static class Constants
{
    public static IEnumerable<Rune> AllRunes => Enumerable
        .Range(0, 0xD7FF + 1).Concat(Enumerable
        .Range(0xE000, 0x10FFFF - 0xE000 + 1))
        .Select(i => new Rune(i));

    public static readonly byte[] AsciiBytes =
        Enumerable.Range(0b0000_0000, 128).Select(i => (byte)i).ToArray();

    public static readonly string Ascii = Encoding.ASCII.GetString(AsciiBytes);

    public const string Cyrilic =
        "абвгґдеєжзиіїйклмнопрстуфхцчшщьюя" +
        "АБВГҐДЕЄЖЗИІЇЙКЛМНОПРСТУФХЦЧШЩЬЮЯ";

    public static readonly byte[] CyrilicBytes = Encoding.UTF8.GetBytes(Cyrilic);

    public static IEnumerable<byte[]> CyrilicCharBytes =>
        Cyrilic.Select((_, i) => Encoding.UTF8.GetBytes(Cyrilic, i, 1));

    public const string Kana =
        "あいうえおかきくけこさしすせそたちつてとなにぬねの" +
        "はひふへほまみむめもやゆよらりるれろわをん" +
        "がぎぐげござじずぜぞだぢづでどばびぶべぼ" +
        "ぱぴぷぺぽアイウエオカキクケコサシスセソ" +
        "タチツテトナニヌネノハヒフヘホマミムメモ" +
        "ヤユヨラリルレロワヲンガギグゲゴザジズゼゾ" +
        "ダヂヅデドバビブベボパピプペポ";

    public static readonly byte[] KanaBytes = Encoding.UTF8.GetBytes(Kana);

    public static IEnumerable<byte[]> KanaCharBytes =>
        Kana.Select((_, i) => Encoding.UTF8.GetBytes(Kana, i, 1));

    public const string NonSurrogateEmoji =
        "😀😁😂🤣😃😄😅😆😉😊😋😎😍😘😗😙😚😐😑" +
        "😶🙄😏😣😥😮😯😪😫😴😌😛😜😝🤤😒😓😔😕" +
        "🙃🤑😲🙁😖😞😟😤😢😭😦😧😨😩😬😰😱";

    public static readonly byte[] NonSurrogateEmojiBytes = Encoding.UTF8.GetBytes(NonSurrogateEmoji);

    public static IEnumerable<byte[]> NonSurrogateEmojiChars =>
        NonSurrogateEmoji.EnumerateRunes().Select(TestExtensions.ToUtf8);

    public const string Mixed =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZÄÖÜß" +
        "abcäöüあいうабв🤣😃😄😅诶比西" +
        "АБВГҐДЕЄЖЗИІЇЙКЛМНОПРСТУФХЦЧШ" +
        "ЩЬЮЯあいうえおかきくけこさしすせそ" +
        "たちつてとなにぬねのはひふへほまみむめも" +
        "やゆよらりるれろわをんがぎぐげござじずぜぞ" +
        "∈∉∊∋∌∍∎∏∐∑−∓∔∕∖∗∘∙√∛∜∝∞∟∠∡∢∣∤∥∦∧∨∩∪∫∬∭∮∯∰∱" +
        "∲∳∴∵∶∷∸∹∺∻∼∽∾∿≀≁≂≃≄≅≆≇≈≉≊≋≌≍≎≏≐≑≒≓≔≕≖≗≘≙≚" +
        "≛≜≝≞≟≠≡≢≣≤≥≦≧≨≩≪≫≬≭≮≯≰≱≲≳≴≵≶≷≸≹≺≻≼≽≾≿";

    public static readonly byte[] MixedBytes = Encoding.UTF8.GetBytes(Mixed);

    public static IEnumerable<byte[]> MixedCharBytes =>
        Mixed.EnumerateRunes().Select(TestExtensions.ToUtf8);

    public const string AsciiWhitespace = "\t\n\v\f\r ";

    public static ReadOnlySpan<byte> AsciiWhitespaceBytes => "\t\n\v\f\r "u8;

    public static readonly byte[] NonAsciiBytes =
        Enumerable.Range(0b1000_0000, 128).Select(i => (byte)i).ToArray();

    public static readonly byte[] ContinuationBytes =
        Enumerable.Range(0b1000_0000, 64).Select(i => (byte)i).ToArray();

    public static readonly byte[] NonContinuationBytes =
        Enumerable.Range(0b0000_0000, 128).Select(i => (byte)i).Concat(
        Enumerable.Range(0b1100_0000, 64).Select(i => (byte)i)).ToArray();

    public static readonly Rune[] WhitespaceRunes =
    [
        new('\t'), new('\n'), new('\v'), new('\f'), new('\r'), new(' '), // ASCII
        new(0x0085), new(0x00A0), new(0x1680), new(0x2000), new(0x2001), // Unicode
        new(0x2002), new(0x2003), new(0x2004), new(0x2005), new(0x2006),
        new(0x2007), new(0x2008), new(0x2009), new(0x200A), new(0x2028),
        new(0x2029), new(0x202F), new(0x205F), new(0x3000)
    ];

    public static readonly byte[][] WhitespaceCharBytes =
        WhitespaceRunes.Select(TestExtensions.ToUtf8).ToArray();

    // Covers all non-whitespace runes
    public static IEnumerable<Rune> NonWhitespaceRunes => Enumerable
        .Range(0, 0xD7FF + 1).Concat(Enumerable
        .Range(0xE000, 0x10FFFF - 0xE000 + 1))
        .Select(i => new Rune(i))
        .Except(WhitespaceRunes);
}
