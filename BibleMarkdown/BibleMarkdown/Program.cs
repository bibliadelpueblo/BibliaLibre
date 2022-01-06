using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Diagnostics.Tracing;
using System.Security.Cryptography;
using System.Data.SqlTypes;
using System.Xml;
using System.Xml.Linq;
using Pandoc;
using System.Runtime.InteropServices;

namespace BibleMarkdown
{
	class Program
	{

		static DateTime bibmarktime;
		static bool LowercaseFirstWords = false;
		static bool FromSource = false;
		static bool Imported = false;
		static string Language = null;
		static string Replace = null;

		static void Log(string file)
		{
			var current = Directory.GetCurrentDirectory();
			if (file.StartsWith(current))
			{
				file = file.Substring(current.Length);
			}
			Console.WriteLine($"Created {file}.");
		}
		static void ImportFromUSFM(string mdpath, string srcpath)
		{
			var sources = Directory.EnumerateFiles(srcpath)
				.Where(file => file.EndsWith(".usfm"));

			if (sources.Any())
			{

				var mdtimes = Directory.EnumerateFiles(mdpath)
					.Select(file => File.GetLastWriteTimeUtc(file));
				var sourcetimes = sources.Select(file => File.GetLastWriteTimeUtc(file));

				var mdtime = DateTime.MinValue;
				var sourcetime = DateTime.MinValue;


				foreach (var time in mdtimes) mdtime = time > mdtime ? time : mdtime;
				foreach (var time in sourcetimes) sourcetime = time > sourcetime ? time : sourcetime;

				if (FromSource)
				{
					Imported = true;

					int bookno = 1;

					foreach (var source in sources)
					{
						var src = File.ReadAllText(source);
						var bookm = Regex.Match(src, @"\\h\s+(.*?)$", RegexOptions.Multiline);
						var book = bookm.Groups[1].Value.Trim();

						if (book == "") bookm = Regex.Match(src, @"\\toc1\s+(.*?)$", RegexOptions.Multiline);
						book = bookm.Groups[1].Value.Trim();

						src = Regex.Match(src, @"\\c\s+[0-9]+.*", RegexOptions.Singleline).Value; // remove header that is not content of a chapter
						src = src.Replace("\r", "").Replace("\n", ""); // remove newlines
						src = Regex.Replace(src, @"(?<=\\c\s+[0-9]+\s*(\\s[0-9]+\s+[^\\]*?)?)\\p", ""); // remove empty paragraph after chapter
						src = Regex.Replace(src, @"\\m?s([0-9]+)\s*([^\\]+)", m =>
						{
							int n = 1;
							int.TryParse(m.Groups[1].Value, out n);
							n++;
							return $"{new String('#', n)} {m.Groups[2].Value.Trim()}{Environment.NewLine}";
						}); // section titles
						bool firstchapter = true;
						src = Regex.Replace(src, @"\\c\s+([0-9]+\s*)", m =>
						{
							var res = firstchapter ? $"# {m.Groups[1].Value}{Environment.NewLine}" : $"{Environment.NewLine}{Environment.NewLine}# {m.Groups[1].Value}{Environment.NewLine}";
							firstchapter = false;
							return res;
						}); // chapters
						src = Regex.Replace(src, @"\\v\s+([0-9]+)", "^$1^"); // verse numbers

						// footnotes
						var n = 0;
						string osrc;
						do {
							osrc = src;
							src = Regex.Replace(src, @"\\(?<type>[fx])\s*[+-?]\s*(?<footnote>.*?)\\\k<type>\*(?<body>.*?(?=\s*#|\\p))|(?<par>\\p)", m =>
							{
								if (m.Groups["par"].Success)
								{
									n = 0;
									return m.Value;
								} else
								{
									return $"^{Label(n)}^{m.Groups["body"].Value}{Environment.NewLine}^{Label(n++)}^[{m.Groups["footnote"].Value}]";
								}
							}, RegexOptions.Singleline); 
						} while (osrc != src);

						src = Regex.Replace(src, @"\\p *", string.Concat(Enumerable.Repeat(Environment.NewLine, 2))); // replace new paragraph with empty line
						src = Regex.Replace(src, @"\|([a-z-]+=""[^""]*""\s*)+", ""); // remove word attributes
						src = Regex.Replace(src, @"\\\+?\w+(\*|\s*)?", ""); // remove usfm tags
						src = Regex.Replace(src, @" +", " "); // remove multiple spaces
						src = Regex.Replace(src, @"\^\[([0-9]+)[.:]([0-9]+)", "^[**$1:$2**"); // bold verse references in footnotes
						src = Regex.Replace(src, @"(?<!^|(r?\n\r?\n)|#)#(?!#)", $"{Environment.NewLine}#", RegexOptions.Singleline); // add blank line over title
						if (LowercaseFirstWords) // needed for ReinaValera1909, it has uppercase words on every beginning of a chapter
						{
							src = Regex.Replace(src, @"(\^1\^ \w)(\w*)", m => $"{m.Groups[1].Value}{m.Groups[2].Value.ToLower()}");
							src = Regex.Replace(src, @"(\^1\^ \w )(\w*)", m => $"{m.Groups[1].Value}{m.Groups[2].Value.ToLower()}");
						}

						var md = Path.Combine(mdpath, $"{bookno:D2}-{book}.md");
						bookno++;
						File.WriteAllText(md, src);
						Log(md);
					}

				}

			}
		}

