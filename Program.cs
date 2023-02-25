using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using CommandLine;
using CommandLine.Text;

namespace SmsAndMmsMerger;

public class Program
{
	public static async Task<int> Main(string[] args)
	{
		Options options = null!;
		ParserResult<Options>? parserResult = CommandLine.Parser.Default.ParseArguments<Options>(args)
			.WithParsed(o => options = o);
		if (parserResult.Tag == ParserResultType.NotParsed)
		{
			Console.WriteLine(HelpText.RenderUsageText(parserResult));
			return -1;
		}

		if (File.Exists(options.OutputFile) == true)
		{
			if (options.AllowOutputOverride == false)
			{
				Console.WriteLine($"Output file, {options.OutputFile}, already exits. Aborting. Specify --force to override");
				return -1;
			}

			Console.WriteLine($"Output file, {options.OutputFile}, already exists. Will be overriden");
		}

		foreach (var inputFilePath in options.InputFiles)
		{
			if (File.Exists(inputFilePath) == false)
			{
				Console.WriteLine($"Input file, {inputFilePath}, does not exist. Aborting.");
				return -1;
			}
		}

		List<ConcurrentDictionary<string, NodePathInfo>> nodePath = new List<ConcurrentDictionary<string, NodePathInfo>>();
		// Filter function; by default this does nothing, but if the user has specified any requried addresses change it to filter
		Func<NodePathInfo, bool> includeItem = (_) => true;
		if (options.MustMatchAddress.Any() == true)
		{
			includeItem = item =>
			{
				// If the address is null/empty it cannot possibly match our filter, early out
				if (string.IsNullOrWhiteSpace(item.Address) == true)
				{
					return false;
				}

				foreach (string addr in options.MustMatchAddress)
				{
					// If we match incldue the item
					if (item.Address.Contains(addr) == true)
					{
						return true;
					}
				}

				// No matched address, filter out.
				return false;
			};
		}

		foreach (string inputFilePath in options.InputFiles)
		{
			ConcurrentDictionary<string, NodePathInfo> nodes = new ConcurrentDictionary<string, NodePathInfo>();
			await AnalyseFileAsync(inputFilePath, nodes, includeItem);
			nodePath.Add(nodes);
		}

		List<(string filePath, NodePathInfo nodePathInfo)> combinedItems = CombineItems(options.InputFiles, nodePath);

		// Display some analysis
		Console.WriteLine("Merged file analysis:");
		foreach (var file in combinedItems.GroupBy(g => g.filePath))
		{
			Console.WriteLine($"\t{file.Key}");
			foreach ((string nodeType, int count) type in file.GroupBy(g => g.nodePathInfo.Name).Select(g => (g.Key, g.Count())))
			{
				Console.WriteLine($"\t\t{type.count:N0} {type.nodeType}(s)");
			}
		}

		if (options.ReorderItemsByDate == true)
		{
			Console.WriteLine("Reordering merged items by the date they were received");
			combinedItems = combinedItems
				.OrderBy(ci => ci.nodePathInfo.Date)
				.ToList();
		}

		await WriteMergedResultsAsync(options.OutputFile, combinedItems);

		return 0;
	}

	private static List<(string filePath, NodePathInfo nodePathInfo)> CombineItems(IList<string> inputFiles, List<ConcurrentDictionary<string, NodePathInfo>> fileNodes)
	{
		if (inputFiles.Count != fileNodes.Count)
		{
			throw new ArgumentException($"Mismatch of arguments; working with {inputFiles.Count} file(s) and {fileNodes.Count} mappings for those files. These should be the same.", nameof(fileNodes));
		}


		// We can start by taking the right-most file and using all of its results. Any duplicates will end up being from this file anyway
		string lastInputFile = inputFiles.Last();
		List<(string filePath, NodePathInfo nodePathInfo)> results = fileNodes.Last()
			.Where(kvp => kvp.Value.IsPrimaryId == true)
			.Select(kvp => (lastInputFile, kvp.Value))
			.ToList();
		// Extract all the last items ids and mark them as existing in our result set
		Dictionary<string, bool> mergedIds = fileNodes.Last()
			.ToDictionary(kvp => kvp.Key, kvp => true);

		// Match the files to their node paths and reverse the list
		List<(string inputFilePath, ConcurrentDictionary<string, NodePathInfo> nodes)> reversedPairs = inputFiles
			.Select((t, i) => (t, fileNodes[i]))
			.Reverse()
			.ToList();

		// We've reversed the list so our set of files is now ordered from most desirable to least desirable
		// We can now move through the list and for each file we deal with we know if an item already exists in our merged set then we can ignore it
		foreach (var file in reversedPairs.Skip(1))
		{
			foreach (NodePathInfo item in file.nodes.Values.Where(npi => npi.IsPrimaryId))
			{
				if (mergedIds.ContainsKey(item.PrimaryId) == true)
				{
					// We already have a matching item. Skip.
					continue;
				}
				// Check for alternate ids just in case
				bool foundByAlternate = false;
				foreach (var id in item.AlternateIds)
				{
					if (mergedIds.ContainsKey(id) == true)
					{
						foundByAlternate = true;
						break;
					}
				}

				if (foundByAlternate == true)
				{
					// Found via alternate id, skip
					continue;
				}

				// Haven't found a match - can take form this file
				results.Add((file.inputFilePath, item));

				// Mark all the ids as mregemerged
				mergedIds[item.PrimaryId] = true;
				foreach (var id in item.AlternateIds)
				{
					mergedIds[id] = true;
				}
			}
		}

		return results;
	}

