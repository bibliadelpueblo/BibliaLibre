using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Threading.Tasks;

namespace BibleMarkdown
{
	partial class Program
	{

		static void CreatePandoc(string file, string panfile)
		{
			if (IsNewer(panfile, file)) return;

			var text = File.ReadAllText(file);

			text = Preprocess(text);

			string? bookname = Books.Name(file);
			int bookno = Books.Number(file);

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
				text = Regex.Replace(text, @"\^(?<mark>[a-zA-Z]+)\^(?!\[)(?<text>.*?)(?:\^\k<mark>(?<footnote>\^\[(?>\[(?<c>)|[^\[\]]+|\](?<-c>))*(?(c)(?!))\]))[ \t]*\r?\n?", m =>
				{
					replaced = true;
					return $"{m.Groups["footnote"].Value}{m.Groups["text"].Value}";
				}, RegexOptions.Singleline);// ^^ footnotes
			} while (replaced);

			if (Regex.IsMatch(text, @"%!verse-paragraphs.*?%", RegexOptions.Singleline)) // each verse in a separate paragraph. For use in Psalms & Proverbs
			{
				text = Regex.Replace(text, @"(\^[0-9]+\^[^#]*?)(\s*?)(?=\^[0-9]+\^)", "$1\\\n", RegexOptions.Singleline);
			}

			// text = Regex.Replace(text, @"\^([0-9]+)\^", @"\bibleverse{$1}"); // verses
			text = Regex.Replace(text, @"%.*?%", "", RegexOptions.Singleline); // comments
																									 // text = Regex.Replace(text, @"^(# .*?)$\n^(## .*?)$", "$2\n$1", RegexOptions.Multiline); // titles
			text = Regex.Replace(text, @"\^\^", "^"); // alternative for superscript
			text = Regex.Replace(text, @"(?<!<[^\n<>]*?)""(.*?)""(?![^\n<>]>)", $"“$1”"); // replace quotation mark with nicer letters
																													//text = Regex.Replace(text, @"\^([0-9]+)\^", "[$1]{.bibleverse}"); // replace bibleverses with bibleverse span.
			text = Regex.Replace(text, @"([\u0590-\u05fe]+)", "[$1]{.hebrew}");
			text = Regex.Replace(text, @"([\u0370-\u03ff\u1f00-\u1fff]+)", "[$1]{.greek}");
			text = Regex.Replace(text, @"\^([0-9]+)\^", "[$1]{.bibleverse}");

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
			LogFile(panfile);
		}

		static async Task CreateTeXAsync(string mdfile, string texfile)
		{
			if (IsNewer(texfile, mdfile)) return;

			var mdtexfile = Path.Combine(Path.GetDirectoryName(mdfile), "tex", Path.GetFileName(mdfile));
			var src = File.ReadAllText(mdfile);
			src = Regex.Replace(src, @"\[([0-9]+)\]\{\.bibleverse\}", @"\bibleverse{$1}");
			src = Regex.Replace(src, @"\[([\u0590-\u05fe]+)\]\{\.hebrew\}", @"\hebrew{$1}");
			src = Regex.Replace(src, @"\[([\u0370-\u03ff\u1f00-\u1fff]+)\]\{\.greek\}", @"\greek{$1}");
			src = Regex.Replace(src, @"^(# .*?)$\n^(## .*?)$", "$2\n$1", RegexOptions.Multiline); // titles
			File.WriteAllText(mdtexfile, src);
			LogFile(mdtexfile);

			if (!string.IsNullOrWhiteSpace(src)) {
				Pandoc.RunAsync(mdtexfile, texfile, "markdown-smart", "latex");
				LogFile(texfile);
			}
		}

		static async Task CreateHtmlAsync(string mdfile, string htmlfile)
		{
			if (IsNewer(htmlfile, mdfile) || TwoLanguage) return;

			//var mdhtmlfile = Path.ChangeExtension(mdfile, ".html.md");

			//File.Copy(mdfile, mdhtmlfile);
			var src = File.ReadAllText(mdfile);
			//src = Regex.Replace(src, @"\^([0-9]+)\^", "<sup class='bibleverse'>$1</sup>", RegexOptions.Singleline);
			//File.WriteAllText(htmlfile, src);
			//Log(mdhtmlfile);

			if (!string.IsNullOrWhiteSpace(src))
			{
				await Pandoc.RunAsync(mdfile, htmlfile, "markdown-smart", "html");
				LogFile(htmlfile);
			}
		}


		static string Id(string name)
		{
			return name.Replace(' ', '-').Replace('.', '-');
		}

