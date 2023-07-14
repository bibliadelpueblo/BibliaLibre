chcp 65001
..\..\bin\bibmark.exe

cd tex
@REM xelatex Biblia14ptB5
@REM del BibliaDelPinoleroLetraGrandeB5.pdf
@REM ren Biblia14ptB5.pdf BibliaDelPinoleroLetraGrandeB5.pdf
xelatex Biblia11ptB5 -output-directory=..\out\pdf
cd ..