	private static async Task WriteMergedResultsAsync(string outputFilePath, List<(string sourceFile, NodePathInfo nodePathInfo)> totalItems)
	{
		//using (MemoryMappedFile output = MemoryMappedFile.CreateFromFile(outputFilePath, FileMode.Create))
		using (FileStream output = File.Open(outputFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read))
		{
			Dictionary<string, MemoryMappedFile> sourceFiles = new Dictionary<string, MemoryMappedFile>();
			Dictionary<string, MemoryMappedViewStream> sourceStreams = new Dictionary<string, MemoryMappedViewStream>();
			foreach (string sourceFile in totalItems.DistinctBy(s => s.sourceFile).Select(s => s.sourceFile))
			{
				MemoryMappedFile file = MemoryMappedFile.CreateFromFile(sourceFile, FileMode.Open);
				sourceFiles[sourceFile] = file;
				sourceStreams[sourceFile] = file.CreateViewStream();

			}

			try
			{
				// TODO: Make generic
				string preamble = $@"
<?xml version='1.0' encoding='UTF-8' standalone='yes' ?>
<smses count=""{totalItems.Count}"" backup_set=""{Guid.NewGuid()}"" backup_date=""{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}"" type=""full"">
";
				byte[] preambleBytes = Encoding.UTF8.GetBytes(preamble);
				output.Write(preambleBytes);

				Memory<byte> buffer = new Memory<byte>(new byte[10_000_000]); // 10MB buffer

				foreach (var item in totalItems)
				{
					await WriteNodeToDestinationAsync(sourceStreams[item.sourceFile], item.nodePathInfo, output, buffer);

				}

				byte[] postambleBytes = Encoding.UTF8.GetBytes("</smses>");
				output.Write(postambleBytes);
			} finally
			{
				foreach (MemoryMappedViewStream view in sourceStreams.Values)
				{
					await view.DisposeAsync();
				}

				foreach (MemoryMappedFile file in sourceFiles.Values)
				{
					file.Dispose();
				}
			}
		}
	}

	private static async Task WriteNodeToDestinationAsync(Stream sourceStream, NodePathInfo node, Stream output, Memory<byte> buffer)
	{

		sourceStream.Position = node.Start.Offset;
		long bytesRemaining = node.ByteCount;
		while (bytesRemaining > 0)
		{
			Memory<byte> span = buffer.Slice(0, (int)Math.Min(bytesRemaining, buffer.Length));
			int read = await sourceStream.ReadAsync(span);

			await output.WriteAsync(span[..read]);
			bytesRemaining -= read;
		}
	}

