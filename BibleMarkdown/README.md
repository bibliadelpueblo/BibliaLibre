BibleMarkdown or bibmark.exe is an application that transforms USFM markup to Bible Markdown and then to LaTeX and HTML.

Bible Markdown is normal pandoc Markdown with the following extensions:
- For Footnotes you can make them more readable, by placing a marker ^label^ at the place of the footnote, but specifying the footnote later in the text with ordinary ^label^[Footnote] markdown. "label" must be a letter or word without any digits.
syntax.
- You can have comments by bracing them with two % characters. Comments can span multiple lines.
- Verse numbers are noted with superscript Markdown notation, like this ^1^ In the beginning was the Word and the Word was with God and the Word was God. ^2^ This was in the beginning...
- if the text contains the comment %!verse-paragraphs%, each verse is rendered in a paragraph. For use in Psalms and Proverbs.
- Chapter numbers are denoted with a # markdown title and Chapter headings with a ## markdown title
- A special comment %!verse-paragraphs% can be placed in the text, so that for this document, all verses are placed in a separate paragraph
- A special comment %!replace /regularexpression/replacement/regularexpression/replacement/...% can be placed in the text. All the regular expressions will be replaced. You can choose another delimiter char than /, the first character encountered will be used as delimiter.

If you have the source text of your Bible in USFM markup, you can place those files in a subfolder src. bibmark searches this folder for USFM source and creates Bible Markdown
files in the main folder if the source files are newer than the Bible Markdown files.
From the Bible Markdown files, bibmark creates LaTeX files in the out/tex folder and HTML files in the out/html folder.

bibmark also creates a file called frames.md in the out folder that specifies chapter titles and paragraphs and footnotes. If you move this file to the src folder and it is newer than the Bible Markdown files, bibmark applies the chapter titles and paragraphs and footnotes found in the frames.md file to the Bible Markdown files.
In the frames.md file, the Bible Markdown files are specified by a # markdown title, the chapter numbers by a ## markdown title, and chapter titles by a ### markdown title.
Verses that contain a paragraph or a footnote are denoted with superscript markdown notation followed by a \ for a paragraph or a ^^ for a footnote marker, or a ^[Footnote]
footnote.

bibmark also creates a file verses.md in the out folder, a file that shows how many verses each chapter has, so you can compare different Bibles verse numberings.