		static void CreateTwoLanguage(string path, string path1, string path2)
		{

			Log("Create Two Langauge...");
			var leftfiles = Directory.EnumerateFiles(path1, "*.md")
				.Select(file => new
				{
					Name = Books.Name(file),
					Number = Books.Number(file),
					File = file,
					Book = Books[LeftLanguage].ContainsKey(Books.Name(file)) ? Books[LeftLanguage][Books.Name(file)] : null,
					Text = File.ReadAllText(file),
				})
				.Where(book => book.Book != null)
				.OrderBy(book => book.Number)
				.ToArray();

			var rightfiles = Directory.EnumerateFiles(path2, "*.md")
				.Select(file => new
				{
					Name = Books.Name(file),
					Number = Books.Number(file),
					File = file,
					Book = Books[RightLanguage].ContainsKey(Books.Name(file)) ? Books[RightLanguage][Books.Name(file)] : null,
					Text = File.ReadAllText(file),
				})
				.Where(book => book.Book != null)
				.ToDictionary(book => book.Number);

			var books = leftfiles
				.Select(file => new
				{
					Left = file,
					Right = rightfiles.ContainsKey(file.Number) ? rightfiles[file.Number] : null,
					New = Path.Combine(path, $"{file.Number:d2}-{file.Name}.md")
				})
				.Where(book => book.Right != null && !IsNewer(book.New, book.Left.File) && !IsNewer(book.New, book.Right.File));

			foreach (var book in books)
			{

				var leftchapters = Regex.Matches(book.Left.Text, @"(?<=(^|\n))#[ \t]+(?<chapter>[0-9]+)[ \t]*\r?\n(?<text>.*?)(?=\r?\n#[ \t]+[0-9]+|\s*$)", RegexOptions.Singleline)
					.Select(match => new
					{
						Chapter = int.Parse(match.Groups["chapter"].Value),
						Text = match.Groups["text"].Value
					});
				var rightchapters = Regex.Matches(book.Right.Text, @"(?<=(^|\n))#[ \t]+(?<chapter>[0-9]+)[ \t]*\r?\n(?<text>.*?)(?=\r?\n#[ \t]+[0-9]+|\s*$)", RegexOptions.Singleline)
					.Select(match => new
					{
						Chapter = int.Parse(match.Groups["chapter"].Value),
						Text = match.Groups["text"].Value
					})
					.ToDictionary(chapter => chapter.Chapter);
				var text = new StringBuilder();
				foreach (var chapter in leftchapters)
				{
					text.AppendLine($@"\biblebeginparacol{Environment.NewLine}{Environment.NewLine}# {chapter.Chapter}");
					text.AppendLine(chapter.Text);
					text.AppendLine($@"\switchcolumn{Environment.NewLine}{Environment.NewLine}# {chapter.Chapter}");
					text.AppendLine(rightchapters[chapter.Chapter].Text);
					text.AppendLine(@"\bibleendparacol");
				}
				var newfile = Path.Combine(path, $"{book.Left.Number:d2}-{book.Left.Name}.md");
				File.WriteAllText(newfile, text.ToString());
				LogFile(newfile);
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
			LogFile(frames);
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
				bool replaced;
				do
				{
					replaced = false;
					txt = Regex.Replace(txt, @"\^(?<mark>[a-zA-Z]+)\^(?!\[)(?<text>.*?)(?:\^\k<mark>(?<footnote>\^\[(?>\[(?<c>)|[^\[\]]+|\](?<-c>))*(?(c)(?!))\]))[ \t]*\r?\n?", m =>
					{
						replaced = true;
						return $"{m.Groups["footnote"].Value}{m.Groups["text"].Value}";
					}, RegexOptions.Singleline);
				} while (replaced);

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

					var tokens = Regex.Matches(chaptertext,
						@"\^(?<verse>[0-9]+)\^|(?<footnote>\^\[(?>\[(?<c>)|[^\[\]]+|\](?<-c>))*(?(c)(?!))\])(?=(?<endofverse>\s*?((\^[0-9]+\^)|\n#|$)))|(?<=\r?\n)(?<blank>\r?\n)(?!\s*?(?:\^[a-zA-Z]+\^\[|#|$))(?=\s*\^[0-9]+\^)|(?<=\r?\n|^)##(?<title>.*?)(?=\r?\n|$)",
						RegexOptions.Singleline);
					int verse = 0;

					foreach (Match token in tokens)
						if (token.Groups["verse"].Success) verse = int.Parse(token.Groups["verse"].Value);
						/* else if (token.Groups["footnote"].Success && token.Groups["endofverse"].Success)
						{
							var item = new FootnoteItem(book, token.Groups["footnote"].Value, chapterItem.Chapter, verse);
							items.Add(item);
							bookItem.Items.Add(item);
						} don't add footnotes, only add linklist footnotes */
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

			// ImportParallelVerses(items);

			SortFramework(items);

			var result = new StringBuilder();
			Location lastlocation = Location.Zero;

			XElement? filexml = null, chapterxml = null;
			XElement root = new XElement("BibleFramework");
			foreach (var item in items)
			{
				if (item is BookItem)
				{
					var bookItem = (BookItem)item;
					result.AppendLine($"{Environment.NewLine}# {bookItem.Name}");
					filexml = new XElement("Book");
					filexml.Add(new XAttribute("Name", bookItem.Name));
					filexml.Add(new XAttribute("File", bookItem.File));
					root.Add(filexml);
				}
				else if (item is ChapterItem)
				{
					result.AppendLine($"{Environment.NewLine}## {item.Chapter}");
					chapterxml = new XElement("Chapter");
					chapterxml.Add(new XAttribute("Number", item.Chapter));
					if (filexml == null) Log("Error: No file for framework.");
					else filexml.Add(chapterxml);
				}
				else if (item is TitleItem)
				{
					var titleItem = (TitleItem)item;
					if (Location.Compare(lastlocation, item.Location) != 0) result.AppendLine($"^{item.Verse}^");
					var title = new XElement("Title");
					title.Value = titleItem.Title;
					title.Add(new XAttribute("Verse", item.Verse));
					if (chapterxml != null) chapterxml.Add(title);
					else Log("Error: No chapter for framework.");
					result.AppendLine($"{Environment.NewLine}###{titleItem.Title}");
				}
				else if (item is FootnoteItem)
				{
					var footnoteItem = (FootnoteItem)item;
					if (Location.Compare(lastlocation, item.Location) != 0) result.Append($"^{item.Verse}^ ");
					var footnote = new XElement("Footnote");
					footnote.Value = footnoteItem.Footnote;
					footnote.Add(new XAttribute("Verse", item.Verse));
					if (chapterxml != null) chapterxml.Add(footnote);
					else Log("Error: No chapter for framework.");
					result.Append(footnoteItem.Footnote); result.Append(' ');
				}
				else if (item is ParagraphItem)
				{
					if (Location.Compare(lastlocation, item.Location) != 0) result.Append($"^{item.Location.Verse}^ ");
					var paragraph = new XElement("Paragraph");
					paragraph.Add(new XAttribute("Verse", item.Verse));
					if (chapterxml != null) chapterxml.Add(paragraph);
					else Log("Error: No chapter for framework.");
					result.Append("\\ ");
				}
				lastlocation = item.Location;
			}

			File.WriteAllText(framesfile, result.ToString());
			string framesxml = Path.ChangeExtension(framesfile, ".xml");
			root.Save(framesxml);
			LogFile(framesfile);
			LogFile(framesxml);
		}

		static void CreateUSFM(string mdfile, string usfmfile)
		{
			if (IsNewer(usfmfile, mdfile) || TwoLanguage) return;

			string usfm = "";
			if (File.Exists(usfmfile)) usfm = File.ReadAllText(usfmfile);

			var name = Books.Name(mdfile);
			var txt = File.ReadAllText(mdfile);
			if (string.IsNullOrEmpty(usfm)) txt = @$"\h {name}{Environment.NewLine}\toc1 {name}{Environment.NewLine}{txt}";
			txt = Regex.Replace(txt, @"(^|\n)#[ \t]+([0-9]+)", @"\c $2", RegexOptions.Singleline);
			txt = Regex.Replace(txt, @"(^|\n)##[ \t]+(.*?)\r?\n", @"\s1 $2", RegexOptions.Singleline);
			txt = Regex.Replace(txt, @"(?<!^|\n)\[([0-9]+)\]\{\.bibleverse\}", $@"{Environment.NewLine}\v $1", RegexOptions.Singleline);
			txt = Regex.Replace(txt, @"\[([0-9]+)\]\{\.bibleverse\}", @"\v $1", RegexOptions.Singleline);
			txt = Regex.Replace(txt, @"\[([\u0590-\u05fe]+)\]\{\.hebrew\}", @"$1");
			txt = Regex.Replace(txt, @"\[([\u0370-\u03ff\u1f00-\u1fff]+)\]\{\.greek\}", @"$1");
			txt = Regex.Replace(txt, @"\*", "", RegexOptions.Singleline);

			// remove bibmark footnotes.
			bool replaced = true;
			while (replaced)
			{
				replaced = false;
				txt = Regex.Replace(txt, @"\^(?<mark>[a-zA-Z]+)\^(?!\[)(?<text>.*?)(?:\^\k<mark>(?<footnote>\^\[(?>\[(?<c>)|[^\[\]]+|\](?<-c>))*(?(c)(?!))\]))[ \t]*\r?\n?", m =>
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
			LogFile(usfmfile);
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