	private static async Task AnalyseFileAsync(string filePath, ConcurrentDictionary<string, NodePathInfo> paths, Func<NodePathInfo, bool> includeItem)
	{
		Console.WriteLine($"Analysing {filePath}...");

		Stopwatch totalSw = Stopwatch.StartNew();

		using (MemoryMappedFile file = MemoryMappedFile.CreateFromFile(filePath))
		{
			ConcurrentDictionary<string, long> analysis = new ConcurrentDictionary<string, long>();

			await using (MemoryMappedViewStream view = file.CreateViewStream())
			{
				List<long> lineOffsets = BuildLineOffsets(view);

				XmlReaderSettings settings = new XmlReaderSettings()
				{
					Async = true,
					CloseInput = false, IgnoreComments = true, IgnoreProcessingInstructions = true,
					IgnoreWhitespace = true, ValidationType = ValidationType.None,
					ValidationFlags = XmlSchemaValidationFlags.None, CheckCharacters = true,
					DtdProcessing = DtdProcessing.Ignore,
				};

				string[] supportedTopLevelTags = new string[] { "smses", "allsms" };

				using (XmlReader reader = XmlReader.Create(view, settings))
				{
					try
					{
						Action<NodePathInfo> pushNodePath = (npi) =>
						{
							paths[npi.Id] = npi;
						};

						await reader.MoveToContentAsync();

						if (supportedTopLevelTags.Contains(reader.Name) == false)
						{
							// Unhandled file type
							throw new Exception($"Encountered a top level tag of <{reader.Name}> which is unsupported. Supported top level tags are; {string.Join(", ", supportedTopLevelTags.Select(t => $"<{t}>"))}. Aborting.");
						}

						Dictionary<string, Func<XmlReader, Task<NodePathInfo>>> analysisMap = new Dictionary<string, Func<XmlReader, Task<NodePathInfo>>>()
						{
							{ "mms", ExtractMmsInfoAsync },
							{ "sms", ExtractSmsInfoAsync }
						};

						// Skip rest of this <smses> node
						reader.Read();

						do
						{
							if (analysisMap.TryGetValue(reader.Name, out Func<XmlReader, Task<NodePathInfo>>? analyser) == false)
							{
								throw new Exception($"Encountered <{reader.Name}> at line {reader.AsLineInfo().LineNumber} (position {reader.AsLineInfo().LinePosition}) which we do not handle. Aborting.");
							}

							NodePathInfo result = await analyser(reader);
							if (result == null)
							{
								continue;
							}

							if (result.Start.LineNumber > lineOffsets.Count)
							{
								throw new Exception($"Node {reader.Name} occurs at line {result.Start.LineNumber} (position: {result.Start.LinePosition}) but we only know up to line {lineOffsets.Count} which occurs at {lineOffsets.Last()}");
							}
							if (result.End.LineNumber > lineOffsets.Count)
							{
								throw new Exception($"Node {reader.Name} finishes at line {result.End.LineNumber} (position: {result.Start.LinePosition}) but we only know up to line {lineOffsets.Count} which occurs at {lineOffsets.Last()}");
							}

							result.Start.Offset = lineOffsets[(int)result.Start.LineNumber] + (result.Start.LinePosition - 1);
							result.End.Offset = lineOffsets[(int)result.End.LineNumber] + (result.End.LinePosition - 1);

							if (includeItem(result) == false)
							{
								// Item has been excluded, continue
								continue;
							}

							pushNodePath(result);
							if (result.AlternateIds.Any())
							{
								foreach (string id in result.AlternateIds)
								{
									NodePathInfo alt = new NodePathInfo()
									{
										Name = result.Name,
										Id = id,
										PrimaryId = result.PrimaryId,
										Date = result.Date,
										Start = result.Start,
										End = result.End,
										// Composed of all the ids but this one
										AlternateIds = result.AlternateIds.Where(i => i != id).Union(new[] { result.PrimaryId }).ToList()!,
										Address = result.Address,
									};
									pushNodePath(alt);
								}
							}
						} while ((reader.EOF != true) && (reader.Name != "smses"));
					} catch (Exception ex)
					{
						if (reader.EOF == true)
						{
							Console.WriteLine("Reader found EOF.");
						}

						if (view.Position == view.Length)
						{
							Console.WriteLine("File has completed reading.");
						} else
						{
							Console.WriteLine($"At: {view.PointerOffset}: {ex}");
							throw;
						}
					}

					foreach (var key in analysis.Keys.OrderBy(o => o))
					{
						Console.WriteLine($"{key}: {analysis[key]}");
					}
				}
			}
		}

		Console.WriteLine($"Finished analysing {filePath} in {totalSw.Elapsed}. Discovered {paths.Count:N0} item(s)");
	}

	private static List<long> BuildLineOffsets(MemoryMappedViewStream view)
	{
		const byte CarriageReturn = 0x0d;
		const byte LineFeed = 0x0a;

		LinkedList<long> lineOffsetsStack = new LinkedList<long>();
		// Create two empty offsets to allow direct indexing to the results with 1 based offsets
		// [0] -> [0] can be ignored, there's no "line 0" to the XMLReader
		// [1] -> [0] points line "1" to byte 0 of the file
		LinkedListNode<long> node = lineOffsetsStack.AddFirst(0);
		node = lineOffsetsStack.AddAfter(node, 0);

		try
		{
			view.Position = 0;
			long offset = 0;
			Memory<byte> buffer = new Memory<byte>(new byte[100_000_000]);

			Stopwatch total = Stopwatch.StartNew();

			while (view.Position != view.Length)
			{
				Stopwatch sw = Stopwatch.StartNew();

				int read = view.Read(buffer.Span);
				int inspect = 0;

				while (inspect < read)
				{
					int index = buffer.Span[inspect..read].IndexOfAny(LineFeed, CarriageReturn);

					if (index > -1)
					{
						if (index + inspect > read)
						{
							Console.WriteLine("Searched past end of read block. Skipping.");
							break;
						}

						if ((buffer.Span[inspect + index] == CarriageReturn) && (buffer.Span[inspect + index + 1] == LineFeed))
						{
							inspect++;
						}

						node = lineOffsetsStack.AddAfter(node, offset + inspect + index);
						inspect += index + 1;
					} else
					{
						break;
					}
				}


				Console.WriteLine($"Took {sw.Elapsed} to process {read:N0} bytes from {view.Position:N0}");
				offset += read;
			}

			Console.WriteLine($"Took {total.Elapsed} to build {lineOffsetsStack.Count:N0} line offsets from {view.Position:N0} bytes of data");
		} finally
		{
			view.Position = 0;
		}

		return lineOffsetsStack.ToList();
	}

