using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Threading.Tasks;
using System.Diagnostics.Contracts;

namespace BibleMarkdown
{
	partial class Program
	{

		static void ImportFromUSFM(string mdpath, string srcpath)
		{
			var sources = Directory.EnumerateFiles(srcpath)
				.Where(file => file.EndsWith(".usfm"));

			if (sources.Any())
			{

				//var mdtimes = Directory.EnumerateFiles(mdpath)
				//	.Select(file => File.GetLastWriteTimeUtc(file));
				//var sourcetimes = sources.Select(file => File.GetLastWriteTimeUtc(file));

				//var mdtime = DateTime.MinValue;
				//var sourcetime = DateTime.MinValue;

				//foreach (var time in mdtimes) mdtime = time > mdtime ? time : mdtime;
				//foreach (var time in sourcetimes) sourcetime = time > sourcetime ? time : sourcetime;

				if (FromSource)
				{
					Imported = true;

					int bookno = 1;

					foreach (var source in sources)
					{
						var src = File.ReadAllText(source);
						var bookm = Regex.Matches(src, @"(\\h|\\toc1|\\toc2|\\toc3)\s+(.*?)$", RegexOptions.Multiline)
							.Select(m => m.Groups[2].Value.Trim())
							.OrderBy(b => b.Length)
							.ThenBy(b => b.Count(ch => char.IsUpper(ch)))
							.ToArray();
						var book = bookm
							.FirstOrDefault();

						var namesfile = Path.Combine(mdpath, "src", "bnames.xml");
						var useNames = File.Exists(namesfile);

						XElement[] xmlbooks = new XElement[0];

						if (useNames) {
							using (var stream = File.Open(namesfile, FileMode.Open, FileAccess.Read))
							{
								xmlbooks = XElement.Load(stream)
									.Elements("ID")
									.SelectMany(x => x.Elements("BOOK"))
									.ToArray();
							}
						}

						var books = xmlbooks
							.Select(x => new
							{
								Book = x.Value,
								Abbreviation = (string)x.Attribute("bshort"),
								Number = (int)x.Attribute("bnumber")
							})
							.ToArray();

						if (useNames)
						{
							var book2 = books.FirstOrDefault(b => bookm.Any(bm => b.Book == bm));
							if (book2 != null) bookno = book2.Number;
						}

						src = Regex.Match(src, @"\\c\s+[0-9]+.*", RegexOptions.Singleline).Value; // remove header that is not content of a chapter

						src = src.Replace("\r", "").Replace("\n", ""); // remove newlines

						src = Regex.Replace(src, @"(?<=\\c\s+[0-9]+\s*(\\s[0-9]+\s+[^\\]*?)?)\\p", ""); // remove empty paragraph after chapter

						src = src.Replace("[", "\\[").Replace("]", "\\]"); // escape [ and ]

						src = Regex.Replace(src, @"\\m?s(?<level>[0-9]+)\s*(?<text>[^\\$]+)", m => // section titles
						{
							int n = 1;
							int.TryParse(m.Groups["level"].Value, out n);
							n++;
							return $"{new String('#', n)} {m.Groups["text"].Value.Trim()}{Environment.NewLine}";
						}, RegexOptions.Singleline);

						bool firstchapter = true;
						src = Regex.Replace(src, @"\\c\s+([0-9]+\s*)", m => // chapters
						{
							var res = firstchapter ? $"# {m.Groups[1].Value}{Environment.NewLine}" : $"{Environment.NewLine}{Environment.NewLine}# {m.Groups[1].Value}{Environment.NewLine}";
							firstchapter = false;
							return res;
						});

						src = Regex.Replace(src, @"\\v\s+([0-9]+)", "^$1^"); // verse numbers

						// footnotes
						int n = 0;
						bool replaced;
						do
						{
							replaced = false;
							src = Regex.Replace(src, @"(?<=(?<dotbefore>[.:;?!¿¡])?)\\(?<type>[fx])\s*[+-?]\s*(?<footnote>.*?)\\\k<type>\*(?=(?<spaceafter>\s)?)(?<body>.*?(?=\s*#|\\p|$))", m =>
							{
								var space = n == 0 ? Environment.NewLine : " ";
								var spacebefore = m.Groups["dotbefore"].Success ? "" : "";
								var spaceafter = m.Groups["spaceafter"].Success ? "" : " ";
								replaced = true;
								return $"{spacebefore}^{Label(n)}^{spaceafter}{m.Groups["body"].Value}{space}^{Label(n)}^[{m.Groups["footnote"].Value}]";
							}, RegexOptions.Singleline);
							n++;
						} while (replaced);

						src = Regex.Replace(src, @"\\p *", $"{Environment.NewLine}{Environment.NewLine}"); // replace new paragraph with empty line
						src = Regex.Replace(src, @"\|([a-zA-Z-]+=""[^""]*""\s*)+", ""); // remove word attributes
						src = Regex.Replace(src, @"\\\+?\w+(\*|\s*)?", ""); // remove usfm tags
						src = Regex.Replace(src, @" +", " "); // remove multiple spaces
						src = Regex.Replace(src, @"\^\[([0-9]+)[.:]([0-9]+)", "^[**$1:$2**"); // bold verse references in footnotes
						src = Regex.Replace(src, @"(\.|\?|!|;|:|,)(\w|“|¿|¡)", "$1 $2"); // Add space after dot
						src = Regex.Replace(src, @"(?<!^|(r?\n\r?\n)|#)#(?!#)", $"{Environment.NewLine}#", RegexOptions.Singleline); // add blank line over title
						if (LowercaseFirstWords) // needed for ReinaValera1909, it has uppercase words on every beginning of a chapter
						{
							src = Regex.Replace(src, @"(\^1\^ \w)(\w*)", m => $"{m.Groups[1].Value}{m.Groups[2].Value.ToLower()}");
							src = Regex.Replace(src, @"(\^1\^ \w )(\w*)", m => $"{m.Groups[1].Value}{m.Groups[2].Value.ToLower()}");
						}

						src = Regex.Replace(src, @"\^[0-9]\^(?=\s*(\^[0-9]+\^|#|$))", "", RegexOptions.Singleline); // remove empty verses
						src = Regex.Replace(src, @"(?<!\s|^)(\^[0-9]+\^)", " $1", RegexOptions.Singleline);

						var md = Path.Combine(mdpath, $"{bookno:D2}-{book}.md");
						bookno++;
						File.WriteAllText(md, src);
						Log(md);
					}

				}

			}
		}

