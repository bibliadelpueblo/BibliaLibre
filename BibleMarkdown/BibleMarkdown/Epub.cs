using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
		public static Func<int, string> Page = book => $"ch{book:d3}.xhtml";

	}
}
