# Biblia Libre

Biblia Libre is a collection of OpenSource & Public Domain Bibles. The Bibles use a specific version of Markdown, BibleMarkdown. BibleMarkdown is normal pandoc Markdown, with the following extensions:

- You can put a marker ^letters^ at a place where you want to have a footnote, and put the footnote later in the text with regular Markdown ^letters^[The footnote] syntax.
- You can have comments, surrounded by /*  */ signs, like /* This is a comment */ or by // following
  a comment until the end of the line like this:
  // This is a comment.
   A /* */ comment can span multiple lines.
- Verse numbers are noted with superscript Markdown notation, like this ^1^ In the beginning was the Word and the Word was with God and the Word was God. ^2^ This was in the beginning...

To edit the Markdown of the Bibles, you can use a normal editor like Typora, stackedit.io or VisualStudio Code.

The conversion from BibleMarkdown to other formats is done by a tool called bibmark. 
bibmark processes all the .md files in the current directory and converts them to other formats in the "out" subdirectory. The md files in the current directory must follow a naming schema, of two digits followed by a minus and the name of the bible book, e.g. like 01-Genesis.md or 02-Exodus.md. Bibmark only processes files with names adhering to that schema. The md files can be constructed from various source formats. For this, the source files must be placed in the subdirectory "src". In the "src" subdirectory you can place USFM files or zefania xml files, or a BibleEdit folder. You can also place a script.cs file in the "src" folder that will be executed when running bibmark, that can configure bibmark for certain tasks. Next you can place a file booknames.xml in the "src" subdirectory that contains names of Bible books in different languages. The names of the books should correspond to the titles of the books in the USFM files. Then you can also import a Parallel Verses file, linklist.xml, that contains parallel verses.
