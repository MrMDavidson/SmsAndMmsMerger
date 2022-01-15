using System.Diagnostics;

namespace SmsAndMmsMerger;

[DebuggerDisplay("{Offset,nq} - Line = {LineNumber,nq}, Col = {LinePosition,nq}")]
public class NodePosition
{
	/// <summary>
	/// 1-based line number of this node in its source file
	/// </summary>
	public int LineNumber { get; set; }

	/// <summary>
	/// 1-based column of the node's first character in the source file
	/// </summary>
	public int LinePosition { get; set; }

	/// <summary>
	/// Byte offset in the source file for this node
	/// </summary>
	public long Offset { get; set; }
}