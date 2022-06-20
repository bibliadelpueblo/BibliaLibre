using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Threading.Tasks;
using Pandoc;

namespace BibleMarkdown
{
	partial class Program
	{

		static void CreatePandoc(string file, string panfile)
		{
			if (IsNewer(panfile, file)) return;

			var text = File.ReadAllText(file);

			if (Replace != null && Replace.Length > 1)
			{
				var tokens = Replace.Split(Replace[0]);
				for (int i = 1; i < tokens.Length - 1; i += 2)
				{
					text = Regex.Replace(text, tokens[i], tokens[i + 1], RegexOptions.Singleline);
				}
			}
			var replmatch = Regex.Match(text, @"%!replace\s+(?<replace>.*?)%");
			if (replmatch.Success)
			{
				var s = replmatch.Groups["replace"].Value;
				if (s.Length > 4)
				{
					var tokens = s.Split(s[0]);
					for (int i = 1; i < tokens.Length - 1; i += 2)
					{
						text = Regex.Replace(text, tokens[i], tokens[i + 1]);
					}
				}
			}

			bool replaced;
			do
			{
				replaced = false;
				text = Regex.Replace(text, @"\^(?<mark>[a-zA-Z]+)\^(?!\[)(?<text>.*?)(?:\^\k<mark>(?<footnote>\^\[.*?(?<!\\)\]))", m =>
				{
					replaced = true;
					return $"{m.Groups["footnote"].Value}{m.Groups["text"].Value}";
				}, RegexOptions.Singleline);// ^^ footnotes
			} while (replaced);

			if (text.Contains(@"%!verse-paragraphs.*?%")) // each verse in a separate paragraph. For use in Psalms & Proverbs
			{
				text = Regex.Replace(text, @"(\^[0-9]+\^[^#]*?)(\s*?)(?=\^[0-9]+\^)", "$1\\\n", RegexOptions.Singleline);
			}

			// text = Regex.Replace(text, @"\^([0-9]+)\^", @"\bibleverse{$1}"); // verses
			text = Regex.Replace(text, @"%.*?%", "", RegexOptions.Singleline); // comments
																									 // text = Regex.Replace(text, @"^(# .*?)$\n^(## .*?)$", "$2\n$1", RegexOptions.Multiline); // titles
			text = Regex.Replace(text, @"\^\^", "^"); // alternative for superscript
			text = Regex.Replace(text, @"""(.*?)""", $"“$1”"); // replace quotation mark with nicer letters
			/*
			text = Regex.Replace(text, @" ^# (.*?)$", @"\chapter{$1}", RegexOptions.Multiline);
			text = Regex.Replace(text, @"^## (.*?)$", @"\section{$1}", RegexOptions.Multiline);
			text = Regex.Replace(text, @"^### (.*?)$", @"\subsection{$1}", RegexOptions.Multiline);
			text = Regex.Replace(text, @"^#### (.*)$", @"\subsubsection{$1}", RegexOptions.Multiline);
			text = Regex.Replace(text, @"\*\*(.*?)(?=\*\*)", @"\bfseries{$1}");
			text = Regex.Replace(text, @"\*([^*]*)\*", @"\emph{$1}", RegexOptions.Singleline); 
			text = Regex.Replace(text, @"\^\[([^\]]*)\]", @"\footnote{$1}", RegexOptions.Singleline);
			*/

			File.WriteAllText(panfile, text);
			Log(panfile);
		}

		static async Task CreateTeXAsync(string mdfile, string texfile)
		{
			if (IsNewer(texfile, mdfile)) return;

			var mdtexfile = Path.Combine(Path.GetDirectoryName(mdfile), "tex", Path.GetFileName(mdfile));
			var src = File.ReadAllText(mdfile);
			src = Regex.Replace(src, @"\^([0-9]+)\^", @"\bibleverse{$1}"); // verses
			src = Regex.Replace(src, @"^(# .*?)$\n^(## .*?)$", "$2\n$1", RegexOptions.Multiline); // titles
			File.WriteAllText(mdtexfile, src);
			Log(mdtexfile);

			await PandocInstance.Convert<PandocMdIn, LaTeXOut>(mdtexfile, texfile);
			Log(texfile);
		}

