using System;

// Program.EpubLinks = false;

Verses.EpubPage = book => {
    book = book+3;
    return $"ch{book:d3}.xhtml";
};

Console.WriteLine("Added epub rule.");
