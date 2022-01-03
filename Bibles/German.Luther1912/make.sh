#!/bin/bash
dotnet ../../bin/bibmark.dll -replace /HErrn/[Herrn]{.smallcaps}/HErr/[Herr]{.smallcaps}

xelatex tex/Bibel11ptB5 -output-directory=out/pdf