		static async Task CreateHtmlAsync(string mdfile, string htmlfile)
		{
			if (IsNewer(htmlfile, mdfile)) return;

			//var mdhtmlfile = Path.ChangeExtension(mdfile, ".html.md");

			//File.Copy(mdfile, mdhtmlfile);
			//var src = File.ReadAllText(mdfile);
			//src = Regex.Replace(src, @"\^([0-9]+)\^", "<sup class='bibleverse'>$1</sup>", RegexOptions.Singleline);
			//File.WriteAllText(htmlfile, src);
			//Log(mdhtmlfile);

			await PandocInstance.Convert<PandocMdIn, HtmlOut>(mdfile, htmlfile);
			Log(htmlfile);
		}

		static void CreateEpub(string path, string mdfile, string epubfile)
		{
			if (IsNewer(epubfile, mdfile)) return;

			var src = File.ReadAllText(mdfile);
			string? book = Regex.Match(Path.GetFileNameWithoutExtension(mdfile), "^[0-9]*-?(?<name>.*?)$", RegexOptions.Singleline)?.Groups["name"]?.Value;

			src = Regex.Replace(src, @"(?<=^|\n#\s(?<chapter>[0-9]+).*?)\^(?<verse>[0-9]+)\^", m =>
			{
				return @$"[**{m.Groups["verse"].Value}**]{{#{m.Groups["chapter"].Value}-{m.Groups["verse"].Value}}}";
			}, RegexOptions.Singleline);

			src = Regex.Replace(src, @"(?<=\n|^)#", "##", RegexOptions.Singleline);

			var namesfile = Path.Combine(path, "src", "bnames.xml");
			var books = XElement.Load(File.Open(namesfile, FileMode.Open, FileAccess.Read))
				.Elements("ID")
				.SelectMany(x => x.Elements("BOOK"))
				.Select(x => new
				{
					Book = x.Value,
					Abbreviation = (string)x.Attribute("bshort"),
					Number = (int)x.Attribute("bnumber")
				})
				.ToArray();

			var pattern = String.Join('|', books.Select(b => b.Abbreviation).ToArray());
			src = Regex.Replace(src, @$"(?<book>{pattern})\s+(?<chapter>[0-9]+)([:,](?<verse>[0-9]+)(-(?<upto>[0-9]+))?)", m =>
			{
				var book = books.FirstOrDefault(b => b.Abbreviation == m.Groups["book"].Value);
				if (!m.Groups["upto"].Success) return $@"[{m.Groups["book"].Value} {m.Groups["chapter"].Value},{m.Groups["verse"].Value}]()";
				else return $@"[{m.Groups["book"].Value} {m.Groups["chapter"].Value},{m.Groups["verse"].Value}-{m.Groups["upto"].Value}]()";
			}, RegexOptions.Singleline);

			src = Regex.Replace(src, @"\^([0-9]+)\^", "**$1**", RegexOptions.Singleline);
			var name = Regex.Replace(Path.GetFileNameWithoutExtension(mdfile), @"^[0-9\.]+-", "", RegexOptions.Singleline);
			src = $"# {name}{Environment.NewLine}{Environment.NewLine}{src}";
			File.WriteAllText(epubfile, src);
			Log(epubfile);
		}