		public static void ImportFromTXT(string mdpath, string srcpath)
		{
			var sources = Directory.EnumerateFiles(srcpath)
				.Where(file => file.EndsWith(".txt"));

			if (sources.Any())
			{

				var mdtimes = Directory.EnumerateFiles(mdpath)
					.Select(file => File.GetLastWriteTimeUtc(file));
				var sourcetimes = sources.Select(file => File.GetLastWriteTimeUtc(file));

				var mdtime = DateTime.MinValue;
				var sourcetime = DateTime.MinValue;


				foreach (var time in mdtimes) mdtime = time > mdtime ? time : mdtime;
				foreach (var time in sourcetimes) sourcetime = time > sourcetime ? time : sourcetime;

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

			var mdtimes = Directory.EnumerateFiles(mdpath)
				.Select(file => File.GetLastWriteTimeUtc(file));
			var sourcetimes = sources.Select(file => File.GetLastWriteTimeUtc(file));

			var mdtime = DateTime.MinValue;
			var sourcetime = DateTime.MinValue;

			foreach (var time in mdtimes) mdtime = time > mdtime ? time : mdtime;
			foreach (var time in sourcetimes) sourcetime = time > sourcetime ? time : sourcetime;

			if (FromSource)
			{

				foreach (var source in sources)
				{
					var root = XElement.Load(File.Open(source, FileMode.Open));

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
		public struct Footnote
		{
			public int Index;
			public int FIndex;
			public string Value;

			public Footnote(int Index, int FIndex, string Value)
			{
				this.Index = Index;
				this.FIndex = FIndex;
				this.Value = Value;
			}
		}
		static void CreatePandoc(string file, string panfile)
		{
			var text = File.ReadAllText(file);

			if (Replace != null && Replace.Length > 1)
			{
				var tokens = Replace.Split(Replace[0]);
				for (int i = 1; i < tokens.Length-1; i += 2)
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
					for (int i = 1; i < tokens.Length-1; i += 2)
					{
						text = Regex.Replace(text, tokens[i], tokens[i + 1]);
					}
				}
			}

			var exit = false;
			while (!exit) {
				var txt = Regex.Replace(text, @"\^(?<mark>[a-zA-Z]+)\^(?<text>.*?)(?:\^\k<mark>(?<footnote>\^\[.*?\]))", "${footnote}${text}", RegexOptions.Singleline); // ^^ footnotes
				if (txt == text) exit = true;
				text = txt;
			} 


			if (text.Contains(@"%!verse-paragraphs.*?%")) // each verse in a separate paragraph. For use in Psalms & Proverbs
			{
				text = Regex.Replace(text, @"(\^[0-9]+\^[^#]*?)(\s*?)(?=\^[0-9]+\^)", "$1\\\n", RegexOptions.Singleline);
			}

			text = Regex.Replace(text, @"\^([0-9]+)\^", @"\bibleverse{$1}"); // verses
			text = Regex.Replace(text, @"%.*?%", "", RegexOptions.Singleline); // comments
			text = Regex.Replace(text, @"^(# .*?)$\n^(## .*?)$", "$2\n$1", RegexOptions.Multiline); // titles
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

		static async Task CreateTeX(string mdfile, string texfile)
		{
			await PandocInstance.Convert<PandocMdIn, LaTeXOut>(mdfile, texfile);
			Log(texfile);
		}

		static string Label(int i)
		{
			if (i == 0) return "a";
			StringBuilder label = new StringBuilder();
			while (i > 0)
			{
				var ch = (char)(((int)'a') + i % 26);
				label.Append(ch);
				i = i / 26;
			}
			return label.ToString();
		}

		static async Task CreateHtml(string mdfile, string htmlfile)
		{

			var mdhtmlfile = Path.ChangeExtension(mdfile, ".html.md");

			var src = File.ReadAllText(mdfile);
			src = Regex.Replace(src, @"\\bibleverse\{([0-9]+)\}", "<sup class='bibleverse'>$1</sup>", RegexOptions.Singleline);
			File.WriteAllText(mdhtmlfile, src);
			Log(mdhtmlfile);
			
			await PandocInstance.Convert<PandocMdIn, HtmlOut>(mdhtmlfile, htmlfile);
			Log(htmlfile);
		}

		static async Task ProcessFile(string file)
		{
			var path = Path.GetDirectoryName(file);
			var md = Path.Combine(path, "out", "pandoc");
			var tex = Path.Combine(path, "out", "tex");
			var html = Path.Combine(path, "out", "html");
			if (!Directory.Exists(md)) Directory.CreateDirectory(md);
			if (!Directory.Exists(tex)) Directory.CreateDirectory(tex);
			if (!Directory.Exists(html)) Directory.CreateDirectory(html);
			var mdfile = Path.Combine(md, Path.GetFileName(file));
			var texfile = Path.Combine(tex, Path.GetFileNameWithoutExtension(file) + ".tex");
			var htmlfile = Path.Combine(html, Path.GetFileNameWithoutExtension(file) + ".html");

			var mdfiletime = DateTime.MinValue;
			var texfiletime = DateTime.MinValue;
			var htmlfiletime = DateTime.MinValue;
			var filetime = File.GetLastWriteTimeUtc(file);

			Task TeXTask = Task.CompletedTask, HtmlTask = Task.CompletedTask;

			if (File.Exists(mdfile)) mdfiletime = File.GetLastWriteTimeUtc(mdfile);
			if (mdfiletime < filetime || mdfiletime < bibmarktime)
			{
				CreatePandoc(file, mdfile);
				mdfiletime = DateTime.Now;
			}

			if (File.Exists(texfile)) texfiletime = File.GetLastWriteTimeUtc(texfile);
			if (texfiletime < mdfiletime || texfiletime < bibmarktime)
			{
				TeXTask = CreateTeX(mdfile, texfile);
			}

			if (File.Exists(htmlfile)) htmlfiletime = File.GetLastWriteTimeUtc(htmlfile);
			if (htmlfiletime < mdfiletime || htmlfiletime < bibmarktime)
			{
				HtmlTask = CreateHtml(mdfile, htmlfile);
				htmlfiletime = DateTime.Now;
			}
			await Task.WhenAll(TeXTask, HtmlTask);
			return;
		}

		static void CreateVerseStats(string path)
		{
			var sources = Directory.EnumerateFiles(path, "*.md")
				.Where(file => Regex.IsMatch(Path.GetFileName(file), "^([0-9][0-9])"));
			var verses = new StringBuilder();

			var frames = Path.Combine(path, @"out", "verseinfo.md");
			var frametime = DateTime.MinValue;
			if (File.Exists(frames)) frametime = File.GetLastWriteTimeUtc(frames);

			if (sources.All(src => File.GetLastWriteTimeUtc(src) < frametime)) return;

			bool firstsrc = true;
			int btotal = 0;
			foreach (var source in sources) {

				if (!firstsrc) verses.AppendLine();
				firstsrc = false;
				verses.AppendLine($"# {Path.GetFileName(source)}");

				var txt = File.ReadAllText(source);

				int chapter = 0;
				int verse = 0;
				int nverses = 0;
				int totalverses = 0;
				var matches = Regex.Matches(txt, @"((^|\n)# ([0-9]+))|(\^([0-9]+)\^(?!\s*[#\^$]))");
				foreach (Match m in matches)
				{
					if (m.Groups[1].Success)
					{
						int.TryParse(m.Groups[3].Value, out chapter);
						if (verse != 0)
						{
							verses.Append(verse);
							verses.Append(' ');
						}
						verses.Append(chapter); verses.Append(':');
						totalverses += nverses;
						nverses = 0;
					} else if (m.Groups[4].Success)
					{
						int.TryParse(m.Groups[5].Value, out verse);
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

			if (sources.All(src => File.GetLastWriteTimeUtc(src) < frametime)) return; 

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
				bnames = XElement.Load(File.OpenRead(namesfile))
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

			} else
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
								XElement r = refs[refi];

								while ((refi+1 < refs.Length) && (((int)r.Attribute("bn") < book) || 
									((int)r.Attribute("bn") == book) && ((int)r.Attribute("cn") < nchapter) ||
									((int)r.Attribute("bn") == book) && ((int)r.Attribute("cn") == nchapter) && ((int)r.Attribute("vn") < nvers)))
								{
									r = refs[refi++];
								}
								if (((int)r.Attribute("bn") == book) && ((int)r.Attribute("cn") == nchapter) && ((int)r.Attribute("vn") == nvers)) {
									if (lastvers != vers) verses.Append($@"^{vers}^ ");
									lastvers = vers;
									var label = Label(footnotenumber++);
									verses.Append($"^{label}^ ");
									footnotes.Append($"^{label}^[**{nchapter}:{nvers}**");
									bool firstlink = true;
									foreach (var link in r.Elements("link"))
									{
										var bookname = bnames.FirstOrDefault(b => ((int)b.Attribute("bnumber")) == ((int)link.Attribute("bn")));
										if (bookname != null)
										{
											var bshort = (string)bookname.Attribute("bshort");
											if (!firstlink) footnotes.Append(';');
											else firstlink = false;
											footnotes.Append($" {bshort} {(string)link.Attribute("cn1")},{(string)link.Attribute("vn1")}");
											if (link.Attribute("vn2") != null) footnotes.Append($"-{(string)link.Attribute("vn2")}");
										}
									}
									footnotes.Append("] ");
								}
							}
						} else if (m.Groups["marker"].Success)
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
						} else if (m.Groups["title"].Success)
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

		static void ImportFramework(string path)
		{
			var frmfile = Path.Combine(path, @"src\framework.md");

			if (File.Exists(frmfile))
			{

				var mdfiles = Directory.EnumerateFiles(path, "*.md")
					.Where(file => Regex.IsMatch(Path.GetFileName(file), "^([0-9][0-9])"));

				var mdtimes = mdfiles.Select(file => File.GetLastWriteTimeUtc(file));
				var frmtime = File.GetLastWriteTimeUtc(frmfile);

				var frame = File.ReadAllText(frmfile);
				frame = Regex.Replace(frame, "%(!=!).*?%", "", RegexOptions.Singleline); // remove comments

				if (FromSource || Imported)
				{

					foreach (string srcfile in mdfiles)
					{

						File.SetLastWriteTimeUtc(srcfile, DateTime.Now);
						var src = File.ReadAllText(srcfile);
						var srcname = Path.GetFileName(srcfile);

						var frmpartmatch = Regex.Match(frame, $@"(?<=(^|\n)# {srcname}\r?\n).*?(?=\n# |$)", RegexOptions.Singleline);
						if (frmpartmatch.Success)
						{

							// remove current frame
							src = Regex.Replace(src, @"(?<=\r?\n|^)\r?\n(?!\s*#)", @""); // remove blank line
							src = Regex.Replace(src, @"(?<=^|\n)##+.*?\r?\n", ""); // remove titles
							src = Regex.Replace(src, @"(\s*\^[a-zA-Z]+\^)|(([ \t]*\^[a-zA-Z]+\^\[[^\]]*\])+([ \t]*\r?\n)?)", "", RegexOptions.Singleline); // remove footnotes
							src = Regex.Replace(src, @"%!verse-paragraphs.*?%\r?\n?", ""); // remove verse paragraphs

							var frmpart = frmpartmatch.Value;
							var frames = Regex.Matches(frmpart, @"(?<=(^|\n)## (?<chapter>[0-9]+)(?:\r?\n|$).*?)\^(?<verse>[0-9]+)\^(?<versecontent>(\s*((?<marker>\^[a-zA-Z]+\^(?!\[))|(?<footnote>\^[a-zA-Z]+\^\[[^\]]*\])))*\s*(?:(?:\r?\n#(?<titlelevel>#+)\s*(?<title>.*?)(\r?\n|$))|\\|(?=\^[0-9]+\^)))", RegexOptions.Singleline).GetEnumerator();
							var hasFrame = frames.MoveNext();

							var m = Regex.Match(frmpart, "%!verse-paragraphs.*?%");
							if (m.Success) src = $"{m.Value}{Environment.NewLine}{src}";

							int chapter = 0;
							int verse = 0;
							src = Regex.Replace(src, @"(?<=^|\n)#\s+(?<chapter>[0-9]+)(\s*\r?\n|$)|\^(?<verse>[0-9]+)\^.*?(?=\^[0-9]+\^|\s*#)", m =>
							{
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

									if (fchapter <= chapter && fverse <= verse)
									{
										hasFrame = frames.MoveNext();
										var res = new StringBuilder(m.Value);
										if (f.Groups["marker"].Success) { 
											var markers = Regex.Matches(f.Groups["versecontent"].Value, @"\^[a-zA-Z]+\^(?!\[)");
											foreach (Match marker in markers)
											{
												if (!char.IsWhiteSpace(m.Value[m.Value.Length - 1])) res.Append(" ");
												res.Append($"{marker.Value} ");
											}
										}
										var foots = Regex.Matches(f.Groups["versecontent"].Value, @"\^[a-zA-Z]+\^\[[^\]]*\]");
										bool hasFoots = false;
										foreach (Match foot in foots) {
											if (hasFoots) res.Append(" ");
											else res.AppendLine();
											res.Append(foot.Value);
											hasFoots = true;
										}
										if (f.Groups["versecontent"].Value.Contains("\\")) { res.AppendLine(); res.AppendLine(); }
										else if (f.Groups["title"].Success && f.Groups["titlelevel"].Value != "#") 
											if (m.Groups["chapter"].Success)
											{
												return $"{res.ToString()}## {f.Groups["title"].Value}{Environment.NewLine}";
											} else
											{
												return $"{res.ToString()}{Environment.NewLine}{Environment.NewLine}## {f.Groups["title"].Value}{Environment.NewLine}"; // add title
											}
										//if (hasFoots) res.AppendLine();
										return res.ToString();
									}
								}
								return m.Value;
							}, RegexOptions.Singleline);

							File.WriteAllText(srcfile, src);
							Log(srcfile);
						}
					}
				}
			}
		}

		static async Task ProcessPath(string path)
		{
			var srcpath = Path.Combine(path, "src");
			var outpath = Path.Combine(path, "out");
			if (!Directory.Exists(outpath)) Directory.CreateDirectory(outpath);
			if (Directory.Exists(srcpath))
			{
				ImportFromUSFM(path, srcpath);
				ImportFromTXT(path, srcpath);
				ImportFromZefania(path, srcpath);
				ImportFramework(path);
			}
			CreateFramework(path);
			CreateVerseStats(path);
			var files = Directory.EnumerateFiles(path, "*.md");
			Task.WaitAll(files.Select(file => ProcessFile(file)).ToArray());
		}

		static void InitPandoc()
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) PandocInstance.SetPandocPath("pandoc.exe");
			else PandocInstance.SetPandocPath("pandoc");
		}
		static void Main(string[] args)
		{

			// Get the version of the current application.
			var asm = Assembly.GetExecutingAssembly();
			var aname = asm.GetName();
			Console.WriteLine($"{aname.Name}, v{aname.Version.Major}.{aname.Version.Minor}.{aname.Version.Build}.{aname.Version.Revision}");

			InitPandoc();
			var exe = new Uri(System.Reflection.Assembly.GetExecutingAssembly().CodeBase).LocalPath;
			bibmarktime = File.GetLastWriteTimeUtc(exe);

			LowercaseFirstWords = args.Contains("-plc");
			FromSource = args.Contains("-s") || args.Contains("-src");
			var lnpos = Array.IndexOf(args, "-ln");
			if (lnpos >= 0 && (lnpos + 1 < args.Length)) Language = args[lnpos + 1];

			var replacepos = Array.IndexOf(args, "-replace");
			if (replacepos == -1) replacepos = Array.IndexOf(args, "-r");
			if (replacepos >= 0 && replacepos + 1 < args.Length) Replace = args[replacepos + 1];

			var paths = args.ToList();
			for (int i = 0; i < paths.Count; i++)
			{
				if (paths[i] == "-ln" || paths[i] == "-replace" || paths[i] == "-r")
				{
					paths.RemoveAt(i); paths.RemoveAt(i); i--;
				} else if (paths[i].StartsWith('-'))
				{
					paths.RemoveAt(i); i--;
				}
			}
			string path;
			if (paths.Count == 0)
			{
				path = Directory.GetCurrentDirectory();
				ProcessPath(path);
			} else
			{
				path = paths[0];
				if (Directory.Exists(path))
				{
					ProcessPath(path);
				}
				else if (File.Exists(path)) ProcessFile(path);
			}
		}
	}
}