		public static void ImportFromBibleEdit(string srcpath)
		{
			var root = Path.Combine(srcpath, "bibleedit");
			if (FromSource && Directory.Exists(root))
			{
				Console.WriteLine("Import from BibleEdit");

				var oldfiles = Directory.EnumerateFiles(srcpath, "*.usfm");
				foreach (var of in oldfiles) File.Delete(of);

				var folders = Directory.EnumerateDirectories(root).ToArray();
				if (folders.Length == 1) folders = Directory.EnumerateDirectories(Path.Combine(folders[0])).ToArray();

				var namesfile = Path.Combine(srcpath, "bnames.xml");
				XElement[] books;
				using (var stream = File.Open(namesfile, FileMode.Open, FileAccess.Read))
				{
					books = XElement.Load(stream)
						.Elements("ID")
						.SelectMany(id => id.Elements("BOOK"))
						.ToArray();
				}

				int fileno = 1;
				
				foreach (string folder in folders)
				{
					var chapters = Directory.EnumerateDirectories(folder).ToArray();
					int i = 0;
					chapters= chapters.OrderBy(f =>
					{
						var name = Path.GetFileName(f);
						int n;
						if (!int.TryParse(name, out n)) n = i;
						i++;
						return n;
					})
					.ToArray();

					var files = chapters
						.Select(ch => File.ReadAllText(Path.Combine(ch, "data")))
						.ToArray();

					var bookm = Regex.Matches(files[0], @"(\\h|\\toc1|\\toc2|\\toc3)\s+(.*?)$", RegexOptions.Multiline)
						.Select(m => m.Groups[2].Value.Trim())
						.ToArray();
					var bookxml = books
						.Where(e => bookm.Any(b => string.Compare(e.Value, b, true) == 0))
						.FirstOrDefault();
					int index = fileno++;
					string book = bookm.FirstOrDefault();

					if (bookxml != null)
					{
						index = ((int)bookxml.Attribute("bnumber"));
						book = bookxml.Value.Trim();
					}

					var txt = new StringBuilder();
					foreach (string file in files)
					{
						txt.AppendLine(file);
					}

					var usfmfile = Path.Combine(srcpath, $"{index:d2}-{book}.usfm");
					File.WriteAllText(usfmfile, txt.ToString());
					Log(usfmfile);
				}
			}
		}
		public static void ImportFromTXT(string mdpath, string srcpath)
		{
			var sources = Directory.EnumerateFiles(srcpath)
				.Where(file => file.EndsWith(".txt"));

			if (sources.Any())
			{

				//var mdtimes = Directory.EnumerateFiles(mdpath)
				//	.Select(file => File.GetLastWriteTimeUtc(file));
				//var sourcetimes = sources.Select(file => File.GetLastWriteTimeUtc(file));

				//var mdtime = DateTime.MinValue;
				//var sourcetime = DateTime.MinValue;

				//foreach (var time in mdtimes) mdtime = time > mdtime ? time : mdtime;
				//foreach (var time in sourcetimes) sourcetime = time > sourcetime ? time : sourcetime;

				if (FromSource)
				{
					Imported = true;

					int bookno = 1;
					string book = null;
					string chapter = null;
					string md;

					var s = new StringBuilder();
					foreach (var source in sources)
					{
						var src = File.ReadAllText(source);
						var matches = Regex.Matches(src, @"(?:^|\n)(.*?)\s*([0-9]+):([0-9]+)(.*?)(?=$|\n[^\n]*?[0-9]+:[0-9]+)", RegexOptions.Singleline);

						foreach (Match m in matches)
						{
							var bk = m.Groups[1].Value;
							if (book != bk)
							{
								if (book != null)
								{
									md = Path.Combine(mdpath, $"{bookno++:D2}-{book}.md");
									File.WriteAllText(md, s.ToString());
									Log(md);
									s.Clear();
								}
								book = bk;
							}

							var chap = m.Groups[2].Value;
							if (chap != chapter)
							{
								chapter = chap;
								if (chapter != "1")
								{
									s.AppendLine();
									s.AppendLine();
								}
								s.AppendLine($"# {chapter}");
							}

							string verse = m.Groups[3].Value;
							string text = Regex.Replace(m.Groups[4].Value, @"\r?\n", " ").Trim();
							s.Append($"{(verse == "1" ? "" : " ")}^{verse}^ {text}");
						}
					}
					md = Path.Combine(mdpath, $"{bookno++:D2}-{book}.md");
					File.WriteAllText(md, s.ToString());
					Log(md);
				}
			}
		}

