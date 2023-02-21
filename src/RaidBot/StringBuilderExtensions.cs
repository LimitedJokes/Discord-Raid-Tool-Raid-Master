using System.Text;

namespace RaidBot;

internal static class StringBuilderExtensions
{
    public static StringBuilder AppendTruncated(this StringBuilder stringBuilder, string str, int maxLength)
    {
        if (str is null)
        {
            throw new ArgumentNullException(nameof(str));
        }

        if (maxLength < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLength));
        }

        if (str.Length <= maxLength)
        {
            stringBuilder.Append(str);
        }
        else
        {
            stringBuilder.Append(str.AsSpan()[..(maxLength - 3)].TrimEnd()).Append("...");
        }

        return stringBuilder;
    }
}
