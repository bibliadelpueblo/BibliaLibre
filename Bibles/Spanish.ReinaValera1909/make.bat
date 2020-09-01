chcp 65001
..\..\bin\bibmark.exe
cd tex
xelatex Biblia14ptB5
del BibliaDelOsoPinolero.pdf
ren Biblia14ptB5.pdf BibliaDelOsoPinolero.pdf
cd ..