using System.Xml;

namespace SmsAndMmsMerger;

/// <summary>
/// Extension methods for <see cref="XmlReader"/>
/// </summary>
public static class XmlReaderExtensions
{
    /// <summary>
    /// Returns the value of <paramref name="attribute"/>. If the attribute equals the literal string "null", or is null or whitespace, it'll return null
    /// </summary>
    public static string? GetAttributeNullable(this XmlReader reader, string attribute)
    {
        string? value = reader.GetAttribute(attribute);
        if (string.IsNullOrWhiteSpace(value) == true)
        {
            return null;
        }

        if (value == "null")
        {
            return null;
        }

        return value;
    }

	/// <summary>
	/// Moves the <see cref="XmlReader"/> to the next readable sibling regardless of its name.
	/// </summary>
	/// <param name="reader"><seealso cref="XmlReader"/> to operate on</param>
	/// <returns><c>true</c> if a sibling node was found, <c>false</c> if it wasn't</returns>
    public static bool ReadToNextSibling(this XmlReader reader)
    {
	    int depth = reader.Depth;

	    do
	    {
	    } while ((reader.Read() == true) && (reader.Depth > depth));

	    if (reader.NodeType == XmlNodeType.EndElement)
	    {
		    if (reader.Read() == false)
		    {
			    return false;
		    }
	    }

	    return reader.Depth == depth;
    }

	/// <summary>
	/// Reads an attribute as a <see cref="long"/> with a list of fallback attributes to read from should the first item not be a valid <see cref="long"/>
	/// </summary>
	/// <param name="reader"><see cref="XmlReader"/> to operate on</param>
	/// <param name="primaryAttribute">Preferred name of the XML attribute to read from</param>
	/// <param name="alternateAttributes">Alternate attributes to read from, if required</param>
	/// <returns>Value of the first attribute with a valid value</returns>
    public static long? GetAttributeAsLongWithFallback(this XmlReader reader, string primaryAttribute, params string[] alternateAttributes)
    {
        Func<string, long?> converter = inp =>
        {
            long converted;
            if (long.TryParse(inp, out converted) == false)
            {
                return null;
            }

            return converted;
        };

        return GetAttributeWithFallback(reader, primaryAttribute, converter, alternateAttributes);
	}

	/// <summary>
	/// Reads an attribute, of type <typeparamref name="TType"/>, with a list of fall back attributes if required
	/// </summary>
	/// <typeparam name="TType">Type of result</typeparam>
	/// <param name="reader"><seealso cref="XmlReader"/> to operate on</param>
	/// <param name="primaryAttribute">Preferred attribute name</param>
	/// <param name="converter">Method for converting the string representation of the attribute to <typeparamref name="TType"/></param>
	/// <param name="alternateAttributes">Fallback attributes if required</param>
	/// <returns>Value of the first attribute with a valid value</returns>
	public static TType? GetAttributeWithFallback<TType>(this XmlReader reader, string primaryAttribute, Func<string, TType?> converter, params string[] alternateAttributes)
    {
        string value = reader.GetAttributeNullable(primaryAttribute)!;
        TType? converted = default(TType);

        if (string.IsNullOrWhiteSpace(value) == false)
        {
            converted = converter(value);
            if (converted != null)
            {
                return converted;
            }
        }

        foreach (string alternate in alternateAttributes)
        {
            value = reader.GetAttribute(alternate)!;
            if (string.IsNullOrWhiteSpace(value) == false)
            {
                converted = converter(value);
                if (converted != null)
                {
                    return converted;
                }
            }
        }

        return default;
    }

	/// <summary>
	/// Gets an <seealso cref="IXmlLineInfo"/> from the <paramref name="reader"/>
	/// </summary>
    public static IXmlLineInfo AsLineInfo(this XmlReader reader)
    {
		return reader as IXmlLineInfo ?? throw new ArgumentException($"{nameof(reader)} cannot be cast to {nameof(IXmlLineInfo)}");
    }
}