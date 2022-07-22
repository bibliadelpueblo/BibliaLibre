namespace BibleMarkdown
{
	public enum FrameworkItemClass { Book, Chapter, Title, Footnote, Paragraph, Verse }
	public class FrameworkItem: IComparable<FrameworkItem>
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

		public int CompareTo(FrameworkItem? other)
		{
			var sameloc = Location.Compare(Location, other.Location);
			if (sameloc != 0) return sameloc;
			if (this is ParagraphItem || this is TitleItem)
				if (!(other is ParagraphItem ||other is TitleItem)) return 1;
				else return 0;
			else if (other is ParagraphItem || other is TitleItem) return -1;
			else return 0;
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