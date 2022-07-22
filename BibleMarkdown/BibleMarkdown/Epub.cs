using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BibleMarkdown
{
	public class Epub
	{

		public static bool CreateChapterLinks = true;
		public static bool Links = true;
		public static string TableOfContentsPage = "ch001.xhtml";
		public static bool OmitTitles = true;
		public static bool OmitParagraphs = true;
		public static bool OmitFootnotes = true;
		public static Func<int, string> Page = book => $"ch{book:d3}.xhtml";

	}

	public partial class Program
	{
		public static void CreateEpub(string path, string mdfile, string epubfile)
		{
			if (IsNewer(epubfile, mdfile) || TwoLanguage) return;

			string? bookname = Books.Name(mdfile);
			int bookno = Books.Number(mdfile);

			var src = File.ReadAllText(mdfile);

			if (Epub.OmitTitles)
			{
				src = Regex.Replace(src, @"(?<=^|\n)##\s+.*?\r?\n", "", RegexOptions.Singleline);
			}

			if (Epub.OmitParagraphs)
			{
				src = Regex.Replace(src, @"(?<=\r?\n)[ \t]*\r?\n(?!#)", "", RegexOptions.Singleline);
			}

			if (Epub.OmitFootnotes)
			{

			}

			if (Epub.CreateChapterLinks)
			{
				var chapters = Regex.Matches(src, @"(?<=(^|\n)#\s+)[0-9]+", RegexOptions.Singleline);
				var links = new StringBuilder($@"<div id=""chapterlinks-{Id(bookname)}"" class=""chapterlinks"">");
				foreach (Match chapter in chapters)
				{
					links.Append($"[{chapter.Value}](#chapter-{Id(bookname)}-{chapter.Value}) ");
				}
				links.Append("</div>");
				links.AppendLine(); links.AppendLine();
				links.Append(src);
				src = links.ToString();
				// src = Regex.Replace(src, @"(?<=(^|\n)#\s+)([0-9]+)", $@"[$2](#book-{Id(book)}) {{.unnumbered #chapter-{Id(book)}-$2}}", RegexOptions.Singleline);
				src = Regex.Replace(src, @"(?<=(^|\n)#\s+)([0-9]+)", $@"[$2]({Epub.Page(bookno)}) {{.unnumbered #chapter-{Id(bookname)}-$2}}", RegexOptions.Singleline);
			}

			src = Regex.Replace(src, @"(?<=\n|^)#", "##", RegexOptions.Singleline);

			if (Epub.Links)
			{
				var pattern = String.Join('|', Books[Language].Values.Select(b => b.Abbreviation).ToArray());
				src = Regex.Replace(src, @$"(?<book>{pattern})\s+(?<chapter>[0-9]+)([:,](?<verse>[0-9]+)(-(?<upto>[0-9]+))?)", m =>
				{
					var bookabr = Books[Language].Values.FirstOrDefault(b => b.Abbreviation == m.Groups["book"].Value);
					var chapter = m.Groups["chapter"].Value;
					var verse = m.Groups["verse"].Value;
					if (!m.Groups["upto"].Success) return $@"[{m.Groups["book"].Value} {m.Groups["chapter"].Value},{m.Groups["verse"].Value}]({Epub.Page(bookabr.Number)}#verse-{Id(bookabr.Name)}-{chapter}-{verse})";
					else return $@"[{m.Groups["book"].Value} {m.Groups["chapter"].Value},{m.Groups["verse"].Value}-{m.Groups["upto"].Value}]({Epub.Page(bookabr.Number)}#verse-{Id(bookabr.Name)}-{chapter}-{verse})";
				}, RegexOptions.Singleline);

			}
			src = Regex.Replace(src, @"(?:^|\n)#[ \t]+(?<chapter>[0-9]+).*?(?=(?:\r?\n#[ \t]+[0-9]+|$))", chapter =>
			{
				return Regex.Replace(chapter.Value, @"\[(?<verse>[0-9]+)\]\{\.bibleverse\}", verse =>
				{
					return @$"[{verse.Groups["verse"].Value}]{{#verse-{Id(bookname)}-{chapter}-{verse}}}";
				}, RegexOptions.Singleline);
			}, RegexOptions.Singleline);

			src = $@"# [{bookname}]({Epub.TableOfContentsPage}) {{#book-{Id(bookname)}}}{Environment.NewLine}{Environment.NewLine}{src}";
			File.WriteAllText(epubfile, src);
			LogFile(epubfile);
		}
	}
}