	private static Task<NodePathInfo> ExtractMmsInfoAsync(XmlReader reader)
	{
		if (reader.Name != "mms")
		{
			throw new ArgumentException($"Expected to find <mms> but instead found <{reader.Name}>. Aborting.");
		}

		IList<string> ids = GenerateIds(reader);

		NodePathInfo result = new NodePathInfo()
		{
			Id = ids.First(),
			IsPrimaryId = true,
			PrimaryId = ids.First(),
			// Composed of all the other ids
			AlternateIds = ids.Skip(1).ToList(),
			Name = reader.Name,
			Date = reader.GetAttributeAsLongWithFallback("date", "date_sent") ?? 0,
			Address = reader.GetAttribute("address")
		};

		IXmlLineInfo lineInfo = reader.AsLineInfo();
		result.Start.LineNumber = lineInfo.LineNumber;
		result.Start.LinePosition = lineInfo.LinePosition;

		// Skip to next item
		reader.ReadToNextSibling();

		lineInfo = reader.AsLineInfo();
		result.End.LineNumber = lineInfo.LineNumber;
		result.End.LinePosition = lineInfo.LinePosition - 1;

		return Task.FromResult(result);
	}

	private static Task<NodePathInfo> ExtractSmsInfoAsync(XmlReader reader)
	{
		if (reader.Name != "sms")
		{
			throw new ArgumentException($"Expected to find <sms> but instead found <{reader.Name}>. Aborting.");
		}

		var ids = GenerateIds(reader);
		NodePathInfo result = new NodePathInfo()
		{
			Id = ids.First(),
			IsPrimaryId = true,
			PrimaryId = ids.First(),
			// Composed of all the other ids
			AlternateIds = ids.Skip(1).ToList(),
			Name = reader.Name,
			Date = reader.GetAttributeAsLongWithFallback("date", "date_sent") ?? 0,
			Address = reader.GetAttribute("address")
		};
		result.Start.LineNumber = reader.AsLineInfo().LineNumber;
		result.Start.LinePosition = reader.AsLineInfo().LinePosition;

		// Skip to next item
		reader.ReadToNextSibling();
		result.End.LineNumber = reader.AsLineInfo().LineNumber;
		result.End.LinePosition = (reader.AsLineInfo().LinePosition - 1);

		return Task.FromResult(result);
	}


	public static IList<string> GenerateIds(XmlReader reader)
	{
		if (reader.Name == "sms")
		{
			string content = reader.GetAttribute("body") ?? string.Empty;
			string contentHash = "";
			if (string.IsNullOrWhiteSpace(content) == false)
			{
				using (SHA1 sha1 = SHA1.Create())
				{
					byte[] bytes = Encoding.UTF8.GetBytes(content);
					byte[] hashedBytes = sha1.ComputeHash(bytes, 0, bytes.Length);
					contentHash = Convert.ToBase64String(hashedBytes);
				}
			}

			return new[]
			{
				$"SMS=1_D={reader.GetAttribute("date")}_DS={reader.GetAttribute("date_sent")}_A={reader.GetAttribute("address")}_P={reader.GetAttribute("protocol")}_T={reader.GetAttribute("type")}_C={contentHash}"
			};
		}

		if (reader.Name == "mms")
		{
			string commonAttributes = $"MMS=1_D={reader.GetAttribute("date")}_DS={reader.GetAttribute("date_sent")}_MB={reader.GetAttributeNullable("msg_box")}_A={reader.GetAttributeNullable("address")}";

			List<string> ids = new List<string>();
			string? m_id = reader.GetAttributeNullable("m_id");
			if (string.IsNullOrWhiteSpace(m_id) == false)
			{
				ids.Add($"{commonAttributes}_MID={m_id}");
			}

			string? transaction_id = reader.GetAttributeNullable("tr_id");
			if (string.IsNullOrWhiteSpace(transaction_id) == false)
			{
				ids.Add($"{commonAttributes}_TRID={transaction_id}");
			}

			//return ids;
			if (ids.Any() == true)
			{
				return ids;
			}
		}

		return new[] { reader.Value };
	}
}