using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BibleMarkdown
{
	public enum FrameworkItemClass { Book, Chapter, Title, Footnote, Paragraph }
	public class FrameworkItem
	{
		public Location Location;
		public FrameworkItemClass Class
		{
			get
			{
				if (this is BookItem) return FrameworkItemClass.Book;
				if (this is ChapterItem) return FrameworkItemClass.Chapter;
				if (this is TitleItem) return FrameworkItemClass.Title;
				if (this is FootnoteItem) return FrameworkItemClass.Footnote;
				if (this is ParagraphItem) return FrameworkItemClass.Paragraph;
				else throw new NotSupportedException();
			}
		}

		public int Verse { get { return Location.Verse; } set { Location.Verse = value; } }
		public int Chapter { get { return Location.Chapter; } set { Location.Verse = value; } }

		public FrameworkItem(Book book, int chapter = 0, int verse = -1)
		{
			Location = new Location()
			{
				Book = book,
				Chapter = chapter,
				Verse = verse
			};
		}
	}

	public class BookItem : FrameworkItem
	{
		public string Name;
		public string File;
		public bool VerseParagraphs;
		public bool MapVerses;
		public List<FrameworkItem> Items = new List<FrameworkItem>();
		public BookItem(Book book, string file) : base(book, 0, -1)
		{
			Name = book.Name;
			File = file;
		}
	}

	public class ChapterItem : FrameworkItem
	{
		public ChapterItem(Book book, int chapter) : base(book, chapter, -1) { }
	}
	public class TitleItem : FrameworkItem
	{
		public string Title;

		public TitleItem(Book book, string title, int chapter, int verse) : base(book, chapter, verse)
		{
			Title = title;
		}
	}

	public class FootnoteItem : FrameworkItem
	{
		public string Footnote;

		public FootnoteItem(Book book, string footnote, int chapter, int verse) : base(book, chapter, verse)
		{
			Footnote = footnote;
		}
	}

	public class ParagraphItem : FrameworkItem
	{
		public ParagraphItem(Book book, int chapter, int verse) : base(book, chapter, verse) { }
	}

}