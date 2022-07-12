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

						var namesfile = Path.Combine(mdpath, "src", "booknames.xml");
						var useNames = File.Exists(namesfile);

						XElement[] xmlbooks = new XElement[0];

						if (useNames)
						{
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
								var foottxt = m.Groups["footnote"].Value;
								foottxt = Regex.Replace(foottxt, @"([0-9]+)[:,]\s+([0-9]+)", "$1:$2", RegexOptions.Singleline);
								return $"{spacebefore}^{Label(n)}^{spaceafter}{m.Groups["body"].Value}{space}^{Label(n)}^[{foottxt}]";
							}, RegexOptions.Singleline);
							n++;
						} while (replaced);

						src = Regex.Replace(src, @"\\p[ \t]*", $"{Environment.NewLine}{Environment.NewLine}"); // replace new paragraph with empty line
						src = Regex.Replace(src, @"\|([a-zA-Z-]+=""[^""]*""\s*)+", ""); // remove word attributes
						src = Regex.Replace(src, @"\\\+?\w+(\*|[ \t]*)?", "", RegexOptions.Singleline); // remove usfm tags
						src = Regex.Replace(src, @" +", " "); // remove multiple spaces
						src = Regex.Replace(src, @"\^\[([0-9]+)[.,:]([0-9]+)", "^[**$1:$2**"); // bold verse references in footnotes
						src = Regex.Replace(src, @"(\.|\?|!|;|(?<![0-9]+):(?![0-9]+)|,)(\w|“|¿|¡)", "$1 $2"); // Add space after dot
						src = Regex.Replace(src, @"(?<!^|(?:^|\n)[ \t]*\r?\n|(?:^|\n)#+[ \t]+[^\n]*\n)#", $"{Environment.NewLine}#", RegexOptions.Singleline); // add blank line over title
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

				var namesfile = Path.Combine(srcpath, "booknames.xml");
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
					chapters = chapters.OrderBy(f =>
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

		static void ReadMDFrameworkItems(string filename, List<FrameworkItem> items)
		{
			items.Clear();

			var frame = File.ReadAllText(filename);

			frame = Regex.Replace(frame, "%(?!!).*?%", "", RegexOptions.Singleline); // remove comments

			var mapVerses = Regex.IsMatch(frame, @"%!map-verses\s*%", RegexOptions.Singleline);
			MapVerses = mapVerses;

			var books = Regex.Matches(frame, @"(^|\n)#\s+(?<book>.*?)[ \t]*\n(?<bookbody>.*?)(?:\r?\n#\s|$)", RegexOptions.Singleline)
				.Select(match => new
				{
					Name = Books.Name(match.Groups["book"].Value),
					Body = match.Groups["bookbody"].Value,
					Book = Books["default", Books.Name(match.Groups["book"].Value)]
				});
			foreach (var book in books)
			{
				var bookItem = new BookItem(book.Book, book.Name)
				{
					MapVerses = mapVerses,
					VerseParagraphs = Regex.IsMatch(book.Body, @"%!verse-paragraphs\s*%", RegexOptions.Singleline)
				};
				items.Add(bookItem);

				var chapters = Regex.Matches(book.Body, @"(^|\n)##\s+(?<chapter>[0-9]+).*?\r?\n(?<chapterbody>.*?)(?:\r?\n##\s|$)", RegexOptions.Singleline)
					.Select(match => new
					{
						Chapter = int.Parse(match.Groups["chapter"].Value),
						Body = match.Groups["chapterbody"].Value
					});
				foreach (var chapter in chapters)
				{
					var chapterItem = new ChapterItem(book.Book, chapter.Chapter);
					items.Add(chapterItem);
					bookItem.Items.Add(chapterItem);

					var tokens = Regex.Matches(chapter.Body, @"\^(?<verse>[0-9]+)\^.*?((?<paragraph>\\)|(?<footnote>\^\[.*?\])|(?<=\n)###\s+(?<title>.*?)(\r?\n|$))", RegexOptions.Singleline)
						.Select(match => new
						{
							Verse = int.Parse(match.Groups["verse"].Value),
							Class = match.Groups["paragraph"].Success ?
								FrameworkItemClass.Paragraph : match.Groups["footnotes"].Success ?
									FrameworkItemClass.Footnote : FrameworkItemClass.Title,
							Footnote = match.Groups["footnotes"].Success ? match.Groups["footnotes"].Value : "",
							Title = match.Groups["title"].Success ? match.Groups["title"].Value : ""
						});

					foreach (var token in tokens)
					{
						switch (token.Class)
						{
							case FrameworkItemClass.Title:
								var titleItem = new TitleItem(book.Book, token.Title, chapterItem.Chapter, token.Verse);
								items.Add(titleItem);
								bookItem.Items.Add(titleItem);
								break;
							case FrameworkItemClass.Footnote:
								var footnoteItem = new FootnoteItem(book.Book, token.Footnote, chapterItem.Chapter, token.Verse);
								items.Add(footnoteItem);
								bookItem.Items.Add(footnoteItem);
								break;
							case FrameworkItemClass.Paragraph:
								var paragraphItem = new ParagraphItem(book.Book, chapterItem.Chapter, token.Verse);
								items.Add(paragraphItem);
								bookItem.Items.Add(paragraphItem);
								break;
						}
					}
				}
			}
			items.Sort((a, b) => Location.Compare(a.Location, b.Location));
		}

		static void ReadXmlFrameworkItems(string filename, List<FrameworkItem> items)
		{
			items.Clear();

			XElement frame;
			using (var file = File.Open(filename, FileMode.Open, FileAccess.Read))
			{
				frame = XElement.Load(file);
			}
			MapVerses = frame.Attribute("MapVerses") != null ? (bool)frame.Attribute("MapVerses") : false;

			foreach (XElement file in frame.Elements("Book"))
			{
				var bookname = (string)file.Attribute("Name");
				var book = Books["default", bookname];

				BookItem bookItem = new BookItem(book, (string)file.Attribute("File"));
				items.Add(bookItem);

				bookItem.VerseParagraphs = file.Attributes("VerseParagraphs").Any() && (bool)file.Attribute("VerseParagraph");

				foreach (XElement chapter in file.Elements("Chapter"))
				{
					var chapterno = (int)chapter.Attribute("Number");

					var chapterItem = new ChapterItem(book, chapterno);
					items.Add(chapterItem);
					bookItem.Items.Add(chapterItem);

					foreach (XElement x in chapter.Elements())
					{
						if (x.Name == "Title")
						{
							var titleItem = new TitleItem(book, x.Value, chapterItem.Chapter, (int)x.Attribute("Verse"));
							items.Add(titleItem);
							bookItem.Items.Add(titleItem);
						}
						else if (x.Name == "Footnote")
						{
							var footnoteItem = new FootnoteItem(book, x.Value, chapterItem.Chapter, (int)x.Attribute("Verse"));
							items.Add(footnoteItem);
							bookItem.Items.Add(footnoteItem);
						}
						else if (x.Name == "Paragraph")
						{
							var paragraphItem = new ParagraphItem(book, chapterItem.Chapter, (int)x.Attribute("Verse"));
							items.Add(paragraphItem);
							bookItem.Items.Add(paragraphItem);
						}
					}
				}
			}
			items.Sort((a, b) => Location.Compare(a.Location, b.Location));
		}

		public static void DoMapVerses(List<FrameworkItem> items)
		{
			if (MapVerses)
			{
				foreach (var title in items.OfType<TitleItem>())
				{
					title.Location = VerseMaps.Titles.Map(title.Location);
				}
				foreach (var footnote in items.OfType<FootnoteItem>())
				{
					footnote.Location = VerseMaps.Footnotes.Map(footnote.Location);
					// map footnote references

					var books = Books[Language]
						.Select(book => Regex.Escape(book.Value.Abbreviation))
						.ToArray();
					var bookspattern = String.Join('|', books);
					footnote.Footnote = Regex.Replace(footnote.Footnote, $@"\s(?<book>{bookspattern})\s+(?<chapter[0-9]+)(?<separator>[:,])(?<verse>[0-9]+)(?:-(?<upto>[0-9]+))?", match =>
					{
						var bookabbrevation = match.Groups["book"].Value;
						var chapter = int.Parse(match.Groups["chapter"].Value);
						var verse = int.Parse(match.Groups["verse"].Value);
						int upto = -1;
						if (match.Groups["upto"].Success) upto = int.Parse(match.Groups["upto"].Value);
						Location? location = null, uptolocation = null;
						location = new Location()
						{
							Book = Books[Language].Values.FirstOrDefault(book => book.Abbreviation == bookabbrevation),
							Chapter = chapter,
							Verse = verse
						};
						location = VerseMaps.ParallelVerses.Map(location);
						if (upto != -1)
						{
							uptolocation = new Location()
							{
								Book = Books[Language].Values.FirstOrDefault(book => book.Abbreviation == bookabbrevation),
								Chapter = chapter,
								Verse = upto
							};
							uptolocation = VerseMaps.ParallelVerses.Map(uptolocation);
							if (uptolocation.Chapter != location.Chapter || uptolocation.Verse <= location.Verse) uptolocation = null;
						}

						var uptostring = uptolocation != null ? $"-{uptolocation.Verse}" : "";
						return $" {bookabbrevation} {location.Chapter}{match.Groups["separator"].Value}{location.Verse}{uptostring}";
					});

				}
				foreach (var paragraph in items.OfType<ParagraphItem>())
				{
					paragraph.Location = VerseMaps.Paragraphs.Map(paragraph.Location);
				}
				// Sort mapped items
				items.Sort((a, b) => Location.Compare(a.Location, b.Location));
				foreach (var book in items.OfType<BookItem>())
				{
					book.Items.Sort((a, b) => Location.Compare(a.Location, b.Location));
				}
			}
		}
		static void ImportFramework(string path)
		{
			var frmfile = Path.Combine(path, "src", "framework.md");
			var frmfilexml = Path.ChangeExtension(frmfile, ".xml");
			bool XmlFile = false;
			if (File.Exists(frmfile) || File.Exists(frmfilexml))
			{

				var mdfiles = Directory.EnumerateFiles(path, "*.md")
					.Where(file => Regex.IsMatch(Path.GetFileName(file), "^([0-9][0-9])"));

				var mdtimes = mdfiles.Select(file => File.GetLastWriteTimeUtc(file));
				if (!File.Exists(frmfile))
				{
					frmfile = frmfilexml;
					XmlFile = true;
				}
				var frmtime = File.GetLastWriteTimeUtc(frmfile);

				Books.Load(mdfiles);

				List<FrameworkItem> items = new List<FrameworkItem>();
				if (XmlFile) ReadXmlFrameworkItems(frmfilexml, items);
				else ReadMDFrameworkItems(frmfile, items);

				DoMapVerses(items);

				if (FromSource || Imported)
				{
					Log(frmfile, "Importing");

					foreach (string srcfile in mdfiles)
					{

						File.SetLastWriteTimeUtc(srcfile, DateTime.Now);
						var src = File.ReadAllText(srcfile);
						var srcname = Path.GetFileName(srcfile);
						string bookname = Books.Name(srcname);

						var bookItem = items
							.OfType<BookItem>()
							.FirstOrDefault(b => b.File == srcname);
						if (bookItem == null) continue;

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
						src = Regex.Replace(src, @"(?<!(^|\n)#.*?)\r?\n(?!\s*#)", " ", RegexOptions.Singleline);
						src = Regex.Replace(src, " +", " "); // remove multiple spaces.


						src = Regex.Replace(src, @"(?<=^|\n)##+.*?\r?\n", "", RegexOptions.Singleline); // remove titles
																																  // src = Regex.Replace(src, @"(\s*\^[a-zA-Z]+\^)|(([ \t]*\^[a-zA-Z]+\^\[[^\]]*\])+([ \t]*\r?\n)?)", "", RegexOptions.Singleline); // remove footnotes
						src = Regex.Replace(src, @"%!verse-paragraphs.*?%\r?\n?", "", RegexOptions.Singleline); // remove verse paragraphs

						var frames = bookItem.Items.GetEnumerator();
						var book = Books["default", bookname];
						FrameworkItem? frame = frames.MoveNext() ? frames.Current : null;
						int chapter = 0;
						int verse = -1;

						src = Regex.Replace(src, @"(?<=^|\n)#[ \t]+(?<chapter>[0-9]+)(\s*\r?\n|$)|\^(?<verse>[0-9]+)\^.*?(?=\^[0-9]+\^|\s*#|\s*$)|(?<=(^|\n)#[ \t]+[0-9]+[ \t]*\r?\n(##[ \t]+.*?\r?\n)?)(?<empty>.*?)(?=\^[0-9]+\^|\s*#|\s*$)", m =>
						{
							var txt = m.Value;

							if (m.Groups["chapter"].Success) // chapter
							{
								int.TryParse(m.Groups["chapter"].Value, out chapter); verse = -1;
							}
							else if (m.Groups["verse"].Success) // verse
							{
								int.TryParse(m.Groups["verse"].Value, out verse);
							}
							else if (m.Groups["empty"].Success)
							{
								verse = 0;
							}

							if (frame != null)
							{

								if (frame.Chapter <= chapter && frame.Verse <= verse)
								{

									if (frame is TitleItem)
									{
										return $"{txt}{Environment.NewLine}{Environment.NewLine}#{((TitleItem)frame).Title}{Environment.NewLine}";
									}
									else if (frame is FootnoteItem)
									{
										return $"{txt} {((FootnoteItem)frame).Footnote}";
									}
									else if (frame is ParagraphItem)
									{
										return $"{txt}{Environment.NewLine}{Environment.NewLine}";
									}

									frame = frames.MoveNext() ? frames.Current : null;

									return txt;
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
}