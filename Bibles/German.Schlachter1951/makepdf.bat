chcp 65001
..\..\bin\bibmark.exe -replace /HERRN/[Herrn]{.smallcaps}/HERR/[Herr]{.smallcaps}}

cd tex
xelatex Bibel11ptB5 -output-directory=..\out\pdf
cd ..