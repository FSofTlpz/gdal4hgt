# gdal4hgt
converts geotiffs to hgt and vice versa

It' s a extension for gdal (www.gdal.org). You need to install gdal and then copy gdal4hgt.exe to the csharp directory f.e. c:\GDAL\bin\gdal\csharp.
I have it only testet with the 64-bit-version.

--hgt=arg             hgt-filename or path to hgt files to convert (multiple usable)

-r, --raster=arg      tiff- or vrt-filename to convert

-d, --dstpath=arg     destination path

-c, --compr=arg       compress the result (default 'true')

--nodata=arg          value for 'nodata' (default -32768)

-v, --vrt=arg         create additional vrt-file(s) (one for ever number of pixels in HGT's; , default 'true')

--info=arg            only show infos for input files (without arg 'true', default 'false')

-O, --overwrite=arg   overwrite destination files (without arg 'true', default 'false')


      gdal4hgt --hgt=myhgt--dstpath=mytiff

create for all hgt files in myhgt a geotiff file in mytiff and additional a vrt file for every resolution (1201.vrt and/or 3601.vrt)

      gdal4hgt --raster=mytiff --dstpath=myhgt2

create for all geotiff files in mytiff a hgt file in myhgt2
