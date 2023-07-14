chcp 65001
..\..\bin\bibmark.exe

cd out\pandoc\epub
pandoc -o ..\..\epub\WorldEnglishBible.epub --from markdown-smart --epub-embed-font "..\..\..\..\..\Fonts\DayRoman*.ttf" --css=..\..\..\style.css "00.0-Table of contents.md" 00.1-Impressum.md 00.2-Preface.md 01-Genesis.md 02-Exodus.md 03-Leviticus.md 04-Numbers.md 05-Deuteronomy.md 06-Joshua.md 07-Judges.md 08-Ruth.md "09-1 Samuel.md" "10-2 Samuel.md" "11-1 Kings.md" "12-2 Kings.md" "13-1 Chronicles.md" "14-2 Chronicles.md" 15-Ezra.md 16-Nehemiah.md 17-Esther.md 18-Job.md 19-Psalm.md 20-Proverbs.md 21-Ecclesiastes.md "22-Song of Solomon.md" 23-Isaiah.md 24-Jeremiah.md 25-Lamentations.md 26-Ezekiel.md 27-Daniel.md 28-Hosea.md 29-Joel.md 30-Amos.md 31-Obadiah.md 32-Jonah.md 33-Micah.md 34-Nahum.md 35-Habakkuk.md 36-Zephaniah.md 37-Haggai.md 38-Zechariah.md 39-Malachi.md "40-Matthew.md" "41-Mark.md" "42-Luke.md" "43-John.md" 44-Acts.md 45-Romans.md "46-1 Corinthians.md" "47-2 Corinthians.md" 48-Galatians.md 49-Ephesians.md 50-Philippians.md 51-Colossians.md "52-1 Thessalonians.md" "53-2 Thessalonians.md" "54-1 Timothy.md" "55-2 Timothy.md" 56-Titus.md 57-Philemon.md 58-Hebrews.md 59-James.md "60-1 Peter.md" "61-2 Peter.md" "62-1 John.md" "63-2 John.md" "64-3 John.md" "65-Jude.md" "66-Revelation.md" 00.3-Glossary.md

cd ..\..\..