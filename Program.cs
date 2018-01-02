using OSGeo.GDAL;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace Gdal4Hgt {

   class HGTSource {
      public string path;
      public int lon;
      public int lat;

      public HGTSource(string path, int lon, int lat) {
         this.path = path;
         this.lon = lon;
         this.lat = lat;
      }
   }

   class VRTSource {
      public string file;
      public int left;
      public int top;
      public int width;
      public int height;
      public int lon;
      public int lat;

      public VRTSource(string file, int left, int top, int width, int height, int lon, int lat) {
         this.file = file;
         this.left = left;
         this.top = top;
         this.width = width;
         this.height = height;
         this.lon = lon;
         this.lat = lat;
      }
   }

   /*
    *  West    East
    *    |      |
    * 5  XXXXXXXX - North
    * 4  XXXXXXXX
    * 3  XXXXXXXX - South
    *    01234567
    * 
    * Height = abs(North - South) + 1   > 0, if not empty      abs(5-3)+1=3
    * Width  =     East - West    + 1   > 0, if not empty          7-0 +1=8
    * 
    */

   /// <summary>
   /// Rechteck für x-y-Koordinaten (y nach unten!) und Verbindung zu lon/lat (lat nach oben!)
   /// <para>Der Koordinatenursprung ist in beiden Fällen [0,0].</para>
   /// </summary>
   class RectangleXY {

      int _left;
      int _top;
      int _width;
      int _height;

      int dx180;
      int dx360;
      int dy90;
      int dy180;

      /// <summary>
      /// liefert oder setzt den westlichen Rand
      /// </summary>
      public int West {
         get {
            return _left;
         }
         set {
            _left = ValidX(value);
         }
      }

      /// <summary>
      /// liefert oder setzt den nördlichen Rand
      /// </summary>
      public int North {
         get {
            return _top;
         }
         set {
            _top = ValidY(value);
         }
      }

      /// <summary>
      /// liefert oder setzt die Breite (die Anzahl der Punkte in der Breite ist 1 größer!)
      /// </summary>
      public int Width {
         get {
            return _width;
         }
         set {
            _width = Math.Max(0, Math.Min(dx360, value));
         }
      }

      /// <summary>
      /// liefert oder setzt die Höhe (die Anzahl der Punkte in der Höhe ist 1 größer!)
      /// </summary>
      public int Height {
         get {
            return _height;
         }
         set {
            _height = Math.Max(0, Math.Min(dy90 - North, value));
         }
      }

      /// <summary>
      /// liefert den östlichen Rand
      /// </summary>
      public int East {
         get {
            int tmp = West + Width;
            while (tmp > dx180)
               tmp -= dx360;
            return tmp;
         }
      }

      /// <summary>
      /// liefert den südlichen Rand
      /// </summary>
      public int South {
         get {
            return North + Height;
         }
      }

      /// <summary>
      /// liefert den westlichen Rand in Grad
      /// </summary>
      public double LonWest {
         get {
            return X2Lon(_left);
         }
      }

      /// <summary>
      /// liefert den östlichen Rand in Grad
      /// </summary>
      public double LonEast {
         get {
            return X2Lon(East);
         }
      }

      /// <summary>
      /// liefert den nördlichen Rand in Grad
      /// </summary>
      public double LatNorth {
         get {
            return Y2Lat(_top);
         }
      }

      /// <summary>
      /// liefert den südlichen Rand in Grad
      /// </summary>
      public double LatSouth {
         get {
            return Y2Lat(South);
         }
      }

      /// <summary>
      /// liefert die Auflösung für 1 Grad (i.A. 1201 bzw. 3601)
      /// </summary>
      public int Resolution { get; private set; }

      /// <summary>
      /// 
      /// </summary>
      /// <param name="resolution">Anzahl der Punkte für einen 1°-Bereich</param>
      public RectangleXY(int resolution) {
         Resolution = resolution;
         dx180 = dy180 = 180 * (Resolution - 1);
         dx360 = 2 * dx180;
         dy90 = dy180 / 2;
         West = North = Width = Height = 0;
      }

      public RectangleXY(int west, int north, int width, int height, int resolution) :
         this(resolution) {
         West = west;
         North = north;
         Width = width;
         Height = height;
      }

      public RectangleXY(double west, double north, double width, double height, int resolution) :
         this(resolution) {
         West = Lon2X(west);
         North = Lat2Y(north);
         Width = Lon2X(width);
         Height = Lat2Y(-height);
      }

      public RectangleXY(RectangleXY rect) {
         Resolution = rect.Resolution;
         dx180 = rect.dx180;
         dx360 = rect.dx360;
         dy90 = rect.dy90;
         dy180 = rect.dy180;
         West = rect.West;
         North = rect.North;
         Width = rect.Width;
         Height = rect.Height;
      }

      public RectangleXY Intersection(RectangleXY rect) {
         // links, rechts vom 1. Rechteck
         int w1 = West;
         int e1 = East;
         if (e1 < w1)
            e1 += dx360;
         // links, rechts vom 2. Rechteck
         int w2 = rect.West;
         int e2 = rect.East;
         if (e2 < w2)
            e2 += dx360;

         int west = int.MinValue;
         if (w1 <= w2 && w2 <= e1)        // linker Rand vom 2. Rechteck innerhalb des 1. Rechtecks
            west = w2;
         else if (w2 <= w1 && w1 <= e2)   // linker Rand vom 1. Rechteck innerhalb des 2. Rechtecks
            west = w1;

         if (west > int.MinValue) {       // Überlappung für linken Rand ex.
            int east = Math.Min(e1, e2);  // rechter Rand muss dass Min. der beiden rechten Ränder sein
            int n1 = North;
            int s1 = South;
            int n2 = rect.North;
            int s2 = rect.South;          // Koordinatensystem NACH UNTEN !

            int north = int.MinValue;
            if (n1 <= n2 && n2 <= s1)     // oberer Rand vom 2. Rechteck innerhalb des 1. Rechtecks
               north = n2;
            else if (n2 <= n1 && n1 <= s2) // oberer Rand vom 1. Rechteck innerhalb des 2. Rechtecks
               north = n1;

            if (north > int.MinValue)     // Überlappung für oberen Rand ex.
               return new RectangleXY(west, north, east - west, Math.Min(s1, s2) - north, Resolution);
         }
         return null;
      }

      public bool IsEmpty() {
         return Width == 0 || Height == 0;
      }


      /// <summary>
      /// rechnet einen x-Wert in eine geogr. Länge entsprechend der Auflösung um
      /// </summary>
      /// <param name="x"></param>
      /// <returns></returns>
      protected double X2Lon(int x) {
         return (double)ValidX(x) / (Resolution - 1);
      }
      /// <summary>
      /// rechnet eine geogr. Länge in einen x-Wert entsprechend der Auflösung um
      /// </summary>
      /// <param name="lon"></param>
      /// <returns></returns>
      public int Lon2X(double lon) {
         return ValidX((int)Math.Round((Resolution - 1) * lon));
      }
      /// <summary>
      /// rechnet einen y-Wert in eine geogr. Breite entsprechend der Auflösung um
      /// </summary>
      /// <param name="y"></param>
      /// <returns></returns>
      protected double Y2Lat(int y) {
         return -(double)ValidY(y) / (Resolution - 1);
      }
      /// <summary>
      /// rechnet eine geogr. Breite in einen y-Wert entsprechend der Auflösung um
      /// </summary>
      /// <param name="lat"></param>
      /// <returns></returns>
      public int Lat2Y(double lat) {
         return -ValidY((int)Math.Round((Resolution - 1) * lat));
      }

      /// <summary>
      /// liefert den größten Rasterwert (siehe <see cref="Resolution"/>), der kleiner oder gleich v ist
      /// </summary>
      /// <param name="v"></param>
      /// <returns></returns>
      protected int GetLeftRasterValue(int v) {
         int r;
         int left = Math.DivRem(v, Resolution - 1, out r);
         if (r != 0 &&
             v < 0)
            left--;
         return left * (Resolution - 1);
      }
      /// <summary>
      /// liefert den kleinsten Rasterwert (siehe <see cref="Resolution"/>), der größer oder gleich v ist
      /// </summary>
      /// <param name="v"></param>
      /// <returns></returns>
      protected int GetRightRasterValue(int v) {
         int r;
         int right = Math.DivRem(v, Resolution - 1, out r);
         if (r != 0 &&
             v > 0)
            right++;
         return right * (Resolution - 1);
      }


      /// <summary>
      /// liefert den größten Rasterwert (siehe <see cref="Resolution"/>) der kleiner oder gleich dem linken Rand ist
      /// </summary>
      public int WestlyRaster {
         get {
            return ValidX(GetLeftRasterValue(West));
         }
      }

      /// <summary>
      /// liefert den kleinsten Rasterwert (siehe <see cref="Resolution"/>) der größer oder gleich dem linken Rand ist
      /// </summary>
      public int EastlyRaster {
         get {
            return ValidX(GetRightRasterValue(West + Width));
         }
      }

      /// <summary>
      /// liefert den größten Rasterwert (siehe <see cref="Resolution"/>) der kleiner oder gleich dem oberen Rand ist
      /// </summary>
      public int NorthlyRaster {
         get {
            return ValidY(GetLeftRasterValue(North));
         }
      }

      /// <summary>
      /// liefert den kleinsten Rasterwert (siehe <see cref="Resolution"/>) der größer oder gleich dem unetren Rand ist
      /// </summary>
      public int SouthlyRaster {
         get {
            return ValidY(GetRightRasterValue(North + Height));
         }
      }

      /// <summary>
      /// liefert die größte geogr. Länge, die kleiner oder gleich dem linken Rand ist
      /// </summary>
      public int WestlyWholeNumberedLon {
         get {
            return (int)X2Lon(WestlyRaster);
         }
      }

      /// <summary>
      /// liefert die kleinste geogr. Länge, die größer oder gleich dem linken Rand ist
      /// </summary>
      public int EastlyWholeNumberedLon {
         get {
            return (int)X2Lon(EastlyRaster);
         }
      }

      /// <summary>
      /// liefert die kleinste geogr. Breite, die nördlich des Randes liegt
      /// </summary>
      public int NorthlyWholeNumberedLat {
         get {
            return (int)Y2Lat(NorthlyRaster);
         }
      }

      /// <summary>
      /// liefert die größte geogr. Breite, die südlich des Randes liegt
      /// </summary>
      public int SouthlyWholeNumberedLat {
         get {
            return (int)Y2Lat(SouthlyRaster);
         }
      }

      /// <summary>
      /// liefert ein Bounding-Rectangle mit ganzzahligen Lon/Lat
      /// </summary>
      /// <returns></returns>
      public RectangleXY GetWholeNumberedBounding() {
         int right = EastlyRaster;
         if (right < WestlyRaster)
            right += dx360;
         RectangleXY rc = new RectangleXY(WestlyRaster,
                                          NorthlyRaster,
                                          right - WestlyRaster,
                                          SouthlyRaster - NorthlyRaster,
                                          Resolution);
         return rc;
      }

      /// <summary>
      /// liefert ein x im gültigen Wertebereich
      /// </summary>
      /// <param name="x"></param>
      /// <returns></returns>
      protected int ValidX(int x) {
         while (x > dx180)
            x -= dx360;
         while (x < -dx180)
            x += dx360;
         return x;
      }
      /// <summary>
      /// liefert ein y im gültigen Wertebereich
      /// </summary>
      /// <param name="y"></param>
      /// <returns></returns>
      protected int ValidY(int y) {
         while (y > dy90)
            y -= dy180;
         while (y < -dy90)
            y += dy180;
         return y;
      }

      public bool Equals(RectangleXY rc) {
         if ((object)rc == null) // NICHT "p == null" usw. --> f³hrt zur Endlosschleife
            return false;

         // Return true if the fields match:
         return (West == rc.West) &&
                (North == rc.North) &&
                (Width == rc.Width) &&
                (Height == rc.Height) &&
                (Resolution == rc.Resolution);
      }

      public override bool Equals(object obj) {
         if (obj == null)
            return false;

         // If parameter cannot be cast to Point return false.
         RectangleXY rc = obj as RectangleXY;
         if (rc == null)
            return false;

         return (West == rc.West) &&
                (North == rc.North) &&
                (Width == rc.Width) &&
                (Height == rc.Height) &&
                (Resolution == rc.Resolution);
      }

      public override int GetHashCode() {
         return base.GetHashCode();
      }

      public static bool operator ==(RectangleXY a, RectangleXY b) {
         // If both are null, or both are same instance, return true.
         if (ReferenceEquals(a, b))
            return true;

         return (object)a != null && // NICHT "a == null" usw. --> Endlosschleife
                 a.Equals(b);
      }

      public static bool operator !=(RectangleXY a, RectangleXY b) {
         return !(a == b);
      }

      public override string ToString() {
         return dx360 > 0 ? string.Format("Resolution {0} [{1} .. {2} / {3} .. {4}], [{5}° .. {6}° / {7}° .. {8}°]",
                                          Resolution,
                                          West,
                                          East,
                                          North,
                                          South,
                                          LonWest,
                                          LonEast,
                                          LatNorth,
                                          LatSouth) :
                              "not initialized";
      }

   }


   class Program {

      /* commandlines for test
       
         --hgt=hgt --dstpath=tiff --overwrite
         --raster=tiff\3601.vrt --dstpath=hgtclip --overwrite

         --raster=c:\Users\puf\Gps\Projekte\ClippingHgt\neu3601.tif --dstpath=c:\Users\puf\Gps\Projekte\ClippingHgt\hgtclip --overwrite

       */

      static void Main(string[] args) {
         Assembly a = Assembly.GetExecutingAssembly();
         string progname = ((AssemblyProductAttribute)(Attribute.GetCustomAttribute(a, typeof(AssemblyProductAttribute)))).Product + ", Version vom " +
                           ((AssemblyInformationalVersionAttribute)(Attribute.GetCustomAttribute(a, typeof(AssemblyInformationalVersionAttribute)))).InformationalVersion + ", " +
                           ((AssemblyCopyrightAttribute)(Attribute.GetCustomAttribute(a, typeof(AssemblyCopyrightAttribute)))).Copyright;
         Console.Error.WriteLine(progname);

         Console.Error.WriteLine("64 Bit-OS: {0}", Environment.Is64BitOperatingSystem ? "yes" : "no");
         Console.Error.WriteLine("Programmode 64 Bit: {0}", Environment.Is64BitProcess ? "yes" : "no");

#if DEBUG
         // string GDALpath = @"C:\Users\puf\gps\bin\GDAL";
         string GDALpath = @"p:\Gps\Bin\GDAL";

         string orgpath = Environment.GetEnvironmentVariable("PATH");
         if (!orgpath.Contains(@"bin\gdal\csharp") &&
             !string.IsNullOrEmpty(GDALpath)) {
            Environment.SetEnvironmentVariable("GDAL_CACHEMAX", @"6000", EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("GDAL_DRIVER_PATH", Path.Combine(GDALpath, @"bin\gdal\plugins"), EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("PROJ_LIB", Path.Combine(GDALpath, @"bin\proj\SHARE"), EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("PATH", Path.Combine(GDALpath, @"bin\gdal\csharp") + ";" +
                                                       Path.Combine(GDALpath, @"bin") + ";" +
                                                       orgpath, EnvironmentVariableTarget.Process);
         }
#endif

         try {

            Options opt = new Options();
            opt.Evaluate(args);

            try {
               Gdal.AllRegister();
               Console.Error.WriteLine("using " + Gdal.VersionInfo("--version"));      // Returns one line version message suitable for use in response to --version requests. ie. "GDAL 1.1.7, released 2002/04/16"
            } catch {
               throw new Exception(string.Format("Not found {0}-bit gdal (see http://www.gdal.org).", Environment.Is64BitProcess ? 64 : 32));
               //throw new Exception(string.Format("Not found {0}-bit gdal (see http://www.gdal.org)." + Environment.NewLine + "If gdal exist, try option --gdal=path.", Environment.Is64BitProcess ? 64 : 32));
            }

            if (opt.HgtPath.Count > 0) { // convert hgt' to tiff

               List<HGTSource> hgtsrc = new List<HGTSource>();
               foreach (string item in opt.HgtPath) {
                  if (Directory.Exists(item)) { // alle Dateien im Verzeichnis
                     foreach (string item2 in Directory.GetFiles(item)) {
                        int lat, lon;
                        Hgt.HGTReaderWriter.GetLonLatFromHgtFilename(item2, out lon, out lat);
                        if (-180 <= lon && lon <= 180 &&
                            -90 <= lat && lat <= 90) {
                           hgtsrc.Add(new HGTSource(item, lon, lat));
                        }
                     }
                  } else {
                     if (File.Exists(item)) {
                        int lat, lon;
                        Hgt.HGTReaderWriter.GetLonLatFromHgtFilename(item, out lon, out lat);
                        if (-180 <= lon && lon <= 180 &&
                            -90 <= lat && lat <= 90) {
                           hgtsrc.Add(new HGTSource(Path.GetDirectoryName(item), lon, lat));
                        }
                     } else
                        throw new Exception("file '" + item + "' don't exist");
                  }
               }

               List<VRTSource> vrtsrc = opt.WithVRT ? new List<VRTSource>() : null;

               foreach (var item in hgtsrc)
                  HGT2Tiff(opt.Info, item.lon, item.lat, item.path, opt.DestinationPath, opt.OutputOverwrite, opt.Compress, opt.NoDataValue, vrtsrc);

               if (vrtsrc != null) {
                  // Extremwerte je Auflösung einsammeln
                  List<int> resolution = new List<int>();
                  Dictionary<int, int> minleft = new Dictionary<int, int>();
                  Dictionary<int, int> maxleft = new Dictionary<int, int>();
                  Dictionary<int, int> mintop = new Dictionary<int, int>();
                  Dictionary<int, int> maxtop = new Dictionary<int, int>();
                  foreach (var item in vrtsrc) {
                     int actresolution = item.width;
                     if (!resolution.Contains(actresolution)) {
                        resolution.Add(actresolution);
                        minleft.Add(actresolution, int.MaxValue);
                        maxleft.Add(actresolution, int.MinValue);
                        mintop.Add(actresolution, int.MaxValue);
                        maxtop.Add(actresolution, int.MinValue);
                     }

                     minleft[actresolution] = Math.Min(minleft[actresolution], item.left);
                     maxleft[actresolution] = Math.Max(maxleft[actresolution], item.left);
                     mintop[actresolution] = Math.Min(mintop[actresolution], item.top);
                     maxtop[actresolution] = Math.Max(maxtop[actresolution], item.top);
                  }

                  for (int i = 0; i < resolution.Count; i++) { // für jede Auflösung ein eigenes VRT bilden
                     int actresolution = resolution[i];
                     int vrtwidth = (maxleft[actresolution] - minleft[actresolution] + 1) * (actresolution - 1) + 1;    // HGT's überlappen sich immer um 1 Pixel!
                     int vrtheight = (maxtop[actresolution] - mintop[actresolution] + 1) * (actresolution - 1) + 1;
                     List<VRTSource> vrtsrctmp = new List<VRTSource>();
                     for (int j = 0; j < vrtsrc.Count; j++) {
                        if (vrtsrc[j].width == actresolution) {
                           vrtsrc[j].left = (vrtsrc[j].left - minleft[actresolution]) * (vrtsrc[j].width - 1);
                           vrtsrc[j].top = (maxtop[actresolution] - vrtsrc[j].top) * (vrtsrc[j].height - 1);
                           vrtsrc[j].lat += 1;
                           vrtsrctmp.Add(vrtsrc[j]);
                        }
                     }

                     CreateSpecialVrt(Path.Combine(opt.DestinationPath, actresolution.ToString() + ".vrt"),
                                      opt.OutputOverwrite,
                                      vrtwidth,
                                      vrtheight,
                                      actresolution,
                                      opt.NoDataValue == int.MinValue ? (short)Hgt.HGTReaderWriter.NoValue : (short)opt.NoDataValue,
                                      vrtsrctmp);
                  }
               }

            } else { // convert tiff to hgt

               List<string> files = new List<string>();
               if (Directory.Exists(opt.RasterFilename)) {
                  files.AddRange(Directory.GetFiles(opt.RasterFilename, "*.tif"));
               } else
                  files.Add(opt.RasterFilename);

               foreach (string tiffile in files)
                  Tiff2HGT(opt.Info, tiffile, opt.DestinationPath, false, opt.OutputOverwrite, opt.Compress, opt.NoDataValue);

            }

         } catch (Exception ex) {
            Console.Error.WriteLine("Error: " + ex.Message);
         }

      }

      /// <summary>
      /// create a tiff (and vrt) for a hgt
      /// </summary>
      /// <param name="onlyinfo">show only info for HGT</param>
      /// <param name="lon">longitude of the s-w-corner of the hgt</param>
      /// <param name="lat">latitude of the s-w-corner of the hgt</param>
      /// <param name="hgtpath">path, where found the hgt (with standardname)</param>
      /// <param name="destpath">destination path for the tiff</param>
      /// <param name="overwrite">if true overwrite the tiff if exist</param>
      /// <param name="nodata">value for nodata (if short.MinValue ... short.MaxValue)</param>
      /// <param name="compress">if true create a tiff with compression</param>
      /// <param name="vrtsrc">list of HGT data for vrt</param>
      static void HGT2Tiff(bool onlyinfo, int lon, int lat, string hgtpath, string destpath, bool overwrite, bool compress, int nodata, List<VRTSource> vrtsrc) {
         Hgt.HGTReaderWriter hgtreader = new Hgt.HGTReaderWriter(lon, lat);
         hgtreader.Read(hgtpath);
         Console.WriteLine(hgtreader);

         if (onlyinfo)
            return;

         string drivername = "GTiff";
         Driver drv = Gdal.GetDriverByName(drivername);
         if (drv == null)
            throw new Exception("Can't get driver " + drivername);

         else {
            Console.WriteLine("Using driver " + drv.LongName);

            string filename = Path.Combine(destpath, string.Format("hgt{0}{1:D2}{2}{3:D3}.tif",
                                                                     lat >= 0 ? "N" : "S",
                                                                     lat,
                                                                     lon >= 0 ? "E" : "W",
                                                                     lon));
            if (!overwrite && File.Exists(filename))
               throw new Exception(string.Format("File '{0}' exists and may not overwrite.", filename));

            Dataset dsnew = drv.Create(filename, hgtreader.Columns, hgtreader.Rows, 1, DataType.GDT_Int16, compress ? new string[] { // für beste Komprimierung
                                                                                                "COMPRESS=LZW",
                                                                                                "TILED=YES",
                                                                                                "PREDICTOR=2", // PREDICTOR=[1/2/3]: Set the predictor for LZW or DEFLATE compression.The default is 1(no predictor), 2 is horizontal differencing and 3 is floating point prediction.
                                                                                                //"COMPRESS=DEFLATE",
                                                                                                //"ZLEVEL=9"  // ZLEVEL=[1-9]: Set the level of compression when using DEFLATE compression. A value of 9 is best, and 1 is least compression. The default is 6.
                                                                                          } : null);
            if (dsnew == null)
               throw new Exception("Can't create '" + filename + "'.");

            else {
               Console.WriteLine("Create " + filename);

               dsnew.GetRasterBand(1).SetNoDataValue(nodata == int.MinValue ?                // GDAL propritär; QGIS versteht es aber
                                                                  Hgt.HGTReaderWriter.NoValue :
                                                                  (short)nodata);
               dsnew.WriteRaster(0,                      // xOff
                                 0,                      // yOff
                                 hgtreader.Columns,      // xSize
                                 hgtreader.Rows,         // ySize
                                 hgtreader.Data,         // buffer
                                 hgtreader.Columns,      // buf_xSize
                                 hgtreader.Rows,         // buf_ySize
                                 1,                      // bandCount
                                 new int[] { 1 },        // bandMap
                                 0,                      // pixelSpace
                                 0,                      // lineSpace
                                 0);                     // bandSpace
               dsnew.FlushCache();

               if (dsnew != null) {
                  string wktProj = null;
                  OSGeo.OSR.Osr.GetWellKnownGeogCSAsWKT("WGS84", out wktProj);
                  dsnew.SetProjection(wktProj);

                  /*    Die Koordinatenangaben im TIFF beziehen sich auf den Mittelpunkt des Punktes.
                   *    
                   *    Die Geo-Daten eines TIFF beziehen sich aber auf den "äußeren Rahmen"! Sie liegen also 1/2 Pixelgröße weiter "außen" als die Koordinaten der Randpixel.
                   *    Die Bildbreite und -höhe ist also insgesamt um die Breite eines Pixels größer als 1°. Die Größe eines Pixels im TIFF für ein 1201er HGT ist:
                   *       1201 * w = 1° + w
                   *       1200 * w = 1°
                   *              w = 1° / 1200
                   *    Der westliche Rand ist:
                   *       west = hgtleft - w / 2
                   *    Der nördliche Rand ist:
                   *       north = hgtsouth + 1 + w / 2
                   */
                  // Transformationsmatrix für Streckung/Stauchung, Rotation (alpha) und Verzerrung
                  double pixw = 1.0 / (hgtreader.Columns - 1);
                  dsnew.SetGeoTransform(new double[] {
                                             hgtreader.Left - pixw / 2,          // westlicher Rand
                                             pixw,                               // Grad je Pixel waagerecht; allg.: cos(alpha)*scaling
                                             0,                                  // allg.: -sin(alpha)*scaling
                                             hgtreader.Bottom + 1.0 + pixw / 2,  // nördlicher Rand
                                             0,                                  // allg.: sin(alpha)*scaling
                                             -pixw,                              // Grad je Pixel senkrecht; allg.: cos(alpha)*scaling
                                        });

                  dsnew.Dispose();

                  if (vrtsrc != null)
                     vrtsrc.Add(new VRTSource(filename, lon, lat, hgtreader.Rows, hgtreader.Columns, lon, lat));
               }
            }
         }
      }

      /// <summary>
      /// create hgt's for the tiff or vrt
      /// </summary>
      /// <param name="onlyinfo">show only info for TIFF</param>
      /// <param name="tiff">name of tiff or vrt</param>
      /// <param name="destpath">destination path for the hgt's</param>
      /// <param name="morethenedge">if true, a hgt have not only on the edge valid values</param>
      /// <param name="overwrite">if true overwrite a hgt if exist</param>
      /// <param name="compress">if true create a zipped hgt</param>
      /// <param name="vrtfilename">create a vrt if not empty</param>
      /// <param name="nodata">value for nodata (if short.MinValue ... short.MaxValue)</param>
      static void Tiff2HGT(bool onlyinfo, string tiff, string destpath, bool morethenedge, bool overwrite, bool compress, int nodata = int.MinValue) {
         Dataset ds = Gdal.Open(tiff, Access.GA_ReadOnly);

         if (ds == null)
            throw new Exception("Can't open '" + tiff + "'.");

         Console.WriteLine("Driver " + ds.GetDriver().LongName);
         Console.WriteLine("Projection: " + ds.GetProjectionRef());
         double[] geo = new double[6];
         ds.GetGeoTransform(geo);
         double bm_leftgeo = geo[0];
         double bm_topgeo = geo[3];
         double pixel_width = geo[1];
         double pixel_height = -geo[5];

         /*    Die Koordinatenangaben im TIFF beziehen sich auf den Mittelpunkt des Punktes.
          *    
          *    Die Geo-Daten eines TIFF beziehen sich aber auf den "äußeren Rahmen"! Sie liegen also 1/2 Pixelgröße weiter "außen" als die Koordinaten der Randpixel.
          *    Die Bildbreite und -höhe ist also insgesamt um die Breite eines Pixels größer als 1°.
          */
         // geogr. Angaben auf Pixelmittelpunkt beziehen
         bm_leftgeo += pixel_width / 2;
         bm_topgeo -= pixel_height / 2;

         double bm_widthgeo = pixel_width * (ds.RasterXSize - 1);
         double bm_heightgeo = pixel_height * (ds.RasterYSize - 1);

         Console.WriteLine("Points for lon {0}° .. {1}° / lat {2}° .. {3}°", bm_leftgeo, bm_leftgeo + bm_widthgeo, bm_topgeo, bm_topgeo - bm_heightgeo);
         Console.WriteLine("Size " + ds.RasterXSize + " x " + ds.RasterYSize);
         //Console.WriteLine("  RasterCount: " + ds.RasterCount);

         if (ds.RasterCount == 1) {
            Driver drv = Gdal.GetDriverByName(ds.GetDriver().ShortName);
            if (drv == null)
               throw new Exception("Can't get driver " + ds.GetDriver().LongName);

            else {
               Console.WriteLine("Using driver " + drv.LongName);

               Band band = ds.GetRasterBand(1);
               Console.WriteLine("DataType " + band.DataType);
               //Console.WriteLine("   Size (" + band.XSize + "," + band.YSize + ")");
               Console.WriteLine("Color " + band.GetRasterColorInterpretation().ToString());

               if (band.DataType != DataType.GDT_Int16 ||
                   band.GetRasterColorInterpretation() != ColorInterp.GCI_GrayIndex ||
                   pixel_width / pixel_height > 1.0001 ||     // geringe Abweichung zulassen
                   pixel_height / pixel_width > 1.0001) {

                  // nach clipping:
                  // hgtN45E010.tif    Pixel Size = (0.000277779237806,-0.000277778417413)      Faktor 1,000002953
                  //                   -> 99,99947% bzw. 99,99977%
                  // 0.000833319286934, -0.000833354438482

                  throw new Exception("DataType not Int16 or Color not GrayInde or pixel width not height");

               } else {

                  double pixelsize = (pixel_width + pixel_height) / 2;

                  short sNoDataValue = Hgt.HGTReaderWriter.NoValue; // use standard
                  double dNovalue;
                  int iNoDataValue;
                  band.GetNoDataValue(out dNovalue, out iNoDataValue);
                  if (iNoDataValue != 0) { // im Bild vorgegeben, deshalb Standard überschreiben
                     sNoDataValue = (short)dNovalue;
                     Console.WriteLine("internal nodatavalue: " + sNoDataValue);
                  }
                  if (nodata != int.MinValue) { // expl. "von außen" vorgegeben, deshalb Vorrang
                     sNoDataValue = (short)nodata;
                     Console.WriteLine("use nodatavalue: " + sNoDataValue);
                     iNoDataValue = 1;
                  }

                  // Größe eines HGT in Pixel
                  RectangleXY rcBitmap = new RectangleXY(bm_leftgeo, bm_topgeo, bm_widthgeo, bm_heightgeo, (int)Math.Round(1.0 / pixelsize) + 1);
                  Console.WriteLine("HGT-Size {0}", rcBitmap.Resolution);

                  RectangleXY rcAllHGT = rcBitmap.GetWholeNumberedBounding();
                  if (!onlyinfo)
                     Console.Write("create ");
                  Console.WriteLine("HGT's for lon={0}° .. {1}° / lat={2}° .. {3}°",
                                    rcAllHGT.LonWest,
                                    rcAllHGT.LonEast,
                                    rcAllHGT.LatNorth,
                                    rcAllHGT.LatSouth);

                  // Test
                  //CreateHGTData(false, rcAllHGT, rcBitmap, rcBitmap.Lon2X(11.0), rcBitmap.Lat2Y(35.0 + 1), rcBitmap.Resolution, band, sNoDataValue, destpath, morethenedge, overwrite, compress);

                  for (int north = rcAllHGT.North; north != rcAllHGT.South; north += rcAllHGT.Resolution - 1)
                     for (int west = rcAllHGT.West; west != rcAllHGT.East; west += rcAllHGT.Resolution - 1)
                        CreateHGTData(onlyinfo, rcAllHGT, rcBitmap, west, north, rcBitmap.Resolution, band, sNoDataValue, destpath, morethenedge, overwrite, compress);
               }

            }
         }
      }

      /// <summary>
      /// compute and create a HGT (or not, if not necessary)
      /// </summary>
      /// <param name="onlyinfo">show only info for TIFF</param>
      /// <param name="rcAllHGTArea">location and size of all HGT's</param>
      /// <param name="rcBitmapArea">location and size of bitmap</param>
      /// <param name="west">westerly edge of s</param>
      /// <param name="north">northerly edge of HGT</param>
      /// <param name="hgttilesize">HGT size</param>
      /// <param name="band"><see cref="Band"/> des Bitmaps</param>
      /// <param name="sNoDataValue">nodata value</param>
      /// <param name="destpath">destination path for the hgt's</param>
      /// <param name="morethenedge">if true, a hgt have not only on the edge valid values</param>
      /// <param name="overwrite">if true overwrite a hgt if exist</param>
      /// <param name="compress">if true create a zipped hgt</param>
      static void CreateHGTData(bool onlyinfo,
                                RectangleXY rcAllHGTArea,
                                RectangleXY rcBitmapArea,
                                int west,
                                int north,
                                int hgttilesize,
                                Band band,
                                short sNoDataValue,
                                string destpath,
                                bool morethenedge,
                                bool overwrite,
                                bool compress) {
         /*
          * org:
               hgtN46E011.tif
               hgtN46E012.tif
               hgtN46E013.tif
               hgtN47E011.tif
               hgtN47E012.tif

               x x -
               x x x

               -> Bereich 11..14 / 46..48
                          11*3600 .. 14*3600 / 47*3600 ..48*3600
                          39600<= .. <=50400 / 165600<= .. <=172800 Pixelindex (global)
                              10801 breit    /      7201 hoch

            <VRTDataset rasterXSize="10801" rasterYSize="7201">
             <GeoTransform>10.9998611111111,0.000277777777777778,0.0,48.0001388888889,0.0,-0.000277777777777778</GeoTransform>
          0,000277777777777778   Pixelsize
         10,999861111111111111   14,000138888888888889   3,000277777777777778    10801
         45,999861111111111111   48,000138888888888889   2,000277777777777778     7201

          */

         short[] pictdata4hgt = new short[hgttilesize * hgttilesize];
         for (int i = 0; i < pictdata4hgt.Length; i++) // init
            pictdata4hgt[i] = sNoDataValue;

         RectangleXY rcHGT = new RectangleXY(west, north, rcAllHGTArea.Resolution - 1, rcAllHGTArea.Resolution - 1, rcAllHGTArea.Resolution);
         Console.Write("HGT for lon={0}° / lat={1}° ", rcHGT.LonWest, rcHGT.LatSouth);

         //RectangleXY rcOverlap = rcBitmap.Intersection(rcHGT);
         RectangleXY rcOverlap;
         try {

            rcOverlap = rcBitmapArea.Intersection(rcHGT);
            if (rcOverlap != null) {            // wenn keine Überschneidung, dann kein HGT nötig
               if (rcOverlap == rcHGT) {        // Daten für gesamtes HGT im Bitmap-Bereich
                  Console.WriteLine("is complete in bitmap.");

                  /*    public OSGeo.GDAL.CPLErr ReadRaster(int xOff, 
                                                            int yOff, 
                                                            int xSize, 
                                                            int ySize, 
                                                            short[] buffer, 
                                                            int buf_xSize,             Struktur des Zielpuffers
                                                            int buf_ySize, 
                                                            int pixelSpace,            steht wohl für die Byte-Anzahl je Pixel
                                                            int lineSpace)
                   */
                  band.ReadRaster(rcHGT.West - rcBitmapArea.West,
                                  rcHGT.North - rcBitmapArea.North,
                                  hgttilesize,
                                  hgttilesize,
                                  pictdata4hgt,
                                  hgttilesize,
                                  hgttilesize,
                                  2,
                                  0);


               } else {
                  Console.WriteLine(" is partial in bitmap ({0} x {1}).", rcOverlap.Width + 1, rcOverlap.Height + 1);

                  // Daten holen
                  //short[] overlap = new short[rcOverlap.Width * rcOverlap.Height];

                  //band.ReadRaster(rcOverlap.West - rcBitmapArea.West,
                  //                rcOverlap.North - rcBitmapArea.North,
                  //                rcOverlap.Width,
                  //                rcOverlap.Height,
                  //                overlap,
                  //                rcOverlap.Width,
                  //                rcOverlap.Height,
                  //                2,
                  //                0);

                  //// Daten übertragen
                  //int src = 0;
                  //int dst = (rcOverlap.Resolution - rcOverlap.Height) * rcOverlap.Resolution + (rcOverlap.Resolution - rcOverlap.Width);
                  //for (int line = rcOverlap.Height; line > 0; line--, src += rcOverlap.Width, dst += hgttilesize)
                  //   Array.Copy(overlap, src, pictdata4hgt, dst, rcOverlap.Width);


                  // Daten holen
                  short[] overlap = new short[(rcOverlap.Width + 1) * (rcOverlap.Height + 1)];

                  band.ReadRaster(rcOverlap.West - rcBitmapArea.West,      // Pos. im Bitmap
                                  rcOverlap.North - rcBitmapArea.North,    // Pos. im Bitmap
                                  rcOverlap.Width + 1,                     // Bereichsbreite im Bitmap
                                  rcOverlap.Height + 1,                    // Bereichshöhe im Bitmap
                                  overlap,                                 // Zielpuffer
                                  rcOverlap.Width + 1,                     // Breite und ...
                                  rcOverlap.Height + 1,                    //        ... Höhe des Zielpuffers
                                  2,                                       // Byteanzahl je Pixel
                                  0);                                      // stride

                  // Daten übertragen
                  CopyOverlayData2HgtBuffer(overlap,
                                            rcOverlap.Width + 1,
                                            rcOverlap.Height + 1,
                                            pictdata4hgt,
                                            rcOverlap.West - rcHGT.West,
                                            rcOverlap.North - rcHGT.North,
                                            hgttilesize);
               }

               WriteDataAsHGT(onlyinfo, pictdata4hgt, sNoDataValue, destpath, (int)rcHGT.LonWest, (int)rcHGT.LatSouth, morethenedge, overwrite, compress);

            } else
               Console.WriteLine(" not necessary");

         } catch (Exception ex) {
            Console.Error.WriteLine("unexpected error: " + ex.Message);
         }
      }

      /// <summary>
      /// kopiert Daten aus einem Puffer in einen anderen
      /// </summary>
      /// <param name="srcbuff">source buffer</param>
      /// <param name="srcdx">source width</param>
      /// <param name="srcdy">source height</param>
      /// <param name="hgtbuff">destination buffer</param>
      /// <param name="dstx">destination position</param>
      /// <param name="dsty">destination position</param>
      /// <param name="hgttilesize">destination size</param>
      static void CopyOverlayData2HgtBuffer(short[] srcbuff, int srcdx, int srcdy, short[] hgtbuff, int dstx, int dsty, int hgttilesize) {
         int srcptr = 0;
         int dstptr = dsty * hgttilesize + dstx;
         for (int line = 0; line < srcdy; line++, srcptr += srcdx, dstptr += hgttilesize)   // zeilenweise kopieren
            Array.Copy(srcbuff, srcptr, hgtbuff, dstptr, srcdx);
      }

      /// <summary>
      /// create a HGT (or not, if not necessary)
      /// </summary>
      /// <param name="onlyinfo">show only info for TIFF</param>
      /// <param name="pictdata4hgt">data array</param>
      /// <param name="sNoDataValue">nodata value</param>
      /// <param name="destpath">destination path for the hgt's</param>
      /// <param name="LonWest">longitude of HGT</param>
      /// <param name="LatSouth">latitude of HGT</param>
      /// <param name="morethenedge">if true, a hgt have not only on the edge valid values</param>
      /// <param name="overwrite">if true overwrite a hgt if exist</param>
      /// <param name="compress">if true create a zipped hgt</param>
      static void WriteDataAsHGT(bool onlyinfo,
                                 short[] pictdata4hgt,
                                 short sNoDataValue,
                                 string destpath,
                                 int LonWest,
                                 int LatSouth,
                                 bool morethenedge,
                                 bool overwrite,
                                 bool compress) {
         if (pictdata4hgt != null) {
            if (sNoDataValue != Hgt.HGTReaderWriter.NoValue) // convert nodata value
               for (int i = 0; i < pictdata4hgt.Length; i++)
                  if (pictdata4hgt[i] == sNoDataValue)
                     pictdata4hgt[i] = Hgt.HGTReaderWriter.NoValue;

            try {

               Hgt.HGTReaderWriter hgtwriter = new Hgt.HGTReaderWriter(LonWest, LatSouth, pictdata4hgt);

               if (onlyinfo) {
                  Console.WriteLine(hgtwriter);
                  return;
               }

               bool InnerIsAnyWhereValid = true;
               if (hgtwriter.NotValid < hgtwriter.Rows * hgtwriter.Columns) {
                  if (hgtwriter.NotValid > 0) {
                     // testen, ob nur der Rand gültige Werte enthält
                     InnerIsAnyWhereValid = false;
                     for (int r = 1; r < hgtwriter.Rows - 1; r++)
                        for (int c = 1; c < hgtwriter.Columns - 1; c++)
                           if (hgtwriter.Get(r, c) != Hgt.HGTReaderWriter.NoValue) {
                              InnerIsAnyWhereValid = true;
                              break;
                           }
                  }
               } else
                  InnerIsAnyWhereValid = false;

               if (InnerIsAnyWhereValid) {
                  Console.Write("   save ...");
                  string filename = hgtwriter.Write(destpath, compress, overwrite);
                  Console.WriteLine(" file '" + filename + "' created");
                  Console.WriteLine("   {0}m..{1}m, unvalid Values: {2} ({3:F1}%)",
                                    hgtwriter.Minimum,
                                    hgtwriter.Maximum,
                                    hgtwriter.NotValid,
                                    (100.0 * hgtwriter.NotValid) / (hgtwriter.Rows * hgtwriter.Columns));
               } else
                  Console.WriteLine("   not created because only nodata values.");


            } catch (Exception ex) {
               Console.Error.WriteLine("unexpected error: " + ex.Message);
            }

         }
      }

      /// <summary>
      /// create a vrt for our special case with 1°x1° subfiles
      /// </summary>
      /// <param name="vrtfilename">name of vrt-file</param>
      /// <param name="overwrite">if true overwrite a vrt if exist</param>
      /// <param name="vrtwidth">width of vrt</param>
      /// <param name="vrtheight">height of vrt</param>
      /// <param name="resolution">resolution of the 1°x1° subfiles (regular 1201 or 3601)</param>
      /// <param name="nodatavalue">value for nodata (if short.MinValue ... short.MaxValue)</param>
      /// <param name="src">list of subfiles</param>
      static void CreateSpecialVrt(string vrtfilename, bool overwrite, int vrtwidth, int vrtheight, int resolution, short nodatavalue, List<VRTSource> src) {
         if (!overwrite &&
             File.Exists(vrtfilename))
            throw new Exception("File '" + vrtfilename + "' exist.");

         // http://www.gdal.org/drv_vrt.html

         Driver drv = Gdal.GetDriverByName("VRT");
         Dataset ds1 = Gdal.OpenShared(src[0].file, Access.GA_ReadOnly);
         Dataset dsvrt = drv.CreateCopy(vrtfilename, ds1, 0, null, null, null);
         dsvrt.Dispose();
         /* just we have a base like this:

         <VRTDataset rasterXSize="3601" rasterYSize="3601">
           <SRS>GEOGCS["WGS 84",DATUM["WGS_1984",SPHEROID["WGS 84",6378137,298.257223563,AUTHORITY["EPSG","7030"]],AUTHORITY["EPSG","6326"]],PRIMEM["Greenwich",0],UNIT["degree",0.0174532925199433],AUTHORITY["EPSG","4326"]]</SRS>
           <GeoTransform> 1.1000000000000000e+001, 2.7770063871146905e-004, 0.0000000000000000e+000, 4.8000000000000000e+001, 0.0000000000000000e+000,-2.7770063871146905e-004</GeoTransform>
           <Metadata>
             <MDI key="AREA_OR_POINT">Area</MDI>
           </Metadata>
           <VRTRasterBand dataType="Int16" band="1">
             <NoDataValue>-32768</NoDataValue>
             <ColorInterp>Gray</ColorInterp>

             <SimpleSource>
               <SourceFilename relativeToVRT="1">test\hgtN47E11.tif</SourceFilename>
               <SourceBand>1</SourceBand>
               <SourceProperties RasterXSize="3601" RasterYSize="3601" DataType="Int16" BlockXSize="256" BlockYSize="256" />
               <SrcRect xOff="0" yOff="0" xSize="3601" ySize="3601" />
               <DstRect xOff="0" yOff="0" xSize="3601" ySize="3601" />
             </SimpleSource>

           </VRTRasterBand>
         </VRTDataset> 
          */

         if (src.Count > 1) {
            FSoftUtils.SimpleXmlDocument2 vrt = new FSoftUtils.SimpleXmlDocument2(vrtfilename);
            vrt.Validating = false;
            vrt.LoadData();

            vrt.Remove("/VRTDataset/VRTRasterBand/SimpleSource"); // we want 'ComplexSource', not 'SimpleSource'
            string vrtpathu = Path.GetFullPath(vrtfilename);

            int minlon = int.MaxValue;
            int maxlat = int.MinValue;
            for (int i = 0; i < src.Count; i++)
               if (resolution == src[i].width) {
                  AddDatasource2Vrt(vrt, i + 1,
                                    GetRelFilename(vrtpathu, Path.GetFullPath(src[i].file)),
                                    nodatavalue,
                                    src[i].left,
                                    src[i].top,
                                    src[i].width,
                                    src[i].height,
                                    resolution);
                  minlon = Math.Min(minlon, src[i].lon);
                  maxlat = Math.Max(maxlat, src[i].lat);
               }

            vrt.Change("/VRTDataset/@rasterXSize", vrtwidth.ToString());
            vrt.Change("/VRTDataset/@rasterYSize", vrtheight.ToString());

            string geotransform = vrt.ReadValue("/VRTDataset/GeoTransform", "");
            string[] geotransformpart = geotransform.Split(new char[] { ',' });
            double bm_leftgeo = Convert.ToDouble(geotransformpart[0]);
            double pixel_width = Convert.ToDouble(geotransformpart[1]);
            double bm_topgeo = Convert.ToDouble(geotransformpart[3]);
            double pixel_height = Convert.ToDouble(geotransformpart[5]);

            pixel_width = 1.0 / (resolution - 1);
            pixel_height = -pixel_width;
            bm_leftgeo = minlon - pixel_width / 2;
            bm_topgeo = maxlat + pixel_width / 2;

            geotransform = bm_leftgeo.ToString(CultureInfo.InvariantCulture) + "," +
                           pixel_width.ToString(CultureInfo.InvariantCulture) + "," +
                           "0.0," +
                           bm_topgeo.ToString(CultureInfo.InvariantCulture) + "," +
                           "0.0," +
                           pixel_height.ToString(CultureInfo.InvariantCulture);
            vrt.Change("/VRTDataset/GeoTransform", geotransform);

            vrt.SaveData();

            Console.WriteLine("VRT '" + vrtfilename + "' created");
         }
      }

      /// <summary>
      /// add a datasource to a existing vrt
      /// </summary>
      /// <param name="vrt"></param>
      /// <param name="no">number of the source, 1..</param>
      /// <param name="srcfilename">the (relative) filename of the source</param>
      /// <param name="nodata">value for nodata (if short.MinValue ... short.MaxValue)</param>
      /// <param name="left">position of the left-top-corner</param>
      /// <param name="top">position of the left-top-corner</param>
      /// <param name="width">width of this source</param>
      /// <param name="height">height of this source</param>
      /// <param name="resolution">resolution of the 1°x1° subfiles (regular 1201 or 3601)</param>
      static void AddDatasource2Vrt(FSoftUtils.SimpleXmlDocument2 vrt, int no, string srcfilename, short nodata, int left, int top, int width, int height, int resolution) {
         /* this is an example for 1 source:

             <ComplexSource>
               <SourceFilename relativeToVRT="1">test\hgtN47E13.tif</SourceFilename>
               <SourceBand>1</SourceBand>
               <SourceProperties RasterXSize="3601" RasterYSize="3601" DataType="Int16" BlockXSize="256" BlockYSize="256" />
               <SrcRect xOff="0" yOff="0" xSize="3601" ySize="3601" />
               <DstRect xOff="7202" yOff="0" xSize="3601" ySize="3601" />
               <NODATA>-32768</NODATA>
             </ComplexSource> 
          */
         Dictionary<string, string> attr = new Dictionary<string, string>();
         string Source = "/VRTDataset/VRTRasterBand";
         vrt.Append(Source, "ComplexSource", null, null, false);
         Source += "/ComplexSource[" + no.ToString() + "]";

         attr.Clear();
         attr.Add("relativeToVRT", "1");
         vrt.Append(Source, "SourceFilename", srcfilename, attr, false);

         vrt.Append(Source, "SourceBand", "1", null, false);

         attr.Clear();
         attr.Add("RasterXSize", width.ToString());
         attr.Add("RasterYSize", height.ToString());
         attr.Add("DataType", "Int16");
         attr.Add("BlockXSize", "256");
         attr.Add("BlockYSize", "256");
         vrt.Append(Source, "SourceProperties", null, attr, false);

         attr.Clear();
         attr.Add("xOff", "0");
         attr.Add("yOff", "0");
         attr.Add("xSize", width.ToString());
         attr.Add("ySize", height.ToString());
         vrt.Append(Source, "SrcRect", null, attr, false);

         attr.Clear();
         attr.Add("xOff", left.ToString());
         attr.Add("yOff", top.ToString());
         attr.Add("xSize", Math.Max(resolution, width).ToString());
         attr.Add("ySize", Math.Max(resolution, height).ToString());
         vrt.Append(Source, "DstRect", null, attr, false);

         if (short.MinValue <= nodata && nodata <= short.MaxValue)
            vrt.Append(Source, "NODATA", nodata.ToString(), null, false);
      }

      /// <summary>
      /// get, if possible, a relativ filename
      /// </summary>
      /// <param name="referencefile"></param>
      /// <param name="file"></param>
      /// <returns></returns>
      static string GetRelFilename(string referencefile, string file) {
         if (!Path.IsPathRooted(referencefile))
            referencefile = Path.GetFullPath(referencefile);
         if (!Path.IsPathRooted(file))
            file = Path.GetFullPath(file);

         string[] refdirs = referencefile.Split(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
         string[] filedirs = file.Split(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
         int equallevel = -1;
         for (int i = 0; i < refdirs.Length - 1 && i < filedirs.Length - 1; i++) {
            if (refdirs[i].ToUpper() != filedirs[i].ToUpper()) {     // <-- upper nur Win/Dos !!!
               equallevel = i - 1;
               break;
            }
            equallevel = i;
         }

         string relfilename = ".";
         if (equallevel >= 0) {
            for (int i = equallevel + 1; i < refdirs.Length - 1; i++)
               relfilename += Path.DirectorySeparatorChar + "..";

            for (int i = equallevel + 1; i < filedirs.Length; i++)
               relfilename += Path.DirectorySeparatorChar + filedirs[i];
         }

         return relfilename;
      }

   }
}

