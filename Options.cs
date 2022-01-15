using CommandLine;

namespace SmsAndMmsMerger;

/// <summary>
/// Command line arguments for the application
/// </summary>
public class Options
{
	/// <summary>
	/// An ordered list of input files to merge
	/// </summary>
	[Option('i', "input-file", Required = true, HelpText = "Files to merge. Note, order matters. When a node exists in more than one file it'll be added from the last file specified. Specify multiple separated by a comma (,)", Separator = ',')]
	public IList<string> InputFiles { get; set; } = Array.Empty<string>();

	/// <summary>
	/// File to output to
	/// </summary>
	[Option('o', "output-file", Required = true, HelpText = "Output file to write to")]
	public string OutputFile { get; set; } = string.Empty;

	/// <summary>
	/// A set of to/from addresses that wil be filtered down to when generating the output file
	/// </summary>
	[Option("must-match-address", Required = false, HelpText = "A list of addresses that must be matched. If specified any SMS/MMS that isn't to/from address containing one of these addresses will be excluded. Note: MMS and SMS tend to use different formats (including/excluding the country code). So best to specify this as a portion of the address. Instead of '0412345678' or '+61412345678' you'll have more luck using '412345678'")]
	public IList<string> MustMatchAddress { get; set; } = Array.Empty<string>();

	/// <summary>
	/// If <c>true</c> the output file will be written even if it already exits
	/// </summary>
	[Option('f', "force", Default = false, HelpText = "Unless set to true will abort when the output file already exists")]
	public bool AllowOutputOverride { get; set; } = false;

	/// <summary>
	/// If <c>true</c> the output file will be date ordered
	/// </summary>
	[Option("reorder-items-by-date", Default = false, HelpText = "SMS Backup and Restore creates files that are ordered with all the SMS and then all the MMS. When you restore these files you get misleading results around how long it'll take (an MMS takes longer to restore than an SMS). By default the merged file will be ordered by received date not the type of the item this gives a more accurate progress time when restoring these files")]
	public bool ReorderItemsByDate { get; set; } = false;
}