		public static void ImportFromZefania(string mdpath, string srcpath)
		{
			var sources = Directory.EnumerateFiles(srcpath)
				.Where(file => file.EndsWith(".xml"));

			//var mdtimes = Directory.EnumerateFiles(mdpath)
			//	.Select(file => File.GetLastWriteTimeUtc(file));
			//var sourcetimes = sources.Select(file => File.GetLastWriteTimeUtc(file));

			//var mdtime = DateTime.MinValue;
			//var sourcetime = DateTime.MinValue;

			//foreach (var time in mdtimes) mdtime = time > mdtime ? time : mdtime;
			//foreach (var time in sourcetimes) sourcetime = time > sourcetime ? time : sourcetime;

			if (FromSource)
			{

				foreach (var source in sources)
				{
					using (var stream = File.Open(source, FileMode.Open, FileAccess.Read))
					{
						var root = XElement.Load(stream);

						foreach (var book in root.Elements("BIBLEBOOK"))
						{
							Imported = true;

							StringBuilder text = new StringBuilder();
							var file = $"{((int)book.Attribute("bnumber")):D2}-{(string)book.Attribute("bname")}.md";
							var firstchapter = true;

							foreach (var chapter in book.Elements("CHAPTER"))
							{
								if (!firstchapter)
								{
									text.AppendLine(""); text.AppendLine();
								}
								firstchapter = false;
								text.Append($"# {((int)chapter.Attribute("cnumber"))}{Environment.NewLine}");
								var firstverse = true;

								foreach (var verse in chapter.Elements("VERS"))
								{
									if (!firstverse) text.Append(" ");
									firstverse = false;
									text.Append($"^{((int)verse.Attribute("vnumber"))}^ ");
									text.Append(verse.Value);
								}
							}

							var md = Path.Combine(mdpath, file);
							File.WriteAllText(md, text.ToString());
							Log(md);

						}
					}
				}
			}
		}
		static void ImportFramework(string path)
		{
			var frmfile = Path.Combine(path, "src", "framework.md");

			if (File.Exists(frmfile))
			{

				var mdfiles = Directory.EnumerateFiles(path, "*.md")
					.Where(file => Regex.IsMatch(Path.GetFileName(file), "^([0-9][0-9])"));

				var mdtimes = mdfiles.Select(file => File.GetLastWriteTimeUtc(file));
				var frmtime = File.GetLastWriteTimeUtc(frmfile);

				var frame = File.ReadAllText(frmfile);
				frame = Regex.Replace(frame, "%(!=!).*?%", "", RegexOptions.Singleline); // remove comments

				var namesfile = Path.Combine(path, "src", "bnames.xml");
				var linklistfile = Path.Combine(path, "src", "linklist.xml");

				bool MapVerses = false;
				string language = "english";
				frame = Regex.Replace(frame, @"%!map-verses (?<language>[a-zA-Z0-9]+).*?%\r?\n?", m => {
					MapVerses = true;
					language = m.Groups["language"].Value;
					return "";
				}, RegexOptions.Singleline);
				XElement[] bnames;
				using (var stream = File.Open(namesfile, FileMode.Open, FileAccess.Read))
				{
					bnames = XElement.Load(stream)
						.Elements("ID")
						.Where(id => ((string)id.Attribute("descr")) == language)
						.FirstOrDefault()
						.Elements("BOOK")
						.ToArray();
				}

				if (FromSource || Imported)
				{
					Log(frmfile, "Importing");

					foreach (string srcfile in mdfiles)
					{

						File.SetLastWriteTimeUtc(srcfile, DateTime.Now);
						var src = File.ReadAllText(srcfile);
						var srcname = Path.GetFileName(srcfile);
						string book = Path.GetFileNameWithoutExtension(Regex.Replace(srcname, @"[0-9]+\.?[0-9]*-", ""));

						var frmpartmatch = Regex.Match(frame, $@"(?<=(^|\n)# {srcname}\r?\n).*?(?=\n# |$)", RegexOptions.Singleline);
						if (frmpartmatch.Success)
						{

							// remove current frame

							// remove bibmark footnotes.
							bool replaced = true;
							while (replaced)
							{
								replaced = false;
								src = Regex.Replace(src, @"\^(?<mark>[a-zA-Z]+)\^(?!\[)(?<text>.*?)(?:\^\k<mark>(?<footnote>\^\[.*?(?<!\\)\]))[ \t]*\r?\n?", m =>
								{
									replaced = true;
									return $"{m.Groups["footnote"].Value}{m.Groups["text"].Value}";
								}, RegexOptions.Singleline);
							}
							src = Regex.Replace(src, @"(?<=\r?\n|^)\r?\n(?!\s*#)", "", RegexOptions.Singleline); // remove blank line
							src = Regex.Replace(src, @"(?<!(^|\n)#[^\n]*)\r?\n(?!\s*#)", " ", RegexOptions.Singleline);
							src = Regex.Replace(src, " +", " ");


							src = Regex.Replace(src, @"(?<=^|\n)##+.*?\r?\n", "", RegexOptions.Singleline); // remove titles
																																	  // src = Regex.Replace(src, @"(\s*\^[a-zA-Z]+\^)|(([ \t]*\^[a-zA-Z]+\^\[[^\]]*\])+([ \t]*\r?\n)?)", "", RegexOptions.Singleline); // remove footnotes
							src = Regex.Replace(src, @"%!verse-paragraphs.*?%\r?\n?", "", RegexOptions.Singleline); // remove verse paragraphs

							var frmpart = frmpartmatch.Value;
							var frames = Regex.Matches(frmpart, @"(?<=(^|\n)## (?<chapter>[0-9]+)(?:\r?\n|$).*?)\^(?<verse>[0-9]+)\^(?<versecontent>(\s*((?<marker>\^[a-zA-Z]+\^(?!\[))|(?<footnote>\^[a-zA-Z]+\^\[[^\]]*\])))*\s*(?:(?:\r?\n#(?<titlelevel>#+)\s*(?<title>.*?)(\r?\n|$))|\\|(?=\^[0-9]+\^)))", RegexOptions.Singleline).GetEnumerator();
							var hasFrame = frames.MoveNext();

							var m = Regex.Match(frmpart, "%!verse-paragraphs.*?%");
							if (m.Success) src = $"{m.Value}{Environment.NewLine}{src}";

							int chapter = 0;
							int verse = 0;

							src = Regex.Replace(src, @"(?<=^|\n)#[ \t]+(?<chapter>[0-9]+)(\s*\r?\n|$)|\^(?<verse>[0-9]+)\^.*?(?=\^[0-9]+\^|\s*#|\s*$)|(?<=(^|\n)#[ \t]+[0-9]+[ \t]*\r?\n(##[ \t]+.*?\r?\n)?)(?<empty>.*?)(?=\^[0-9]+\^|\s*#|\s*$)", m =>
							{
								var txt = m.Value;

								if (m.Groups["chapter"].Success) // chapter
								{
									int.TryParse(m.Groups["chapter"].Value, out chapter); verse = 0;
								}
								else if (m.Groups["verse"].Success) // verse
								{
									int.TryParse(m.Groups["verse"].Value, out verse);
								}

								if (hasFrame)
								{
									var f = (Match)frames.Current;
									int fchapter = 0;
									int fverse = 0;
									int.TryParse(f.Groups["chapter"].Value, out fchapter);
									int.TryParse(f.Groups["verse"].Value, out fverse);

									var loc = new Location
									{
										Book = book,
										Chapter = fchapter,
										Verse = fverse
									};
									if (MapVerses) loc = Verses.Titles.Map(loc);

									if (loc.Chapter <= chapter && loc.Verse <= verse)
									{
										hasFrame = frames.MoveNext();

										if (f.Groups["title"].Success && f.Groups["titlelevel"].Value != "#")
											if (m.Groups["chapter"].Success)
											{
												return $"{txt}## {f.Groups["title"].Value}{Environment.NewLine}";
											}
											else
											{
												return $"{txt}{Environment.NewLine}{Environment.NewLine}## {f.Groups["title"].Value}{Environment.NewLine}"; // add title
											}
									//if (hasFoots) res.AppendLine();
										return txt;
									}
								}

								return txt;
							}, RegexOptions.Singleline);

							chapter = 0;
							verse = 0;
							frames.Reset();
							hasFrame = frames.MoveNext();
							src = Regex.Replace(src, @"(?<=^|\n)#[ \t]+(?<chapter>[0-9]+)(\s*\r?\n|$)|\^(?<verse>[0-9]+)\^.*?(?=\^[0-9]+\^|\s*#|\s*$)|(?<=(^|\n)#[ \t]+[0-9]+[ \t]*\r?\n(##[ \t]+.*?\r?\n)?)(?<empty>.*?)(?=\^[0-9]+\^|\s*#|\s*$)", m =>
							{
								var txt = m.Value;

								if (m.Groups["chapter"].Success) // chapter
								{
									int.TryParse(m.Groups["chapter"].Value, out chapter); verse = 0;
								}
								else if (m.Groups["verse"].Success) // verse
								{
									int.TryParse(m.Groups["verse"].Value, out verse);
								}

								if (hasFrame)
								{
									var f = (Match)frames.Current;
									int fchapter = 0;
									int fverse = 0;
									int.TryParse(f.Groups["chapter"].Value, out fchapter);
									int.TryParse(f.Groups["verse"].Value, out fverse);

									var loc = new Location
									{
										Book = book,
										Chapter = fchapter,
										Verse = fverse
									};
									if (MapVerses) loc = Verses.Footnotes.Map(loc);

									if (loc.Chapter <= chapter && loc.Verse <= verse)
									{
										hasFrame = frames.MoveNext();

										var res = new StringBuilder(txt);
										if (f.Groups["marker"].Success)
										{
											var markers = Regex.Matches(f.Groups["versecontent"].Value, @"\^[a-zA-Z]+\^(?!\[)");
											foreach (Match marker in markers)
											{
												if (txt.Length == 0 || !char.IsWhiteSpace(txt[txt.Length - 1])) res.Append(" ");

												res.Append($"{marker.Value} ");
											}
										}
										var foots = Regex.Matches(f.Groups["versecontent"].Value, @"\^[a-zA-Z]+\^(?<footbody>\[(?<foottext>[^\]]*)\])");
										bool hasFoots = false;
										foreach (Match foot in foots)
										{
											if (hasFoots) res.Append(" ");
											else res.AppendLine();

											var footer = Regex.Replace(foot.Value, @"(?<book>\w+)\s+(?<chapter>[0-9]+):(?<verse>[0-9]+)(?:-(?<tochapter>[0-9]+)(?<toverse>[0-9]+))", m =>
											{
												var booksname = m.Groups["book"].Value;
												var book = bnames?.FirstOrDefault(x => (string)x.Attribute("bshort") == booksname);
												var bookname = book?.Value;
												var from = new Location
												{
													Book = bookname,
													Chapter = int.Parse(m.Groups["chapter"].Value),
													Verse = int.Parse(m.Groups["verse"].Value)
												};
												if (MapVerses) from = Verses.Footnotes.Map(from);

												int tochapter = -1, toverse = -1;

												var str = new StringBuilder($"{booksname} {from.Chapter}:{from.Verse}");
												if (m.Groups["tochapter"].Success)
												{
													tochapter = int.Parse(m.Groups["tochapter"].Value);
													if (m.Groups["toverse"].Success)
													{
														toverse = int.Parse(m.Groups["toverse"].Value);
														str.Append($":{toverse}");
														var to = new Location
														{
															Book = bookname,
															Chapter = tochapter,
															Verse = toverse
														};
														if (MapVerses) to = Verses.Footnotes.Map(to);
														str.Append($"-{to.Chapter}:{to.Verse}");
													}
													else
													{
														var to = new Location
														{
															Book = bookname,
															Chapter = from.Chapter,
															Verse = tochapter
														};
														if (MapVerses) to = Verses.Footnotes.Map(to);
														if (to.Chapter == from.Chapter) str.Append($"-{to.Verse}");
														else str.Append($"-{to.Chapter}:{to.Verse}");
													}
												}

												return str.ToString();

											}, RegexOptions.Singleline);

											res.Append(footer);
											hasFoots = true;
										}
										return res.ToString();
									}
								}

								return txt;
							}, RegexOptions.Singleline);

							chapter = 0;
							verse = 0;
							frames.Reset();
							hasFrame = frames.MoveNext();
							src = Regex.Replace(src, @"(?<=^|\n)#[ \t]+(?<chapter>[0-9]+)(\s*\r?\n|$)|\^(?<verse>[0-9]+)\^.*?(?=\^[0-9]+\^|\s*#|\s*$)|(?<=(^|\n)#[ \t]+[0-9]+[ \t]*\r?\n(##[ \t]+.*?\r?\n)?)(?<empty>.*?)(?=\^[0-9]+\^|\s*#|\s*$)", m =>
							{
								var txt = m.Value;

								if (m.Groups["chapter"].Success) // chapter
								{
									int.TryParse(m.Groups["chapter"].Value, out chapter); verse = 0;
								}
								else if (m.Groups["verse"].Success) // verse
								{
									int.TryParse(m.Groups["verse"].Value, out verse);
								}

								if (hasFrame)
								{
									var f = (Match)frames.Current;
									int fchapter = 0;
									int fverse = 0;
									int.TryParse(f.Groups["chapter"].Value, out fchapter);
									int.TryParse(f.Groups["verse"].Value, out fverse);

									var loc = new Location
									{
										Book = book,
										Chapter = fchapter,
										Verse = fverse
									};
									if (MapVerses) loc = Verses.Paragraphs.Map(loc);

									if (loc.Chapter <= chapter && loc.Verse <= verse)
									{
										hasFrame = frames.MoveNext();

										if (f.Groups["versecontent"].Value.Contains("\\")) return $"{txt}{Environment.NewLine}{Environment.NewLine}";
										else return txt;
									}
								}

								return txt;
							}, RegexOptions.Singleline);

							// remove bibmark footnotes
							replaced = true;
							while (replaced)
							{
								replaced = false;
								src = Regex.Replace(src, @"\^(?<mark>[a-zA-Z]+)\^(?!\[)(?<text>.*?)[ \t]*(?:\^\k<mark>(?<footnote>\^\[.*?(?<!\\)\]))[ \t]*\r?\n?", m =>
								{
									replaced = true;
									return $"{m.Groups["footnote"].Value}{m.Groups["text"].Value}";
								}, RegexOptions.Singleline);
							}

							// apply bibmark footnotes
							replaced = true;
							int markno = 1;
							while (replaced)
							{
								replaced = false;
								src = Regex.Replace(src, @"(?<!\^[a-zA-Z]+)\^\[(?<footnote>.*?)(?<!\\)\](?<text>.*?)(?=\r?\n[ \t]*\r?\n)", m =>
								{
									replaced = true;
									string space;
									if (markno == 1) space = "\r\n"; else space = " ";
									return $"^{Marker(markno)}^{m.Groups["text"].Value}{space}^{Marker(markno)}^[{m.Groups["footnote"].Value}]";
								}, RegexOptions.Singleline);
								markno++;
							}

							File.WriteAllText(srcfile, src);
							Log(srcfile);
						}
					}
				}
			}
		}

