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
			text = Regex.Replace(text, @"(?<!<[^\n<>]*?)""(.*?)""(?![^\n<>]>)", $"“$1”"); // replace quotation mark with nicer letters
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
			if (IsNewer(htmlfile, mdfile) || TwoLanguage) return;

			//var mdhtmlfile = Path.ChangeExtension(mdfile, ".html.md");

			//File.Copy(mdfile, mdhtmlfile);
			//var src = File.ReadAllText(mdfile);
			//src = Regex.Replace(src, @"\^([0-9]+)\^", "<sup class='bibleverse'>$1</sup>", RegexOptions.Singleline);
			//File.WriteAllText(htmlfile, src);
			//Log(mdhtmlfile);

			await PandocInstance.Convert<PandocMdIn, HtmlOut>(mdfile, htmlfile);
			Log(htmlfile);
		}


		static string Id(string name)
		{
			return name.Replace(' ', '-').Replace('.', '-');
		}

		static void CreateTwoLanguage(string path, string path1, string path2)
		{
			var leftfiles = Directory.EnumerateFiles(path1, "*.md").ToArray();
			var rightfiles = Directory.EnumerateFiles(path2, "*.md").ToArray();
			int bookno = 1;
			var books = Books.All.Where(b => b.Number == bookno);
			Book leftbook = null, rightbook = null;
			while (books.Any())
			{
				var leftfile = leftfiles.FirstOrDefault(f =>
				{
					var bn = books.FirstOrDefault(b =>
						b.Name == Regex.Replace(Path.GetFileNameWithoutExtension(f), @"^[0-9]+(\.[0-9]+)?-", ""));
					if (bn != null)
					{
						leftbook = bn;
						return true;
					}
					else
					{
						return false;
					}
				});
				var rightfile = rightfiles.FirstOrDefault(f =>
				{
					var bn = books.FirstOrDefault(b =>
						b.Name == Regex.Replace(Path.GetFileNameWithoutExtension(f), @"^[0-9]+(\.[0-9]+)?-", ""));
					if (bn != null)
					{
						rightbook = bn;
						return true;
					}
					else
					{
						return false;
					}
				});

				if (leftfile != null && rightfile != null)
				{
					var lefttext = File.ReadAllText(leftfile);
					var righttext = File.ReadAllText(rightfile);

					var leftm = Regex.Matches(lefttext, @"(^|\n)#[ \t]+(?<chapter>[0-9]+)[ \t]*\r?\n(?<text>.*?)(\r?\n#(?!#)|$)", RegexOptions.Singleline);

					var text = new StringBuilder();
					foreach (Match m in leftm)
					{
						var leftchaptertext = m.Groups["text"].Value;
						int chapter = int.Parse(m.Groups["chapter"].Value);
						var endverse = Regex.Matches(leftchaptertext, @"\^([0-9]+)\^")
							.Select(m => int.Parse(m.Groups[1].Value))
							.Max();
						text.Append($@"# {chapter}{Environment.NewLine}\begin{{paracol}}{{2}}{Environment.NewLine}");
						text.Append(leftchaptertext);
						text.Append(@"\switchcolum");

						var rightstart = new Location
						{
							Book = leftbook,
							Chapter = chapter,
							Verse = 0
						};
						var rightend = new Location
						{
							Book = leftbook,
							Chapter = chapter,
							Verse = endverse
						};
						rightstart = VerseMaps.DualLanguage.Map(rightstart);
						rightend = VerseMaps.DualLanguage.Map(rightend);

						var rightpart = new StringBuilder();

						var ms = Regex.Matches(righttext, @"(^|\n)#[ \t]+(?<[0-9]+)");
					}
					books = Books.All.Where(b => b.Number == bookno++);
				}
			}
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

			var framesfile = Path.Combine(path, "out", "framework.md");
			var frametime = DateTime.MinValue;
			if (File.Exists(framesfile)) frametime = File.GetLastWriteTimeUtc(framesfile);

			if (sources.All(src => File.GetLastWriteTimeUtc(src) < frametime) && frametime > bibmarktime) return;

			var items = new List<FrameworkItem>();

			Books.Load(sources);

			foreach (var source in sources)
			{
				int bookno = Books.Number(source);
				string bookname = Books.Name(source);

				var book = Books["default", bookname];

				var bookItem = new BookItem(book, Path.GetFileName(source));
				
				items.Add(bookItem);

				var txt = File.ReadAllText(source);

				// remove bibmark footnotes
				bool replaced = true;
				while (replaced)
				{
					replaced = false;
					txt = Regex.Replace(txt, @"\^(?<mark>[a-zA-Z]+)\^(?!\[)(?<text>.*?)[ \t]*(?:\^\k<mark>(?<footnote>\^\[.*?(?<!\\)\]))[ \t]*\r?\n?", m =>
					{
						replaced = true;
						return $"{m.Groups["footnote"].Value}{m.Groups["text"].Value}";
					}, RegexOptions.Singleline);
				}

				bookItem.VerseParagraphs = Regex.IsMatch(txt, "%!verse-paragraphs.*?%");

				int chapterno = 0;
				var chapters = Regex.Matches(txt, @"(?<!#)#(?!#)(\s*(?<chapter>[0-9]+).*?)\r?\n(?<text>.*?)(?=(?<!#)#(?!#)|$)", RegexOptions.Singleline);
				foreach (Match chapter in chapters)
				{
					chapterno++;
					int.TryParse(chapter.Groups["chapter"].Value, out chapterno);

					var chapterItem = new ChapterItem(book, chapterno);
					items.Add(chapterItem);
					bookItem.Items.Add(chapterItem);

					var chaptertext = chapter.Groups["text"].Value;

					var tokens = Regex.Matches(chaptertext, @"\^(?<verse>[0-9]+)\^|(?<footnote>\^\[(?:[^\]]*)\])|(?<=\r?\n)(?<blank>\r?\n)(?!\s*?(?:\^[a-zA-Z]+\^\[|#|$))|(?<=\r?\n|^)##(?<title>.*?)(?=\r?\n|$)", RegexOptions.Singleline);
					int verse = 0;

					foreach (Match token in tokens)
						if (token.Groups["verse"].Success) verse = int.Parse(token.Groups["verse"].Value);
						else if (token.Groups["footnote"].Success)
						{
							var item = new FootnoteItem(book, token.Groups["footnote"].Value, chapterItem.Chapter, verse);
							items.Add(item);
							bookItem.Items.Add(item);

						}
						else if (token.Groups["blank"].Success)
						{
							var item = new ParagraphItem(book, chapterItem.Chapter, verse);
							items.Add(item);
							bookItem.Items.Add(item);

						}
						else if (token.Groups["title"].Success)
						{
							var item = new TitleItem(book, token.Groups["title"].Value, chapterItem.Chapter, verse);
							items.Add(item);
							bookItem.Items.Add(item);

						}
				}
			}

			// import parallel verses
			foreach (var parverse in ParallelVerseList.ParallelVerses)
			{
				StringBuilder footnote = new StringBuilder($"^[**{parverse.Verse.Chapter}:{parverse.Verse.Verse}** ");
				foreach (var pv in parverse.ParallelVerses)
				{
					if (pv.Verse == -1) pv.Verse = 1;
					footnote.Append($"{pv.Book.Abbreviation} {pv.Chapter},{pv.Verse}");
					if (pv.UpToVerse > 0) footnote.Append($"-{pv.UpToVerse}");
				}
				items.Add(new FootnoteItem(parverse.Verse.Book, footnote.ToString(), parverse.Verse.Chapter, parverse.Verse.Verse));
			}

			items.Sort((FrameworkItem a, FrameworkItem b) => Location.Compare(a.Location, b.Location));

			var result = new StringBuilder();
			Location lastlocation = Location.Zero;

			XElement? filexml = null, chapterxml = null;
			XElement root = new XElement("BibleFramework");
			foreach (var item in items)
			{
				if (item is BookItem)
				{
					var bookItem = (BookItem)item;
					result.AppendLine($"# {bookItem.Name}");
					filexml = new XElement("Book");
					filexml.Add(new XAttribute("Name", bookItem.Name));
					filexml.Add(new XAttribute("File", bookItem.File));
					root.Add(filexml);
				} else if (item is ChapterItem)
				{
					result.AppendLine($"## {item.Chapter}");
					chapterxml = new XElement("Chapter");
					chapterxml.Add(new XAttribute("Number", item.Chapter));
					if (filexml == null) Console.WriteLine("Error: No file for framework.");
					else filexml.Add(chapterxml);
				} else if (item is TitleItem) {
					var titleItem = (TitleItem)item;
					if (Location.Compare(lastlocation, item.Location) != 0) result.AppendLine($"^{item.Verse}^");
					var title = new XElement("Title");
					title.Value = titleItem.Title;
					title.Add(new XAttribute("Verse", item.Verse));
					if (chapterxml != null) chapterxml.Add(title);
					else Console.WriteLine("Error: No chapter for framework.");
					result.AppendLine($"###{titleItem.Title}");
				} else if (item is FootnoteItem)
				{
					var footnoteItem = (FootnoteItem)item;
					if (Location.Compare(lastlocation, item.Location) != 0) result.Append($"^{item.Verse}^");
					var footnote = new XElement("Footnote");
					footnote.Value = footnoteItem.Footnote;
					footnote.Add(new XAttribute("Verse", item.Verse));
					if (chapterxml != null) chapterxml.Add(footnote);
					else Console.WriteLine("Error: No chapter for framework.");
					result.Append(footnoteItem.Footnote);
				} else if (item is ParagraphItem)
				{
					if (Location.Compare(lastlocation, item.Location) != 0) result.Append($"^{item.Location.Verse}^");
					var paragraph = new XElement("Paragraph");
					paragraph.Add(new XAttribute("Verse", item.Verse));
					if (chapterxml != null) chapterxml.Add(paragraph);
					else Console.WriteLine("Error: No chapter for framework.");
					result.Append("\\");
				}
				lastlocation = item.Location;
			}

			File.WriteAllText(framesfile, result.ToString());
			string framesxml = Path.ChangeExtension(framesfile, ".xml");
			root.Save(framesxml);
			Log(framesfile);
			Log(framesxml);
		}

		static void CreateUSFM(string mdfile, string usfmfile)
		{
			if (IsNewer(usfmfile, mdfile)) return;

			string usfm = "";
			if (File.Exists(usfmfile)) usfm = File.ReadAllText(usfmfile);

			var txt = File.ReadAllText(mdfile);
			txt = Regex.Replace(txt, @"(^|\n)#[ \t]+([0-9]+)", @"\c $2", RegexOptions.Singleline);
			txt = Regex.Replace(txt, @"(^|\n)##[ \t]+(.*?)\r?\n", @"\s1 $2", RegexOptions.Singleline);
			txt = Regex.Replace(txt, @"(?<!^|\n)\^([0-9]+)\^", $@"{Environment.NewLine}\v $1", RegexOptions.Singleline);
			txt = Regex.Replace(txt, @"\^([0-9]+)\^", @"\v $1", RegexOptions.Singleline);
			txt = Regex.Replace(txt, @"\*", "", RegexOptions.Singleline);

			// remove bibmark footnotes.
			bool replaced = true;
			while (replaced)
			{
				replaced = false;
				txt = Regex.Replace(txt, @"\^(?<mark>[a-zA-Z]+)\^(?!\[)(?<text>.*?)(?:\^\k<mark>(?<footnote>\^\[.*?(?<!\\)\]))[ \t]*\r?\n?", m =>
				{
					replaced = true;
					return $"{m.Groups["footnote"].Value}{m.Groups["text"].Value}";
				}, RegexOptions.Singleline);
			}
			txt = Regex.Replace(txt, @"\^\[\s*(?<footpos>[0-9]+[:,][0-9]+)\s*(?<foottext>.*?)\s*\]", @"\f + \fr ${footpos} \ft ${foottext} \f*", RegexOptions.Singleline);
			txt = Regex.Replace(txt, @"(\r?\n)([ \t]*)(\r?\n)", @"$1\p$3$3", RegexOptions.Singleline);
			var header = Regex.Match(usfm, @"^.*?(?=\\c)", RegexOptions.Singleline).Value;
			txt = header + txt;

			File.WriteAllText(usfmfile, txt);
			Log(usfmfile);
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
