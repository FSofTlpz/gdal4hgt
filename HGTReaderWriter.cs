/*
Copyright (C) 2011 Frank Stinner

This program is free software; you can redistribute it and/or modify it 
under the terms of the GNU General Public License as published by the 
Free Software Foundation; either version 3 of the License, or (at your 
option) any later version. 

This program is distributed in the hope that it will be useful, but 
WITHOUT ANY WARRANTY; without even the implied warranty of 
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General 
Public License for more details. 

You should have received a copy of the GNU General Public License along 
with this program; if not, see <http://www.gnu.org/licenses/>. 


Dieses Programm ist freie Software. Sie können es unter den Bedingungen 
der GNU General Public License, wie von der Free Software Foundation 
veröffentlicht, weitergeben und/oder modifizieren, entweder gemäß 
Version 3 der Lizenz oder (nach Ihrer Option) jeder späteren Version. 

Die Veröffentlichung dieses Programms erfolgt in der Hoffnung, daß es 
Ihnen von Nutzen sein wird, aber OHNE IRGENDEINE GARANTIE, sogar ohne 
die implizite Garantie der MARKTREIFE oder der VERWENDBARKEIT FÜR EINEN 
BESTIMMTEN ZWECK. Details finden Sie in der GNU General Public License. 

Sie sollten ein Exemplar der GNU General Public License zusammen mit 
diesem Programm erhalten haben. Falls nicht, siehe 
<http://www.gnu.org/licenses/>. 
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;

namespace Hgt {

   /// <summary>
   /// zum lesen und schreiben der HGT-Daten:
   /// 
   /// Eine HGT-Datei enthält die Höhendaten für ein "Quadratgrad", also ein Gebiet über 1 Längen- und 1
   /// Breitengrad. Die Ausdehnung in N-S-Richtung ist also etwa 111km in O-W-Richtung je nach Breitengrad
   /// weniger.
   /// Die ursprünglichen SRTM-Daten (Shuttle Radar Topography Mission (SRTM) im Februar 2000) liegen im
   /// Bereich zwischen dem 60. nördlichen und 58. südlichen Breitengrad vor. Für die USA liegen diese Daten
   /// mit einer Auflösung von 1 Bogensekunde vor (--> 3601x3601 Datenpunkte, SRTM-1, etwa 30m), für den Rest 
   /// der Erde in 3 Bogensekunden (1201x1201, SRTM-3, etwa 92m). Die Randpunkte eines Gebietes sind also 
   /// identisch mit den Randpunkten des jeweils benachbarten Gebietes. (Der 1. Punkt einer Zeile oder Spalte
   /// liegt auf dem einen Rand des Gebietes, der letzte Punkt auf dem gegenüberliegenden Rand.)
   /// Der Dateiname leitet sich immer aus der S-W-Ecke (links-unten) des Gebietes ab, z.B. 
   ///      n51e002.hgt --> Gebiet zwischen N 51° E 2° und N 52° E 3°
   ///      s14w077.hgt --> Gebiet zwischen S 14° W 77° und S 13° W 76°
   /// Die Speicherung der Höhe erfolgt jeweils mit 2 Byte in Big-Endian-Bytefolge mit Vorzeichen.
   /// Die Werte sind in Metern angeben.
   /// Punkte ohne gültigen Wert haben den Wert 0x8000 (-32768).
   /// Die Reihenfolge der Daten ist zeilenweise von N nach S, innerhalb der Zeilen von W nach O.
   /// 
   /// z.B.
   /// http://dds.cr.usgs.gov/srtm/version2_1
   /// http://www.viewfinderpanoramas.org/dem3.html
   /// http://srtm.csi.cgiar.org/
   /// </summary>
   public class HGTReaderWriter {

      /// <summary>
      /// linker Rand in Grad
      /// </summary>
      public int Left { get; private set; }
      /// <summary>
      /// unterer Rand in Grad
      /// </summary>
      public int Bottom { get; private set; }
      /// <summary>
      /// Anzahl der Datenzeilen
      /// </summary>
      public int Rows { get; private set; }
      /// <summary>
      /// Anzahl der Datenspalten
      /// </summary>
      public int Columns { get; private set; }
      /// <summary>
      /// kleinster Wert
      /// </summary>
      public int Minimum { get; private set; }
      /// <summary>
      /// größter Wert
      /// </summary>
      public int Maximum { get; private set; }
      /// <summary>
      /// Anzahl der ungültigen Werte
      /// </summary>
      public long NotValid { get; private set; }

      /// <summary>
      /// Direktzugriff auf die Daten
      /// </summary>
      public short[] Data {
         get {
            return data;
         }
         set {
            if (value != null) {
               data = new short[value.Length];
               Array.Copy(value, data, value.Length);
               SetStats();
            }
         }
      }


      /// <summary>
      /// Kennung, wenn der Wert fehlt
      /// </summary>
      public const int NoValue = -32768;

      short[] data;


      /// <summary>
      /// 
      /// </summary>
      /// <param name="left">positiv für östliche Länge, sonst negativ</param>
      /// <param name="bottom">positiv für nördliche Breite, sonst negativ</param>
      public HGTReaderWriter(int left, int bottom) {
         Left = left;
         Bottom = bottom;
         Maximum = short.MinValue;
         Minimum = short.MaxValue;
         data = null;
         Rows = Columns = 0;
         NotValid = 0;
      }

      /// <summary>
      /// 
      /// </summary>
      /// <param name="left"></param>
      /// <param name="bottom"></param>
      /// <param name="dat"></param>
      public HGTReaderWriter(int left, int bottom, short[] dat) :
         this(left, bottom) {
         data = new short[dat.Length];
         Array.Copy(dat, data, dat.Length);
         Rows = Columns = (int)Math.Sqrt(data.Length);      // sollte im Normalfall immer quadratisch sein
         SetStats();
      }

      /// <summary>
      /// liest die Daten aus der entsprechenden HGT-Datei ein
      /// </summary>
      /// <param name="directory">Verzeichnis der Datendatei</param>
      /// <returns>Dateiname</returns>
      public string Read(string directory) {
         string filename = Path.Combine(directory, GetStdFilename(Left, Bottom));

         if (File.Exists(filename)) {

            Stream dat = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
            ReadFromStream(dat, (int)dat.Length);
            dat.Close();

         } else {

            filename += ".zip";
            if (!File.Exists(filename))
               throw new Exception(string.Format("Weder die Datei '{0}' noch die Datei '{0}.zip' existiert.", filename));

            using (FileStream zipstream = new FileStream(filename, FileMode.Open)) {
               using (ZipArchive zip = new ZipArchive(zipstream, ZipArchiveMode.Read)) {
                  filename = Path.GetFileNameWithoutExtension(filename).ToUpper();
                  ZipArchiveEntry entry = null;
                  foreach (var item in zip.Entries) {
                     if (filename == item.Name.ToUpper()) {
                        entry = item;
                        break;
                     }
                  }
                  if (entry == null)
                     throw new Exception(string.Format("Die Datei '{0}' ist nicht in der Datei '{0}.zip' enthalten.", filename));
                  Stream dat = entry.Open();
                  ReadFromStream(dat, (int)entry.Length);
                  dat.Close();
               }
            }

         }

         SetStats();
         return filename;
      }

      /// <summary>
      /// schreibt die Daten in die entsprechende HGT-Datei
      /// </summary>
      /// <param name="directory">Verzeichnis der Datendatei</param>
      /// <param name="zip">bei true wird eine ZIP-Datei erzeugt</param>
      /// <param name="overwrite">bei true wird eine schon ex. Datei überschrieben</param>
      /// <returns>Name der neuen Datei</returns>
      public string Write(string directory, bool zip, bool overwrite) {
         string stdfilename = GetStdFilename(Left, Bottom);
         string filename = Path.Combine(directory, stdfilename);
         if (zip)
            filename += ".zip";
         if (File.Exists(filename) && !overwrite)
            throw new Exception(string.Format("Die Datei '{0}' existiert schon und darf nicht überschrieben werden.", filename));

         if (zip) {

            using (FileStream fs = new FileStream(filename, FileMode.Create)) {
               using (ZipArchive zipa = new ZipArchive(fs, ZipArchiveMode.Create)) {
                  ZipArchiveEntry entry = zipa.CreateEntry(stdfilename, CompressionLevel.Optimal);
                  WriteToStream(entry.Open());
               }
            }

         } else {

            using (Stream stream = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None)) {
               WriteToStream(stream);
            }

         }
         return filename;
      }

      protected void WriteToStream(Stream str) {
         if (data != null)
            for (int i = 0; i < data.Length; i++) {
               str.WriteByte((byte)(data[i] >> 8));
               str.WriteByte((byte)(data[i] & 0xFF));
            }
      }

      protected void ReadFromStream(Stream str, int length) {
         data = new short[length / 2];       // 2 Byte je Datenpunkt
         for (int i = 0; i < data.Length; i++)
            data[i] = (short)((str.ReadByte() << 8) + str.ReadByte());
      }

      protected string GetStdFilename(int Left, int Bottom) {
         string name = "";
         if (Bottom >= 0)
            name += string.Format("N{0:d2}", Bottom);
         else
            name += string.Format("S{0:d2}", -Bottom);
         if (Left >= 0)
            name += string.Format("E{0:d3}", Left);
         else
            name += string.Format("W{0:d3}", -Left);
         return name + ".hgt";
      }

      protected void SetStats() {
         Maximum = short.MinValue;
         Minimum = short.MaxValue;
         NotValid = 0;
         for (int i = 0; i < data.Length; i++) {
            if (Maximum < data[i])
               Maximum = data[i];
            if (data[i] != NoValue) {
               if (Minimum > data[i])
                  Minimum = data[i];
            } else
               NotValid++;
         }

         Rows = Columns = (int)Math.Sqrt(data.Length);      // sollte im Normalfall immer quadratisch sein
      }

      /// <summary>
      /// liefert den Wert der Matrix
      /// </summary>
      /// <param name="row">Zeilennr. 0 .. unter <see cref="Rows"/> (0 ist die nördlichste)</param>
      /// <param name="col">Spaltennr. 0 .. unter <see cref="Columns"/> (0 ist die westlichste)</param>
      /// <returns></returns>
      public int Get(int row, int col) {
         if (data == null ||
             row < 0 || Rows <= row ||
             col < 0 || Columns <= col)
            return NoValue;
         return data[row * Columns + col];
      }

      /// <summary>
      /// liefert den Wert der Matrix ([0,0] ist die Ecke links unten)
      /// </summary>
      /// <param name="x">0 .. unter <see cref="Columns"/></param>
      /// <param name="y">0 .. unter <see cref="Rows"/></param>
      /// <returns></returns>
      public int Get4XY(int x, int y) {
         return Get(Rows - 1 - y, x);
      }

      /// <summary>
      /// alle Werte bis auf den definierten Bereich ungültig machen
      /// </summary>
      /// <param name="mincol"></param>
      /// <param name="minrow">obere Zeile</param>
      /// <param name="maxcol"></param>
      /// <param name="maxrow">untere Zeile</param>
      /// <returns>Anzahl der NICHT ungültig gemachten Werte</returns>
      public long DiscardExcept(int mincol, int minrow, int maxcol, int maxrow) {
         long discard = 0;
         for (int x = 0; x < Columns; x++)
            for (int y = 0; y < Rows; y++)
               if (!(mincol <= x && x <= maxcol &&
                     minrow <= y && y <= maxrow)) {
                  data[y * Columns + x] = NoValue;
                  discard++;
               }
         Maximum = Int16.MinValue;
         Minimum = Int16.MaxValue;
         NotValid = 0;
         for (int i = 0; i < data.Length; i++) {
            if (Maximum < data[i]) Maximum = data[i];
            if (data[i] != NoValue) {
               if (Minimum > data[i]) Minimum = data[i];
            } else
               NotValid++;
         }
         return Rows * Columns - discard;
      }

      /// <summary>
      /// 
      /// </summary>
      /// <param name="MinLongitude"></param>
      /// <param name="MinLatitude"></param>
      /// <param name="MaxLongitude"></param>
      /// <param name="MaxLatitude"></param>
      /// <returns>Anzahl der NICHT ungültig gemachten Werte</returns>
      public long DiscardExcept(double MinLongitude, double MinLatitude, double MaxLongitude, double MaxLatitude) {
         double lon1 = Math.Max(0.0, Math.Min(1.0, MinLongitude - Left));
         double lon2 = Math.Max(0.0, Math.Min(1.0, MaxLongitude - Left));
         double lat1 = Math.Max(0.0, Math.Min(1.0, MinLatitude - Bottom));
         double lat2 = Math.Max(0.0, Math.Min(1.0, MaxLatitude - Bottom));
         return DiscardExcept((int)(lon1 * Columns), (int)((1 - lat2) * Rows),
                              (int)(lon2 * Columns), (int)((1 - lat1) * Rows));
      }

      //#region Funktionen zur Bitmap-Erzeugung

      ///// <summary>
      ///// Farbe für eine bestimmte Höhe
      ///// </summary>
      //public class HeightColor {

      //   /// <summary>
      //   /// für die Höhe gültig
      //   /// </summary>
      //   public int Height { get; private set; }
      //   /// <summary>
      //   /// Farbe
      //   /// </summary>
      //   public Color Color { get; private set; }

      //   /// <summary>
      //   /// 
      //   /// </summary>
      //   /// <param name="height">-32768 ... 32768</param>
      //   /// <param name="col"></param>
      //   public HeightColor(int height, Color col) {
      //      Height = height;
      //      Color = col;
      //   }

      //}

      //public Bitmap GetBitmap(SortedDictionary<int, Color> heightColor, Color dummycol) {
      //   foreach (int height in heightColor.Keys) {
      //      if (height < -32768 || 32767 < height)
      //         throw new ArgumentException(string.Format("Höhe {0} ist nicht erlaubt.", height));
      //   }
      //   Color[] coltab = new Color[0x10000];
      //   for (int i = 0; i < coltab.Length; i++)
      //      coltab[i] = dummycol;

      //   Color colLast = Color.Black;
      //   int iLastHeight = int.MinValue;
      //   foreach (int height in heightColor.Keys) {
      //      Color colNew = heightColor[height];
      //      if (iLastHeight > int.MinValue)
      //         for (int i = iLastHeight; i < height; i++) {
      //            coltab[0x8000 + i] = GetBetweenColor(colLast, colNew, (i - iLastHeight) / (double)(height - iLastHeight));
      //         }
      //      iLastHeight = height;
      //      colLast = colNew;
      //   }
      //   return GetBitmap(coltab);
      //}

      //public Bitmap GetBitmap(Color[] coltab) {
      //   Bitmap bm = new Bitmap(Columns, Rows);
      //   if (coltab.Length != 0x10000)
      //      throw new Exception("Falsche Länge der Farbtabelle. Für jeden 16-Bit-Wert muss eine Farbe enthalten sein.");
      //   for (int r = 0; r < Rows; r++)
      //      for (int c = 0; c < Columns; c++) {
      //         bm.SetPixel(c, r, coltab[Get(r, c) + 0x8000]);
      //      }
      //   return bm;
      //}

      //Color GetBetweenColor(Color col1, Color col2, double f) {
      //   int rdiff = (int)Math.Round((col2.R - col1.R) * f);
      //   int gdiff = (int)Math.Round((col2.G - col1.G) * f);
      //   int bdiff = (int)Math.Round((col2.B - col1.B) * f);
      //   return Color.FromArgb(col1.R + rdiff, col1.G + gdiff, col1.B + bdiff);
      //}

      ///// <summary>
      ///// eine sehr einfache Umwandlung der Daten in ein Bild
      ///// </summary>
      ///// <returns></returns>
      //public Bitmap GetBitmap() {
      //   Color[] coltab = new Color[0x10000];
      //   for (int i = 0; i < coltab.Length; i++)
      //      coltab[i] = Color.Transparent;

      //   int hend = -21;
      //   int hstart;

      //   Color colstart = Color.FromArgb(0, 255, 255);
      //   Color colend = Color.FromArgb(0, 255, 0);
      //   hstart = hend;
      //   hend = 0;
      //   for (int h = hstart; h <= hend; h++) {
      //      double f = (double)(h - hstart) / (hend - hstart);       // 0.0 .. 1.0
      //      coltab[h + 0x8000] = GetBetweenColor(colstart, colend, f);
      //   }

      //   colstart = Color.FromArgb(0, 80, 0);
      //   colend = Color.FromArgb(0, 255, 0);
      //   hstart = hend;
      //   hend = 300;
      //   for (int h = hstart; h <= hend; h++) {
      //      double f = (double)(h - hstart) / (hend - hstart);
      //      coltab[h + 0x8000] = GetBetweenColor(colstart, colend, f);
      //   }

      //   colstart = Color.FromArgb(0, 255, 0);
      //   colend = Color.FromArgb(200, 255, 200);
      //   hstart = hend;
      //   hend = 600;
      //   for (int h = hstart; h <= hend; h++) {
      //      double f = (double)(h - hstart) / (hend - hstart);
      //      coltab[h + 0x8000] = GetBetweenColor(colstart, colend, f);
      //   }

      //   colstart = Color.FromArgb(200, 255, 200);
      //   colend = Color.FromArgb(255, 215, 0);
      //   hstart = hend;
      //   hend = 1000;
      //   for (int h = hstart; h <= hend; h++) {
      //      double f = (double)(h - hstart) / (hend - hstart);
      //      coltab[h + 0x8000] = GetBetweenColor(colstart, colend, f);
      //   }

      //   colstart = Color.FromArgb(255, 215, 0);
      //   colend = Color.FromArgb(170, 100, 0);
      //   hstart = hend;
      //   hend = 2500;
      //   for (int h = hstart; h <= hend; h++) {
      //      double f = (double)(h - hstart) / (hend - hstart);
      //      coltab[h + 0x8000] = GetBetweenColor(colstart, colend, f);
      //   }

      //   colstart = Color.FromArgb(170, 100, 0);
      //   colend = Color.FromArgb(60, 30, 0);
      //   hstart = hend;
      //   hend = 5000;
      //   for (int h = hstart; h <= hend; h++) {
      //      double f = (double)(h - hstart) / (hend - hstart);
      //      coltab[h + 0x8000] = GetBetweenColor(colstart, colend, f);
      //   }

      //   colstart = Color.FromArgb(60, 30, 0);
      //   colend = Color.FromArgb(200, 200, 200);
      //   hstart = hend;
      //   hend = 8900;
      //   for (int h = hstart; h <= hend; h++) {
      //      double f = (double)(h - hstart) / (hend - hstart);
      //      coltab[h + 0x8000] = GetBetweenColor(colstart, colend, f);
      //   }

      //   return GetBitmap(coltab);
      //}

      //#endregion

      /// <summary>
      /// Daten in eine Textdatei ausgeben
      /// </summary>
      /// <param name="filename"></param>
      public void WriteToFile(string filename) {
         using (StreamWriter sw = new StreamWriter(filename)) {
            for (int row = 0; row < Rows; row++) {
               for (int col = 0; col < Columns; col++) {
                  if (col > 0)
                     sw.Write("\t");
                  sw.Write(Get(row, col));
               }
               sw.WriteLine();
            }
         }
      }

      /// <summary>
      /// liefert aus dem Standardnamen Höhe und Breite
      /// </summary>
      /// <param name="hgtname"></param>
      /// <param name="lon"></param>
      /// <param name="lat"></param>
      public static void GetLonLatFromHgtFilename(string hgtname, out int lon, out int lat) {
         lon = lat = int.MinValue;
         string ext = Path.GetExtension(hgtname).ToUpper();
         hgtname = Path.GetFileNameWithoutExtension(hgtname);
         if ((ext == ".TXT" || ext == ".ZIP") &&
             (hgtname.Length == 11 || hgtname.Length == 15) &&
             (hgtname[0] == 'N' || hgtname[0] == 'S') &&
             char.IsDigit(hgtname[1]) &&
             char.IsDigit(hgtname[2]) &&
             (hgtname[3] == 'E' || hgtname[3] == 'W') &&
             char.IsDigit(hgtname[4]) &&
             char.IsDigit(hgtname[5]) &&
             char.IsDigit(hgtname[6])) {

            lat = Convert.ToInt32(hgtname.Substring(1, 2));
            if (hgtname[0] == 'S')
               lat = -lat;
            lon = Convert.ToInt32(hgtname.Substring(4, 3));
            if (hgtname[3] == 'W')
               lon = -lon;
         }
      }


      public override string ToString() {
         return string.Format("HGT: {0}{1}° {2}{3}°, {4}x{5}, {6}m..{7}m, unvalid Values: {8} ({9}%)",
            Bottom >= 0 ? "N" : "S", Bottom >= 0 ? Bottom : -Bottom,
            Left >= 0 ? "E" : "W", Left >= 0 ? Left : -Left,
            Rows, Columns,
            Minimum, Maximum,
            NotValid, (100.0 * NotValid) / (Rows * Columns));
      }

   }
}
