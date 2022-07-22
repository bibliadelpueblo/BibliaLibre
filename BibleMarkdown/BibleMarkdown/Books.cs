using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BibleMarkdown
{
	public class Book
	{
		public string? Language;
		public string Name = "";
		public string Abbreviation = "";
		public int Number;
	}

	public class BookList : SortedList<string, SortedList<string, Book>>
	{

		public static string Language => Program.Language;

		public Book this[string language, int number]
		{
			get
			{
				return base[language]
					.FirstOrDefault(b => b.Value.Number == number)
					.Value;
			}
		}
		public Book this[string language, string bookname]
		{
			get
			{
				if (base[language].ContainsKey(bookname)) return base[language][bookname];
				else return new Book() { Number = 0, Abbreviation = "", Language = language, Name = bookname };
			}
		}

		public Book this[int number] => this[Language, number];

		public string Name(string file)
		{
			return Regex.Replace(Path.GetFileNameWithoutExtension(file), "^[0-9.]+-", "", RegexOptions.Singleline).Trim();
		}
		public int Number(string file)
		{
			var match = Regex.Match(Path.GetFileNameWithoutExtension(file), "^[0-9]+", RegexOptions.Singleline);
			if (match.Success) return int.Parse(match.Value);
			return -1;
		}
		public void Load(string path)
		{
			if (Count > 1) return;

			var namesfile = Path.Combine(path, "src", "booknames.xml");

			if (!File.Exists(namesfile)) return;

			XElement xml;
			using (var file = File.Open(namesfile, FileMode.Open, FileAccess.Read))
			{
				xml = XElement.Load(file);
			}

			Program.Log($"Importing booknames.xml.");

			Program.Language = (string?)xml.Attribute("default") ?? Language;

			var languages = xml
				.Elements("ID")
				.Select(langset => new
				{
					Language = (string)langset.Attribute("descr"),
					Books = langset.Elements("BOOK")
						.Select(xml => new Book
						{
							Language = Language,
							Name = xml.Value,
							Abbreviation = (string)xml.Attribute("bshort"),
							Number = (int)xml.Attribute("bnumber")
						})
				});

			foreach (var lang in languages)
			{
				var books = new SortedList<string, Book>();
				Add(lang.Language, books);
				foreach (var book in lang.Books)
				{
					books.Add(book.Name, book);
				}
			}
		}

		public void Load(IEnumerable<string> mdfiles)
		{
			if (!ContainsKey("default")) Add("default", new SortedList<string, Book>());
			
			var books = this["default"];

			if (books.Count > 0) return;

			foreach (var file in mdfiles)
			{
				var book = new Book() { Language = null, Abbreviation = "", Name = Name(file), Number = Number(file) };
				books.Add(book.Name, book);
			}
		}

		public IEnumerable<Book> All => Values.SelectMany(lang => lang.Values);

		public static BookList Books = new BookList();
	}

	public class ParallelVerse
	{
		public Location Verse;
		public Location[] ParallelVerses;
	}

	public class ParallelVerseList : List<ParallelVerse>
	{
		
		public void Load(string path)
		{
			_path = path;
		}

		void RunLoad(string path)
		{
			if (Count != 0) return;

			var linklistfile = Path.Combine(path, "src", "linklist.xml");

			if (!File.Exists(linklistfile)) return;

			Program.Log("Importing linklist.xml");

			BookList.Books.Load(path);

			XElement xml;
			using (var file = File.Open(linklistfile, FileMode.Open, FileAccess.Read))
			{
				xml = XElement.Load(file);
			}
			var verses = xml
				.Descendants("verse")
				.Select(x =>
				{

					var book = BookList.Books[(int)x.Attribute("bn")];
					return new ParallelVerse
					{
						Verse = VerseMaps.ParallelVerses.Map(new Location
						{
							Book = book,
							Chapter = (int)x.Attribute("cn"),
							Verse = (int)x.Attribute("vn"),
							UpToVerse = null
						}),
						ParallelVerses = x.Elements("link")
							.Select(link =>
							{
								var linkbook = BookList.Books[(int)link.Attribute("bn")];
								var verse = VerseMaps.ParallelVerses.Map(new Location
								{
									Book = linkbook,
									Chapter = (int)link.Attribute("cn1"),
									Verse = Math.Abs((int)link.Attribute("vn1")),
									UpToVerse = link.Attribute("vn2") != null ? (int)link.Attribute("vn2") : null
								});
								verse.Verse = verse.Verse < 1 ? 1 : verse.Verse;
								return verse;
							})
							.ToArray()
					};
				})
				.OrderBy(v => v.Verse.Book.Number)
				.ThenBy(v => v.Verse.Chapter)
				.ThenBy(v => v.Verse.Verse);

			foreach (var verse in verses) Add(verse);
		}

		static ParallelVerseList parallelVerses = new ParallelVerseList();
		public static string _path;
		public static ParallelVerseList ParallelVerses
		{
			get {
				if (parallelVerses.Count == 0 && _path != null) parallelVerses.RunLoad(_path);
				return parallelVerses; }
		}
	}
	public class VerseMaps : Dictionary<string, SortedList<Location, Location>>
	{
		public static VerseMaps ParallelVerses = new VerseMaps();
		public static VerseMaps Paragraphs = new VerseMaps();
		public static VerseMaps Titles = new VerseMaps();
		public static VerseMaps Footnotes = new VerseMaps();
		public static VerseMaps DualLanguage = new VerseMaps();
		public static string Test = "";

		public Location Map(Location verse)
		{
			if (verse.Verse < 0) verse.Verse = 1;
			if (verse.Verse == 0)
			{

			}
			SortedList<Location, Location> book;
			if (!TryGetValue(verse.Book.Name, out book)) return verse;
			int i = 0, j = book.Count-1, m = 0;
			m = (i + j + 1) / 2;
			Location key = book.Keys[m];
			Location dest;
			while (j>i)
			{

				if (key.Chapter > verse.Chapter || key.Chapter == verse.Chapter && key.Verse > verse.Verse)
				{
					j = m-1;
				}
				else if (key.Chapter == verse.Chapter && key.Verse == verse.Verse)
				{
					i = j = m;
				}
				else
				{ 
					i = m;
				}
				m = (i + j + 1) / 2;
				key = book.Keys[m];
			}
			if (key != null && (key.Chapter > verse.Chapter || key.Chapter == verse.Chapter && key.Verse > verse.Verse)) {
				key = null;
			}
			if (key == null) return verse;
			dest = book.Values[m];
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
					Program.Log($"Verse mapped from {verse.Book.Name} {verse.Chapter}:{verse.Verse} to {loc.Chapter}:{loc.Verse}.");
				}
				if (verse.UpToVerse.HasValue)
				{
					var upto = new Location
					{
						Book = verse.Book,
						Chapter = verse.Chapter,
						Verse = verse.UpToVerse ?? 0,
						UpToVerse = null
					};
					upto = Map(upto);
					if (upto.Chapter != loc.Chapter || upto.Verse < loc.Verse)
					{
						loc.UpToVerse = null;
					}
					else
					{
						loc.UpToVerse = upto.Verse;
					}
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
			var books = Regex.Matches(src, @"(?<=^|\n)#\s+(?<book>.*?)[ \t]*\r?\n(?<map>.*?)(?=\r?\n#\s)", RegexOptions.Singleline)
				.Select(m => new
				{
					Book = m.Groups["book"].Value,
					Map = m.Groups["map"].Value
				});
			foreach (var book in books)
			{
				var bookfromlist = BookList.Books[Program.Language].Values.FirstOrDefault(b => b.Name == book.Book);

				if (bookfromlist == null)
				{
					Program.Log($"Error: No book called {book.Book} booknames.xml");
					continue;
				}
				var map = Regex.Matches(book.Map, @"([0-9]+):([0-9]+)=>([0-9]+):([0-9]+)", RegexOptions.Singleline)
					.Select(m => new
					{
						From = new Location()
						{
							Book = bookfromlist,
							Chapter = int.Parse(m.Groups[1].Value),
							Verse = int.Parse(m.Groups[2].Value)
						},
						To = new Location()
						{
							Book = bookfromlist,
							Chapter = int.Parse(m.Groups[3].Value),
							Verse = int.Parse(m.Groups[4].Value)
						}
					})
					.ToArray();

				if (map.Any())
				{
					var list = new SortedList<Location, Location>();
					Add(book.Book, list);
					foreach (var node in map)
					{
						list.Add(node.From, node.To);
					}
				}
			}
		}

		public static void Load(string path)
		{
			BookList.Books.Load(path);

			var srcpath = Path.Combine(path, "src");
			ParallelVerses.Import(Path.Combine(srcpath, "parallelversesmap.md"));
			Paragraphs.Import(Path.Combine(srcpath, "paragraphsmap.md"));
			Titles.Import(Path.Combine(srcpath, "titlesmap.md"));
			Footnotes.Import(Path.Combine(srcpath, "footnotesmap.md"));
			DualLanguage.Import(Path.Combine(srcpath, "duallanguagemap.md"));

			ParallelVerseList.ParallelVerses.Load(path);
		}
	}

	public class Location: IComparable<Location>
	{
		public Book Book;
		public int Chapter;
		public int Verse;
		public int? UpToVerse;

		public static Location Zero => new Location()
		{
			Book = new Book() { Name = "", Number = 0, Abbreviation = "", Language = "default" },
			Chapter = 0,
			Verse = -1
		};
		public static int Compare(Location a, Location b)
		{
			if (a.Book.Number < b.Book.Number) return -1;
			if (a.Book.Number > b.Book.Number) return 1;
			if (a.Chapter < b.Chapter) return -1;
			if (a.Chapter > b.Chapter) return 1;
			if (a.Verse < b.Verse) return -1;
			if (a.Verse > b.Verse) return 1;
			return 0;
		}
		int IComparable<Location>.CompareTo(Location? other) => Compare(this, other);
	}

	partial class Program
	{
		public static BookList Books => BookList.Books;
		public static ParallelVerseList ParallelVerses => ParallelVerseList.ParallelVerses;

		public static void Init() { Books.Add("default", new SortedList<string, Book>()); }
	}
}