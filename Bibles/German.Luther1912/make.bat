chcp 65001
..\..\bin\bibmark.exe -replace /HErrn/[Herrn]{.smallcaps}/HErr/[Herr]{.smallcaps}

cd tex
xelatex Bibel11ptB5 -output-directory=..\out\pdf
cd ..