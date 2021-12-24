chcp 65001
..\..\bin\net6.0\bibmark.exe
cd tex
@REM xelatex Biblia14ptB5
@REM del BibliaDelPinoleroLetraGrandeB5.pdf
@REM ren Biblia14ptB5.pdf BibliaDelPinoleroLetraGrandeB5.pdf
xelatex Biblia11ptB5
del BibliaDelPinoleroB5.pdf
ren Biblia11ptB5.pdf BibliaDelPinoleroB5.pdf

del ..\pdf\BibliaDelPinoleroB5.pdf
move BibliaDelPinoleroB5.pdf ..\pdf
@REM move BibliaDelPinoleroLetraGrandeB5.pdf ..\pdf

cd ..