		static void CreateVerseStats(string path)
		{
			var sources = Directory.EnumerateFiles(path, "*.md")
				.Where(file => Regex.IsMatch(Path.GetFileName(file), "^([0-9][0-9])"));
			var verses = new StringBuilder();

			var frames = Path.Combine(path, @"out", "verseinfo.md");
			var frametime = DateTime.MinValue;
			if (File.Exists(frames)) frametime = File.GetLastWriteTimeUtc(frames);

			if (sources.All(src => File.GetLastWriteTimeUtc(src) < frametime) && frametime > bibmarktime) return;

			bool firstsrc = true;
			int btotal = 0;
			foreach (var source in sources)
			{

				if (!firstsrc) verses.AppendLine();
				firstsrc = false;
				verses.AppendLine($"# {Path.GetFileName(source)}");

				var txt = File.ReadAllText(source);

				int chapter = 0;
				int verse = 0;
				int nverses = 0;
				int totalverses = 0;
				var matches = Regex.Matches(txt, @"((^|\n)#\s+(?<chapter>[0-9]+))|(\^(?<verse>[0-9]+)\^(?!\s*[#\^$]))", RegexOptions.Singleline);
				foreach (Match m in matches)
				{
					if (m.Groups[1].Success)
					{
						int.TryParse(m.Groups["chapter"].Value, out chapter);
						if (verse != 0)
						{
							verses.Append(verse);
							verses.Append(' ');
						}
						verses.Append(chapter); verses.Append(':');
						totalverses += nverses;
						nverses = 0;
					}
					else if (m.Groups["verse"].Success)
					{
						int.TryParse(m.Groups["verse"].Value, out verse);
						nverses = Math.Max(nverses, verse);

					}
				}
				if (verse != 0) verses.Append(verse);
				totalverses += nverses;
				nverses = 0;
				verses.Append("; Total verses:"); verses.Append(totalverses);
				btotal += totalverses;
				totalverses = 0;
				nverses = 0;
				verse = 0;
				chapter = 0;
			}

			verses.AppendLine(); verses.AppendLine(); verses.AppendLine(btotal.ToString());

			File.WriteAllText(frames, verses.ToString());
			Log(frames);
		}
		static void CreateFramework(string path)
		{
			var sources = Directory.EnumerateFiles(path, "*.md")
				.Where(file => Regex.IsMatch(Path.GetFileName(file), "^([0-9][0-9])"));
			var verses = new StringBuilder();

			var frames = Path.Combine(path, "out", "framework.md");
			var frametime = DateTime.MinValue;
			if (File.Exists(frames)) frametime = File.GetLastWriteTimeUtc(frames);

			if (sources.All(src => File.GetLastWriteTimeUtc(src) < frametime) && frametime > bibmarktime) return;

			var linklistfile = $@"{path}\src\linklist.xml";
			var namesfile = $@"{path}\src\bnames.xml";
			XElement[] refs;
			XElement[] bnames;
			int refi = 0;
			bool newrefs = false;
			if (File.Exists(linklistfile) && ((!File.Exists(frames)) || FromSource || File.GetLastWriteTimeUtc(linklistfile) > File.GetLastWriteTimeUtc(frames)))
			{
				newrefs = true;
				var list = XElement.Load(File.OpenRead(linklistfile));
				var language = ((string)list.Element("collection").Attribute("id"));
				bnames = XElement.Load(File.Open(namesfile, FileMode.Open, FileAccess.Read))
					.Elements("ID")
					.Where(id => ((string)id.Attribute("descr")) == language)
					.FirstOrDefault()
					.Elements("BOOK")
					.ToArray();

				refs = list.Descendants("verse")
					.OrderBy(link => (int)link.Attribute("bn"))
					.ThenBy(link => (int)link.Attribute("cn"))
					.ThenBy(link => (int)link.Attribute("vn"))
					.ToArray();

			}
			else
			{
				refs = new XElement[0];
				bnames = new XElement[0];
			}

			bool firstsrc = true;
			foreach (var source in sources)
			{
				if (!firstsrc) verses.AppendLine();
				firstsrc = false;
				verses.AppendLine($"# {Path.GetFileName(source)}");
				int book;
				int.TryParse(Regex.Match(Path.GetFileName(source), "^[0-9][0-9]").Value, out book);


				var txt = File.ReadAllText(source);

				if (Regex.IsMatch(txt, "%!verse-paragraphs.*?%")) verses.AppendLine("%!verse-paragraphs");

				bool firstchapter = true;
				int nchapter = 0;
				var chapters = Regex.Matches(txt, @"(?<!#)#(?!#)(\s*([0-9]*).*?)\r?\n(.*?)(?=(?<!#)#(?!#)|$)", RegexOptions.Singleline);
				foreach (Match chapter in chapters)
				{
					nchapter++;
					int.TryParse(chapter.Groups[2].Value, out nchapter);

					if (!firstchapter) verses.AppendLine();
					firstchapter = false;
					verses.AppendLine($"## {nchapter}");

					string strip;
					if (newrefs) strip = @"(\^[a-zA-Z]+\^)|(\^[a-zA-Z]+\^\[.*?\](\(.*?\))?[ \t]*\r?\n?)";
					else strip = @"[^\^]\[.*?\](\(.*?\))?[ \t]*\r?\n?";
					var rawch = Regex.Replace(chapter.Groups[3].Value, strip, ""); // remove markdown tags

					var ms = Regex.Matches(rawch, @"\^(?<verse>[0-9]+)\^|(?<marker>\^[a-zA-Z]+\^)|(?<footnote>\^[a-zA-Z]+\^\[(?:[^\]]*)\])|(?<=\r?\n)(?<blank>\r?\n)(?!\s*?(?:\^[a-zA-Z]+\^\[|#|$))|(?<=\r?\n|^)(?<title>##.*?)(?=\r?\n|$)", RegexOptions.Singleline);
					string vers = "0";
					string lastvers = null;
					StringBuilder footnotes = new StringBuilder();
					int footnotenumber = 0;
					foreach (Match m in ms)
					{
						if (m.Groups["verse"].Success)
						{
							vers = m.Groups["verse"].Value;
							int nvers = 0;
							int.TryParse(vers, out nvers);
							if (refi < refs.Length)
							{
								XElement r;
								Location loc;

								refi--;
								int bookno = 0;
								do
								{
									r = refs[++refi];

									bookno = (int)r.Attribute("bn");
									var bookname = bnames.FirstOrDefault(b => ((int)b.Attribute("bnumber")) == bookno);
									loc = new Location
									{
										Book = bookname?.Value ?? "",
										Chapter = ((int)r.Attribute("cn")),
										Verse = ((int)r.Attribute("vn"))
									};
									loc = Verses.ParallelVerses.Map(loc);

								} while ((refi + 1 < refs.Length) && (bookno < book) ||
										(bookno == book) && (loc.Chapter < nchapter) ||
										(bookno == book) && (loc.Chapter == nchapter) && (loc.Verse < nvers));
				
								if ((bookno == book) && (loc.Chapter == nchapter) && (loc.Verse == nvers))
								{
									if (lastvers != vers) verses.Append($@"^{vers}^ ");
									lastvers = vers;
									var label = Label(footnotenumber++);
									verses.Append($"^{label}^ ");
									footnotes.Append($"^{label}^[**{nchapter}:{nvers}**");
									bool firstlink = true;
									foreach (var link in r.Elements("link"))
									{
										var linkbookname = bnames.FirstOrDefault(b => ((int)b.Attribute("bnumber")) == ((int)link.Attribute("bn")));
										if (linkbookname != null)
										{
											var blong = (string)linkbookname.Value;
											var bshort = (string)linkbookname.Attribute("bshort");
											var linkfrom = new Location
											{
												Book = blong,
												Chapter = (int)link.Attribute("cn1"),
												Verse = (int)link.Attribute("vn1")
											};
											linkfrom = Verses.ParallelVerses.Map(linkfrom);

											if (!firstlink) footnotes.Append(';');
											else firstlink = false;
											footnotes.Append($" {bshort} {linkfrom.Chapter},{linkfrom.Verse}");
											if (link.Attribute("vn2") != null) {
												var linkto = new Location
												{
													Book = linkfrom.Book,
													Chapter = linkfrom.Chapter,
													Verse = (int)link.Attribute("vn2")
												};
												linkto = Verses.ParallelVerses.Map(linkto);

												if (linkto.Chapter == linkfrom.Chapter) footnotes.Append($"-{linkto.Verse}");
												else footnotes.Append($"-{linkto.Chapter},{linkto.Verse}");
											}
										}
									}
									footnotes.Append("] ");
								}
							}
						}
						else if (m.Groups["marker"].Success)
						{
							if (lastvers != vers) verses.Append($@"^{vers}^ ");
							lastvers = vers;
							verses.Append($"{m.Groups["marker"].Value} ");
						}
						else if (m.Groups["footnote"].Success)
						{
							if (lastvers != vers) verses.Append($@"^{vers}^ ");
							lastvers = vers;
							verses.Append(m.Groups["footnote"].Value); verses.Append(' ');
						}
						else if (m.Groups["blank"].Success)
						{
							if (lastvers != vers) verses.Append($@"^{vers}^ ");
							lastvers = vers;
							if (footnotes.Length > 0)
							{
								verses.Append(footnotes);
								verses.Append(' ');
								footnotenumber = 0;
								footnotes.Clear();
							}
							verses.Append("\\ ");
						}
						else if (m.Groups["title"].Success)
						{
							if (lastvers != vers) verses.Append($@"^{vers}^ ");
							lastvers = vers;
							if (footnotes.Length > 0)
							{
								verses.Append(footnotes);
								verses.Append(' ');
								footnotenumber = 0;
								footnotes.Clear();
							}
							verses.AppendLine($"{Environment.NewLine}#{m.Groups[7].Value.Trim()}");
						}
					}
					if (footnotes.Length > 0)
					{
						if (lastvers != vers) verses.Append($@"^{vers}^ ");
						lastvers = vers;
						verses.Append(footnotes);
						verses.Append(' ');
						footnotenumber = 0;
						footnotes.Clear();
					}
				}
			}

			File.WriteAllText(frames, verses.ToString());
			Log(frames);
		}

		static void CreateUSFM(string mdfile, string usfmfile)
		{

		}
		static string Marker(int n)
		{
			StringBuilder s = new StringBuilder();
			while (n > 0)
			{
				s.Append((char)((int)'a' + n % 26 - 1));
				n = n / 26;
			}
			return s.ToString();
		}


	}
}