		public class Verses : Dictionary<string, SortedList<Location, Location>>
		{
			public static Verses ParallelVerses = new Verses();
			public static Verses Paragraphs = new Verses();
			public static Verses Titles = new Verses();
			public static Verses Footnotes = new Verses();
			public static Verses DualLanguage = new Verses();

			public Location Map(Location verse)
			{
				SortedList<Location, Location> book;
				if (!TryGetValue(verse.Book, out book)) return verse;
				int i = 0, j = book.Count, m = 0;
				Location key = null, dest;
				while (i != j)
				{
					m = i + j / 2;
					key = book.Keys[m];
					if (key.Chapter > verse.Chapter || key.Chapter == verse.Chapter && key.Verse > verse.Verse)
					{
						j = m;
					}
					else
					{
						i = m;
					}
				}
				dest = book.Values[m];
				if (key == null) return verse;
				if ((key.Chapter <= verse.Chapter || key.Chapter == verse.Chapter && key.Verse <= verse.Verse))
				{
					var loc = new Location
					{
						Book = verse.Book,
						Chapter = verse.Chapter - key.Chapter + dest.Chapter,
						Verse = verse.Verse - key.Verse + dest.Verse,
					};
					if (loc.Chapter != verse.Chapter || loc.Verse != verse.Verse)
					{
						Console.WriteLine($"Verse mapped from {verse.Book} {verse.Chapter}:{verse.Verse} to {loc.Chapter}:{loc.Verse}.");
					}
					return loc;
				}
				else
				{
					return verse;
				}
			}


