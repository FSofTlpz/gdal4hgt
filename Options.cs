using System;
using System.Collections.Generic;

namespace Gdal4Hgt {

   /// <summary>
   /// Optionen und Argumente werden zweckmäßigerweise in eine (programmabhängige) Klasse gekapselt.
   /// Erzeugen des Objektes und Evaluate() sollten in einem try-catch-Block erfolgen.
   /// </summary>
   public class Options {

      // alle Optionen sind i.A. 'read-only'

      /// <summary>
      /// Pfad zu den zu konvertierenden HGT-Dateien bzw. Name einer HGT-Datei
      /// </summary>
      public List<string> HgtPath { get; private set; }

      /// <summary>
      /// Name der zu konvertierenden TIF-Datei
      /// </summary>
      public string RasterFilename { get; private set; }

      /// <summary>
      /// Pfad für die zu erzeugenden Dateien
      /// </summary>
      public string DestinationPath { get; private set; }

      /// <summary>
      /// Wert für 'nodata'-Punkte
      /// </summary>
      public int NoDataValue { get; private set; }

      /// <summary>
      /// Ergebnis komprimieren
      /// </summary>
      public bool Compress { get; private set; }

      /// <summary>
      /// Info über Input liefern
      /// </summary>
      public bool Info { get; private set; }

      /// <summary>
      /// Ausgabeziel ev. überschreiben
      /// </summary>
      public bool OutputOverwrite { get; private set; }

      /// <summary>
      /// VRT-Datei(en) erzeugen
      /// </summary>
      public bool WithVRT { get; private set; }


      FSoftUtils.CmdlineOptions cmd;

      enum MyOptions {
         HgtPath,
         RasterFilename,
         DstPath,
         Compression,
         NoDataValue,
         VrtFilename,
         Info,
         OutputOverwrite,

         Help,
      }

      public Options() {
         Init();
         cmd = new FSoftUtils.CmdlineOptions();
         // Definition der Optionen
         cmd.DefineOption((int)MyOptions.HgtPath, "hgt", "", "hgt-filename or path to hgt files to convert (multiple usable)", FSoftUtils.CmdlineOptions.OptionArgumentType.String, int.MaxValue);
         cmd.DefineOption((int)MyOptions.RasterFilename, "raster", "r", "tiff- or vrt-filename to convert", FSoftUtils.CmdlineOptions.OptionArgumentType.String);
         cmd.DefineOption((int)MyOptions.DstPath, "dstpath", "d", "destination path", FSoftUtils.CmdlineOptions.OptionArgumentType.String);
         cmd.DefineOption((int)MyOptions.VrtFilename, "vrt", "v", "create additional vrt-file(s) (one for ever number of pixels in HGT's; , default 'true')", FSoftUtils.CmdlineOptions.OptionArgumentType.Boolean);
         cmd.DefineOption((int)MyOptions.Compression, "compr", "c", "compress the result (default 'true')", FSoftUtils.CmdlineOptions.OptionArgumentType.Boolean);
         cmd.DefineOption((int)MyOptions.OutputOverwrite, "overwrite", "O", "overwrite destination files (without arg 'true', default 'false')", FSoftUtils.CmdlineOptions.OptionArgumentType.BooleanOrNot);
         cmd.DefineOption((int)MyOptions.Info, "info", "", "only show infos for input files (without arg 'true', default 'false')", FSoftUtils.CmdlineOptions.OptionArgumentType.BooleanOrNot);
         cmd.DefineOption((int)MyOptions.NoDataValue, "nodata", "", "value for 'nodata' (default " + Hgt.HGTReaderWriter.NoValue.ToString() + ")", FSoftUtils.CmdlineOptions.OptionArgumentType.Integer);

         cmd.DefineOption((int)MyOptions.Help, "help", "?", "this help", FSoftUtils.CmdlineOptions.OptionArgumentType.Nothing);
      }

      /// <summary>
      /// Standardwerte setzen
      /// </summary>
      void Init() {
         HgtPath = new List<string>();
         RasterFilename = "";
         DestinationPath = ".";
         WithVRT = true;
         Compress = true;
         Info = false;
         OutputOverwrite = false;
         NoDataValue = int.MinValue;
      }

      /// <summary>
      /// Auswertung der Optionen
      /// </summary>
      /// <param name="args"></param>
      public void Evaluate(string[] args) {
         if (args == null) return;
         List<string> InputArray_Tmp = new List<string>();

         try {
            cmd.Parse(args);

            foreach (MyOptions opt in Enum.GetValues(typeof(MyOptions))) {    // jede denkbare Option testen
               int optcount = cmd.OptionAssignment((int)opt);                 // Wie oft wurde diese Option verwendet?
               if (optcount > 0)
                  switch (opt) {
                     case MyOptions.HgtPath:
                        for (int i = 0; i < optcount; i++) {
                           string tmp = cmd.StringValue((int)opt, i).Trim();
                           if (tmp.Length > 0)
                              HgtPath.Add(tmp);
                        }
                        break;

                     case MyOptions.RasterFilename:
                        RasterFilename = cmd.StringValue((int)opt).Trim();
                        break;

                     case MyOptions.DstPath:
                        DestinationPath = cmd.StringValue((int)opt).Trim();
                        break;

                     case MyOptions.VrtFilename:
                        WithVRT = cmd.BooleanValue((int)opt);
                        break;

                     case MyOptions.Compression:
                        Compress = cmd.BooleanValue((int)opt);
                        break;

                     case MyOptions.OutputOverwrite:
                        if (cmd.ArgIsUsed((int)opt))
                           OutputOverwrite = cmd.BooleanValue((int)opt);
                        else
                           OutputOverwrite = true;
                        break;

                     case MyOptions.Info:
                        if (cmd.ArgIsUsed((int)opt))
                           Info = cmd.BooleanValue((int)opt);
                        else
                           Info = true;
                        break;

                     case MyOptions.NoDataValue:
                        int iTmp = cmd.IntegerValue((int)opt);
                        if ((int)(iTmp & 0xFFFF0000) != 0) {
                           throw new Exception("valid values are " + short.MinValue.ToString() + " .. " + short.MaxValue.ToString());
                        }
                        NoDataValue = (short)(iTmp & 0xFFFF);

                        break;

                     case MyOptions.Help:
                        ShowHelp();
                        break;

                  }
            }

            //TestParameter = new string[cmd.Parameters.Count];
            //cmd.Parameters.CopyTo(TestParameter);

            if (HgtPath.Count == 0 && RasterFilename == "")
               throw new Exception("don't know, what to do (use at least '" + cmd.OptionName((int)MyOptions.HgtPath) + "' or '" + cmd.OptionName((int)MyOptions.RasterFilename) + "')");

            if (cmd.Parameters.Count > 0)
               throw new Exception("args not permitted");

         } catch (Exception ex) {
            Console.Error.WriteLine(ex.Message);
            ShowHelp();
            throw new Exception("Error on prog-options.");
         }
      }

      /// <summary>
      /// Hilfetext für Optionen ausgeben
      /// </summary>
      /// <param name="cmd"></param>
      public void ShowHelp() {
         List<string> help = cmd.GetHelpText();
         for (int i = 0; i < help.Count; i++) Console.Error.WriteLine(help[i]);
         Console.Error.WriteLine();
         Console.Error.WriteLine("Zusatzinfos:");


         Console.Error.WriteLine("Für '--' darf auch '/' stehen und für '=' auch ':' oder Leerzeichen.");
         Console.Error.WriteLine("Argumente mit ';' werden an diesen Stellen in Einzelargumente aufgetrennt.");

         // ...

      }


   }
}
