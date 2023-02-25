namespace SmsAndMmsMerger;

/// <summary>
/// Provides details about an XML node
/// </summary>
public class NodePathInfo
{
	public NodePathInfo()
	{
		Start = new NodePosition();
		End = new NodePosition();
		AlternateIds = Array.Empty<string>();
		Id = "#new";
		PrimaryId = "#new";
		Name = "#new";
	}

	/// <summary>
	/// Name of the node
	/// </summary>
	/// <example>&lt;sms&gt; would be named sms</example>
	public string Name { get; set; }

	/// <summary>
	/// An identifier for this node
	/// </summary>
	public string Id { get; set; }

	/// <summary>
	/// The primary identifier of the node
	/// </summary>
	public string PrimaryId { get; set; }

	/// <summary>
	/// If this <seealso cref="NodePathInfo"/> is the primary identifier for the node
	/// </summary>
	public bool IsPrimaryId { get; set; }

	/// <summary>
	/// Other identifiers that can be used to find this <seealso cref="NodePathInfo"/>
	/// </summary>
	public IList<string> AlternateIds { get; set; }

	/// <summary>
	/// Date that this node is for
	/// </summary>
	public long Date { get; set; }

	/// <summary>
	/// Positional information for the start of the node
	/// </summary>
	public NodePosition Start { get; set; }

	/// <summary>
	/// Positional information for the end of the node
	/// </summary>
	public NodePosition End { get; set; }

	/// <summary>
	/// Number of bytes the node occupies
	/// </summary>
	public long ByteCount => End.Offset - Start.Offset;

	/// <summary>
	/// Address (ie. phone number) for this node
	/// </summary>
	public string? Address { get; set; }
}