			public void Import(string mapfile)
			{
				if (!File.Exists(mapfile)) return;

				Clear();

				var src = File.ReadAllText(mapfile);
				var matches = Regex.Matches(mapfile, @"^#\s+(?<book>.*?)$(^(?<map>[0-9]+:[0-9]+=>[0-9]+:[0-9]+\s+)*$)?\s*", RegexOptions.Multiline);
				foreach (Match match in matches)
				{
					var book = match.Groups["book"].Value;
					if (match.Groups["map"].Success)
					{
						var list = new SortedList<Location, Location>();
						Add(book, list);

						var map = Regex.Matches(match.Groups["map"].Value, @"(?<keychapter>[0-9]+):(?<keyverse>[0-9]+)=>(?<destchapter>[0-9]+):(?<destverse>[0-9]+)", RegexOptions.Singleline);
						foreach (Match edge in map)
						{
							var key = new Location
							{
								Book = book,
								Chapter = int.Parse(edge.Groups["keychapter"].Value),
								Verse = int.Parse(edge.Groups["keyverse"].Value),
							};
							var dest = new Location
							{
								Book = book,
								Chapter = int.Parse(edge.Groups["destchapter"].Value),
								Verse = int.Parse(edge.Groups["destverse"].Value),
							};
							list.Add(key, dest);
						}
					}
				}
			}

		}
		public class Location
		{
			public string Book = "";
			public int Chapter;
			public int Verse;
		}
	}
}
