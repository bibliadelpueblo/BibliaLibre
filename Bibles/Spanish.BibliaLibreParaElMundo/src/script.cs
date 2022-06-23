using System;

Verses.EpubPage = book => {
    book = book+2;
    return $"ch{book:d3}.xhtml";
};

Console.WriteLine("Added epub rule.");
