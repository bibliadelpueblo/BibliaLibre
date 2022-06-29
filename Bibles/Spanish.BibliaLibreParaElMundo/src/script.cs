using System;

// Epub.Links = false;

Epub.OmitParagraphs = false;
Epub.Page = book => {
    book = book+3;
    return $"ch{book:d3}.xhtml";
};

Console.WriteLine("Added epub rule.");
