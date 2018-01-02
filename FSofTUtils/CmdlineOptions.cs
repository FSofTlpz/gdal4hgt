// FSofT, 5.7.2010, 5.3.2011, 3.11.2016

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
using System.Text.RegularExpressions;
using System.Text;
using System.Globalization;

namespace FSoftUtils {

   /*
    * Eine Option kann in kurzer (-x) und/oder langer (--lang bzw /lang) Form definiert werden.
    * Gültige Optionen müßen den reg. Ausdrücken rex_opt_short bzw. rex_opt_long entsprechen.
    * Ein Argument für eine Option wird durch Leerzeichen oder bei langen Optionen auch durch ein '=' oder ':' von der Option getrennt.
    * 
    * Folgt nach einer ...OrNot-Option ein durch Leerzeichen getrennter Text dessen Syntax am Anfang einer Option entspricht, wird er nicht als Argument
    * interpretiert. Soll ein solcher Text doch als Argument verwendet werden, muss die lange Optionsform verwendet werden und das Argument muss mit '=' oder ':'
    * an die Option gebunden werden.
    * 
    * Enthält ein Argument ein- oder mehrfach das Zeichen ';', wird es an diesen Stellen getrennt und die Option wird mehrfach, also für jedes Einzelargument, verwendet.
    * 
    */

   public class CmdlineOptions {

      /// <summary>
      /// Argumentarten
      /// </summary>
      public enum OptionArgumentType {
         /// <summary>
         /// Option ohne Argument
         /// </summary>
         Nothing = 0,
         /// <summary>
         /// Option mit Zeichenkette als Argument
         /// </summary>
         String,
         /// <summary>
         /// Option mit int-Zahl als Argument
         /// </summary>
         Integer,
         /// <summary>
         /// Option mit uint-Zahl als Argument
         /// </summary>
         UnsignedInteger,
         /// <summary>
         /// Option mit uint-Zahl größer als 0 als Argument
         /// </summary>
         PositivInteger,
         /// <summary>
         /// Option mit long-Zahl als Argument
         /// </summary>
         Long,
         /// <summary>
         /// Option mit ulong-Zahl als Argument
         /// </summary>
         UnsignedLong,
         /// <summary>
         /// Option mit ulong-Zahl größer als 0 als Argument
         /// </summary>
         PositivLong,
         /// <summary>
         /// Option mit double-Zahl als Argument
         /// </summary>
         Double,
         /// <summary>
         /// Option mit Argument, dass als boolean interpretiert werden kann
         /// </summary>
         Boolean,

         /// <summary>
         /// Option mit Zeichenkette als Argument oder ohne Argument
         /// </summary>
         StringOrNot,
         /// <summary>
         /// Option mit int-Zahl als Argument oder ohne Argument
         /// </summary>
         IntegerOrNot,
         /// <summary>
         /// Option mit uint-Zahl als Argument oder ohne Argument
         /// </summary>
         UnsignedIntegerOrNot,
         /// <summary>
         /// Option mit uint-Zahl größer als 0 als Argument oder ohne Argument
         /// </summary>
         PositivIntegerOrNot,
         /// <summary>
         /// Option mit long-Zahl als Argument oder ohne Argument
         /// </summary>
         LongOrNot,
         /// <summary>
         /// Option mit ulong-Zahl als Argument oder ohne Argument
         /// </summary>
         UnsignedLongOrNot,
         /// <summary>
         /// Option mit ulong-Zahl größer als 0 als Argument oder ohne Argument
         /// </summary>
         PositivLongOrNot,
         /// <summary>
         /// Option mit double-Zahl als Argument oder ohne Argument
         /// </summary>
         DoubleOrNot,
         /// <summary>
         /// Option mit Argument, dass als boolean interpretiert werden kann oder ohne Argument
         /// </summary>
         BooleanOrNot,
      }

      //Regex rex_opt_short = new Regex(@"^-([\w\?]{1})$");
      Regex rex_opt_short = new Regex(@"^-([A-Za-z0-9\?]+)$");
      Regex rex_opt_long = new Regex(@"^(--|/)([A-Za-z0-9]{1}[\w_]*)");     // \w <--> [A-Za-z0-9_] (vgl. http://en.wikipedia.org/wiki/Regular_expression)


      /// <summary>
      /// eine definierte Option
      /// </summary>
      protected class OptionDefinition {
         /// <summary>
         /// numerischer Schlüssel der Option
         /// </summary>
         public int iKey { get; private set; }
         /// <summary>
         /// Langform der Option
         /// </summary>
         public string sLongOption { get; private set; }
         /// <summary>
         /// Kurzform der Option
         /// </summary>
         public string sShortOption { get; private set; }
         /// <summary>
         /// Optionstyp
         /// </summary>
         public OptionArgumentType Type { get; private set; }
         /// <summary>
         /// max. Anzahl der Verwendungsmöglichkeit der Option (i.A. 1)
         /// </summary>
         public int iMaxCount { get; private set; }
         /// <summary>
         /// Hilfetext
         /// </summary>
         public string sHelpText { get; private set; }

         public OptionDefinition(int uniquekey, string sLongOption, string sShortOption, string sHelpText, OptionArgumentType argtype, int maxcount) {
            iKey = uniquekey;
            Type = argtype;
            this.sLongOption = "";
            this.sShortOption = "";
            this.sHelpText = "";
            this.iMaxCount = 1;
            if (sLongOption == null || sShortOption == null || (sLongOption.Trim().Length == 0 && sShortOption.Trim().Length == 0))
               throw new ArgumentException(string.Format("Die Option '{0}' ist unsinnig.", Name()));
            this.sLongOption = sLongOption;
            this.sShortOption = sShortOption;
            if (sShortOption.Length > 1)
               throw new ArgumentException(string.Format("Der kurze Text der Option in '{0}' ist zu lang.", Name()));
            this.sHelpText = sHelpText;
            this.iMaxCount = maxcount;
            if (iMaxCount <= 0)
               throw new ArgumentException(string.Format("Die Optionsanzahl der Option '{0}' ist unsinnig.", Name()));
         }

         /// <summary>
         /// Langform der Option (mit '--')
         /// </summary>
         /// <returns></returns>
         public string LongName() {
            return sLongOption.Length > 0 ? "--" + sLongOption : "";
         }

         /// <summary>
         /// Kurzform der Option (mit '-')
         /// </summary>
         /// <returns></returns>
         public string ShortName() {
            return sShortOption.Length > 0 ? "-" + sShortOption : "";
         }

         public string Name() {
            return LongName().Length > 0 && ShortName().Length > 0 ?
               string.Format("{0}, {1}", ShortName(), LongName()) :
               LongName().Length > 0 ? LongName() : ShortName();
         }

         public override string ToString() {
            return string.Format("{0} (Key={1}, Typ={2}, iMaxCount={3})", Name(), iKey, Type, iMaxCount);
         }
      }

      /// <summary>
      /// eine eingelesene Option
      /// </summary>
      protected class SampledOption {
         /// <summary>
         /// numerischer Schlüssel der Option
         /// </summary>
         public int iKey { get; private set; }
         /// <summary>
         /// Name der Option
         /// </summary>
         public string sOption { get; private set; }
         /// <summary>
         /// Option in Lang- oder Kurzform ('--' oder '-')
         /// </summary>
         public bool bShort { get; private set; }
         /// <summary>
         /// Optionstyp
         /// </summary>
         public OptionArgumentType Type { get; private set; }
         /// <summary>
         /// Argument entsprechend <see cref="Type"/> konvertiert
         /// </summary>
         public object oArgument { get; private set; }


         /// <summary>
         /// erzeugt eine Option mit ihren Daten
         /// <para>Kann das Argument nicht entsprechend des Typs konvertiert werden, oder darf kein Argument engegeben werden, wird eine Exception ausgelöst.</para>
         /// <para>Das erfolgreich konvertierte Argument wird in <see cref="oArgument"/> gespeichert.</para>
         /// </summary>
         /// <param name="key">numerischer Schlüssel der Option</param>
         /// <param name="sOption">Name der Option</param>
         /// <param name="bShort">Option in Lang- oder Kurzform ('--' oder '-')</param>
         /// <param name="Typ">Optionstyp</param>
         /// <param name="sArgument">Argument</param>
         public SampledOption(int key, string sOption, bool bShort, OptionArgumentType Typ, string sArgument = null) {
            iKey = key;
            this.sOption = sOption;
            this.bShort = bShort;
            this.Type = Typ;

            oArgument = null; // Standard: ohne Argument

            int iArg;
            double dArg;
            uint uArg;
            long lArg;
            ulong ulArg;
            bool bArg;

            switch (Typ) {
               case OptionArgumentType.Nothing:
                  if (sArgument != null)
                     throw new Exception(string.Format("Die Option '{0}' darf kein Argument haben.", Name()));
                  break;
               case OptionArgumentType.String:
               case OptionArgumentType.Integer:
               case OptionArgumentType.UnsignedInteger:
               case OptionArgumentType.PositivInteger:
               case OptionArgumentType.Long:
               case OptionArgumentType.UnsignedLong:
               case OptionArgumentType.PositivLong:
               case OptionArgumentType.Double:
               case OptionArgumentType.Boolean:
                  if (sArgument == null)
                     throw new Exception(string.Format("Die Option '{0}' muss ein Argument haben.", Name()));
                  break;
            }

            switch (Typ) {
               case OptionArgumentType.Nothing:
                  if (sArgument != null)
                     throw new Exception(string.Format("Die Option '{0}' darf kein Argument haben.", Name()));
                  break;

               case OptionArgumentType.String:
               case OptionArgumentType.StringOrNot:
                  if (Typ == OptionArgumentType.String && sArgument == null)
                     throw new Exception(string.Format("Die Option '{0}' muss Argument haben.", Name()));
                  oArgument = sArgument;
                  break;

               case OptionArgumentType.Integer:
               case OptionArgumentType.IntegerOrNot:
                  if (Typ == OptionArgumentType.Integer ||
                      (Typ == OptionArgumentType.IntegerOrNot && sArgument != null)) {
                     if (!IntegerIsPossible(sArgument, out iArg))
                        ThrowExceptionFalseType1();
                     oArgument = iArg;
                  }
                  break;

               case OptionArgumentType.UnsignedInteger:
               case OptionArgumentType.UnsignedIntegerOrNot:
                  if (Typ == OptionArgumentType.UnsignedInteger ||
                      (Typ == OptionArgumentType.UnsignedIntegerOrNot && sArgument != null)) {
                     if (!UnsignedIntegerIsPossible(sArgument, out uArg))
                        ThrowExceptionFalseType1();
                     oArgument = uArg;
                  }
                  break;

               case OptionArgumentType.PositivInteger:
               case OptionArgumentType.PositivIntegerOrNot:
                  if (Typ == OptionArgumentType.PositivInteger ||
                      (Typ == OptionArgumentType.PositivIntegerOrNot && sArgument != null)) {
                     if (!UnsignedIntegerIsPossible(sArgument, out uArg) || uArg == 0)
                        ThrowExceptionFalseType1();
                     oArgument = uArg;
                  }
                  break;

               case OptionArgumentType.Long:
               case OptionArgumentType.LongOrNot:
                  if (Typ == OptionArgumentType.Long ||
                      (Typ == OptionArgumentType.LongOrNot && sArgument != null)) {
                     if (!LongIsPossible(sArgument, out lArg))
                        ThrowExceptionFalseType1();
                     oArgument = lArg;
                  }
                  break;

               case OptionArgumentType.UnsignedLong:
               case OptionArgumentType.UnsignedLongOrNot:
                  if (Typ == OptionArgumentType.UnsignedLong ||
                      (Typ == OptionArgumentType.UnsignedLongOrNot && sArgument != null)) {
                     if (!UnsignedLongIsPossible(sArgument, out ulArg))
                        ThrowExceptionFalseType1();
                     oArgument = ulArg;
                  }
                  break;

               case OptionArgumentType.PositivLong:
               case OptionArgumentType.PositivLongOrNot:
                  if (Typ == OptionArgumentType.PositivLong ||
                      (Typ == OptionArgumentType.PositivLongOrNot && sArgument != null)) {
                     if (!UnsignedLongIsPossible(sArgument, out ulArg) || ulArg == 0)
                        ThrowExceptionFalseType1();
                     oArgument = ulArg;
                  }
                  break;

               case OptionArgumentType.Double:
               case OptionArgumentType.DoubleOrNot:
                  if (Typ == OptionArgumentType.Double ||
                      (Typ == OptionArgumentType.DoubleOrNot && sArgument != null)) {
                     if (!DoubleIsPossible(sArgument, out dArg))
                        ThrowExceptionFalseType1();
                     oArgument = dArg;
                  }
                  break;

               case OptionArgumentType.Boolean:
               case OptionArgumentType.BooleanOrNot:
                  if (Typ == OptionArgumentType.Boolean ||
                      (Typ == OptionArgumentType.BooleanOrNot && sArgument != null)) {
                     if (!BooleanIsPossible(sArgument, out bArg))
                        ThrowExceptionFalseType1();
                     oArgument = bArg;
                  }
                  break;

            }
         }

         protected void ThrowExceptionFalseType1() {
            throw new Exception(string.Format("Die Option '{0}' muss ein {1}-Argument haben.", FullName(), Type));
         }

         protected void ThrowExceptionFalseType2(OptionArgumentType falsetype) {
            throw new Exception(string.Format("Die Option '{0}' wurde nicht als {1} sondern als {2} festgelegt.", FullName(), falsetype, Type));
         }

         /// <summary>
         /// Wurde die Option mit Argument verwendet?
         /// </summary>
         /// <returns></returns>
         public bool ArgUsed() {
            if (Type == OptionArgumentType.Nothing)
               return false;
            return oArgument != null;
         }

         /// <summary>
         /// liefert das Argument wenn möglich als String, sonst null
         /// <para>Bei einem abweichenden Typ wird eine Exception ausgelöst.</para>
         /// </summary>
         /// <returns></returns>
         public string AsString() {
            switch (Type) {
               case OptionArgumentType.String:
               case OptionArgumentType.StringOrNot:
                  if (oArgument != null)
                     return Convert.ToString(oArgument);
                  return null; // ohne Argument
            }
            ThrowExceptionFalseType2(OptionArgumentType.String);
            return null;
         }

         /// <summary>
         /// liefert das Argument als Integer
         /// <para>Ohne Argument wird <see cref="int.MaxValue"/> geliefert.</para>
         /// <para>Bei einem abweichenden Typ wird eine Exception ausgelöst.</para>
         /// </summary>
         /// <returns></returns>
         public int AsInteger() {
            switch (Type) {
               case OptionArgumentType.Integer:
               case OptionArgumentType.IntegerOrNot:
                  if (oArgument != null)
                     return Convert.ToInt32(oArgument);
                  return int.MaxValue; // ohne Argument
            }
            ThrowExceptionFalseType2(OptionArgumentType.Integer);
            return 0;
         }

         /// <summary>
         /// liefert das Argument als UnsignedInteger
         /// </summary>
         /// <para>Ohne Argument wird <see cref="uint.MaxValue"/> geliefert.</para>
         /// <para>Bei einem abweichenden Typ wird eine Exception ausgelöst.</para>
         /// <returns></returns>
         public uint AsUnsignedInteger() {
            switch (Type) {
               case OptionArgumentType.UnsignedInteger:
               case OptionArgumentType.UnsignedIntegerOrNot:
                  if (oArgument != null)
                     return Convert.ToUInt32(oArgument);
                  return uint.MaxValue; // ohne Argument
            }
            ThrowExceptionFalseType2(OptionArgumentType.UnsignedInteger);
            return 0;
         }

         /// <summary>
         /// liefert das Argument als PositivInteger
         /// <para>Ohne Argument wird <see cref="uint.MaxValue"/> geliefert.</para>
         /// <para>Bei einem abweichenden Typ wird eine Exception ausgelöst.</para>
         /// </summary>
         /// <returns></returns>
         public uint AsPositivInteger() {
            switch (Type) {
               case OptionArgumentType.PositivInteger:
               case OptionArgumentType.PositivIntegerOrNot:
                  if (oArgument != null)
                     return Convert.ToUInt32(oArgument);
                  return uint.MaxValue; // ohne Argument
            }
            ThrowExceptionFalseType2(OptionArgumentType.PositivInteger);
            return 0;
         }

         /// <summary>
         /// liefert das Argument als Long
         /// <para>Ohne Argument wird <see cref="long.MaxValue"/> geliefert.</para>
         /// <para>Bei einem abweichenden Typ wird eine Exception ausgelöst.</para>
         /// </summary>
         /// <returns></returns>
         public long AsLong() {
            switch (Type) {
               case OptionArgumentType.Long:
               case OptionArgumentType.LongOrNot:
                  if (oArgument != null)
                     return Convert.ToInt64(oArgument);
                  return long.MaxValue; // ohne Argument
            }
            ThrowExceptionFalseType2(OptionArgumentType.Long);
            return 0;
         }

         /// <summary>
         /// liefert das Argument als UnsignedLong
         /// <para>Ohne Argument wird <see cref="ulong.MaxValue"/> geliefert.</para>
         /// <para>Bei einem abweichenden Typ wird eine Exception ausgelöst.</para>
         /// </summary>
         /// <returns></returns>
         public ulong AsUnsignedLong() {
            switch (Type) {
               case OptionArgumentType.UnsignedLong:
               case OptionArgumentType.UnsignedLongOrNot:
                  if (oArgument != null)
                     return Convert.ToUInt64(oArgument);
                  return ulong.MaxValue; // ohne Argument
            }
            ThrowExceptionFalseType2(OptionArgumentType.UnsignedLong);
            return 0;
         }

         /// <summary>
         /// liefert das Argument als PositivLong
         /// <para>Ohne Argument wird <see cref="ulong.MaxValue"/> geliefert.</para>
         /// <para>Bei einem abweichenden Typ wird eine Exception ausgelöst.</para>
         /// </summary>
         /// <returns></returns>
         public ulong AsPositivLong() {
            switch (Type) {
               case OptionArgumentType.PositivLong:
               case OptionArgumentType.PositivLongOrNot:
                  if (oArgument != null)
                     return Convert.ToUInt64(oArgument);
                  return ulong.MaxValue; // ohne Argument
            }
            ThrowExceptionFalseType2(OptionArgumentType.PositivLong);
            return 0;
         }

         /// <summary>
         /// liefert das Argument als Double
         /// <para>Ohne Argument wird <see cref="double.MaxValue"/> geliefert.</para>
         /// <para>Bei einem abweichenden Typ wird eine Exception ausgelöst.</para>
         /// </summary>
         /// <returns></returns>
         public double AsDouble() {
            switch (Type) {
               case OptionArgumentType.Double:
               case OptionArgumentType.DoubleOrNot:
                  if (oArgument != null)
                     return Convert.ToDouble(oArgument);
                  return ulong.MaxValue; // ohne Argument
            }
            ThrowExceptionFalseType2(OptionArgumentType.Double);
            return 0;
         }

         /// <summary>
         /// liefert das Argument als Boolean
         /// <para>Ohne Argument wird false geliefert.</para>
         /// <para>Bei einem abweichenden Typ wird eine Exception ausgelöst.</para>
         /// </summary>
         /// <returns></returns>
         public bool AsBoolean() {
            switch (Type) {
               case OptionArgumentType.Boolean:
               case OptionArgumentType.BooleanOrNot:
                  if (oArgument != null)
                     return Convert.ToBoolean(oArgument);
                  return false; // ohne Argument
            }
            ThrowExceptionFalseType2(OptionArgumentType.Boolean);
            return false;
         }

         /// <summary>
         /// Name der Option mit führendem '-' oder '--'
         /// </summary>
         /// <returns></returns>
         public string Name() {
            return string.Format("{0}{1}", bShort ? "-" : "--", sOption);
         }

         /// <summary>
         /// Name der Option mit ev. vorhandenem Argument
         /// </summary>
         /// <returns></returns>
         public string FullName() {
            return oArgument != null ?
                           Name() + " " + oArgument.ToString() :
                           Name();
         }

         public override string ToString() {
            return string.Format("{0} (Key={1}, Typ={2}, Argument={3})", Name(), iKey, Type, oArgument == null ? "[null]" : oArgument.ToString());
         }
      }


      #region Konvertierungsfunktionen (i.A. sind auch hexadezimale Werte zulässig)

      /// <summary>
      /// Kann das Argument als long bzw. ulong interpretiert werden?
      /// <para>Mit dem Präfix 0x wird der Text hexadezimal interpretiert.</para>
      /// <para>Mit dem Präfix 0d wird der Text dual interpretiert.</para>
      /// <para>Mit dem Präfix 0o wird der Text oktal interpretiert.</para>
      /// </summary>
      /// <param name="sArgument"></param>
      /// <param name="unsigned"></param>
      /// <param name="val"></param>
      /// <param name="uval"></param>
      /// <returns>true, wenn erfolgreiche Konvertierung</returns>
      static bool NumericIsPossible(string sArgument, bool unsigned, out Int64 val, out UInt64 uval) {
         bool bPossible = false;
         val = 0;
         uval = 0;

         try {
            if (unsigned)
               uval = Convert.ToUInt64(sArgument);
            else
               val = Convert.ToInt64(sArgument);
            bPossible = true;
         } catch { }

         if (!bPossible)
            if (sArgument.Length > 2 && sArgument[0] == '0')
               try {
                  int fromBase = 10;
                  if (sArgument[1] == 'x')
                     fromBase = 16; // Test auf Hex-Zahl
                  else if (sArgument[1] == 'd')
                     fromBase = 2; // Test auf Dual-Zahl
                  else if (sArgument[1] == 'o')
                     fromBase = 8; // Test auf Oktal-Zahl

                  if (unsigned)
                     uval = Convert.ToUInt64(sArgument.Substring(2), fromBase);
                  else
                     val = Convert.ToInt64(sArgument.Substring(2), fromBase);
                  bPossible = true;
               } catch { }

         return bPossible;
      }

      /// <summary>
      /// Kann das Argument als Double interpretiert werden?
      /// </summary>
      /// <param name="sArgument"></param>
      /// <param name="val"></param>
      /// <returns>true, wenn erfolgreiche Konvertierung</returns>
      public static bool DoubleIsPossible(string sArgument, out double val) {
         bool bPossible = true;
         val = 0;
         try {
            val = Convert.ToDouble(sArgument, CultureInfo.InvariantCulture);
         } catch {
            bPossible = false;
         }
         return bPossible;
      }

      /// <summary>
      /// Kann das Argument als Integer (auch hexadezimal mit führendem 0x) interpretiert werden?
      /// </summary>
      /// <param name="sArgument"></param>
      /// <param name="val"></param>
      /// <returns>true, wenn erfolgreiche Konvertierung</returns>
      public static bool IntegerIsPossible(string sArgument, out int val) {
         bool bPossible = false;
         val = 0;
         long tmp;
         ulong utmp;
         if (NumericIsPossible(sArgument, false, out tmp, out utmp))
            if (int.MinValue <= tmp && tmp <= int.MaxValue) {
               val = (int)tmp;
               bPossible = true;
            }
         return bPossible;
      }

      /// <summary>
      /// Kann das Argument als UInteger (auch hexadezimal mit führendem 0x) interpretiert werden?
      /// </summary>
      /// <param name="sArgument"></param>
      /// <param name="val"></param>
      /// <returns>true, wenn erfolgreiche Konvertierung</returns>
      public static bool UnsignedIntegerIsPossible(string sArgument, out uint val) {
         bool bPossible = false;
         val = 0;
         long tmp;
         ulong utmp;
         if (NumericIsPossible(sArgument, true, out tmp, out utmp))
            if (uint.MinValue <= utmp && utmp <= uint.MaxValue) {
               val = (uint)utmp;
               bPossible = true;
            }
         return bPossible;
      }

      /// <summary>
      /// Kann das Argument als Long (auch hexadezimal mit führendem 0x) interpretiert werden?
      /// </summary>
      /// <param name="sArgument"></param>
      /// <param name="val"></param>
      /// <returns>true, wenn erfolgreiche Konvertierung</returns>
      public static bool LongIsPossible(string sArgument, out long val) {
         bool bPossible = false;
         val = 0;
         ulong utmp;
         if (NumericIsPossible(sArgument, false, out val, out utmp))
            bPossible = true;
         return bPossible;
      }

      /// <summary>
      /// Kann das Argument als ULong (auch hexadezimal mit führendem 0x) interpretiert werden?
      /// </summary>
      /// <param name="sArgument"></param>
      /// <param name="val"></param>
      /// <returns>true, wenn erfolgreiche Konvertierung</returns>
      public static bool UnsignedLongIsPossible(string sArgument, out ulong val) {
         bool bPossible = false;
         val = 0;
         long tmp;
         if (NumericIsPossible(sArgument, true, out tmp, out val))
            bPossible = true;
         return bPossible;
      }

      /// <summary>
      /// Kann das Argument als Boolean (auch hexadezimal mit führendem 0x; true, false, 0, 1.0 usw.) interpretiert werden?
      /// </summary>
      /// <param name="sArgument"></param>
      /// <param name="val"></param>
      /// <returns>true, wenn erfolgreiche Konvertierung</returns>
      public static bool BooleanIsPossible(string sArgument, out bool val) {
         bool bPossible = true;
         val = true;
         try {
            val = Convert.ToBoolean(sArgument);          // fkt. nur bei "true" oder "false"
         } catch {
            bPossible = false;
         }
         if (!bPossible)
            try {
               bPossible = true;
               int iVal;
               double dVal;
               if (IntegerIsPossible(sArgument, out iVal))
                  val = iVal != 0;
               else
                  if (DoubleIsPossible(sArgument, out dVal))
                  val = dVal != 0.0;
               else
                  bPossible = false;
            } catch {
               bPossible = false;
            }
         return bPossible;
      }

      #endregion


      /// <summary>
      /// Liste aller erlaubten Optionen
      /// </summary>
      protected Dictionary<int, OptionDefinition> DefinedOptions;

      /// <summary>
      /// Liste aller gefundenen Optionen (mit ihren Argumenten)
      /// </summary>
      protected List<SampledOption> SampledOptions;


      public CmdlineOptions() {
         DefinedOptions = new Dictionary<int, OptionDefinition>();
         ClearParse();
      }

      #region Definition erlaubter Optionen

      /// <summary>
      /// eine neue Option definieren
      /// </summary>
      /// <param name="uniquekey">eindeutiger int-Schlüssel</param>
      /// <param name="sLongOption">langer Name (oder "")</param>
      /// <param name="sShortOption">kurzer Name (oder "")</param>
      /// <param name="sHelpText">Hilfetext (Umbruch jeweils bei \n)</param>
      /// <param name="argtype">Art der Option</param>
      /// <param name="maxcount">max. Anzahl des Auftretens der Option</param>
      public void DefineOption(int uniquekey, string sLongOption, string sShortOption, string sHelpText, OptionArgumentType argtype = OptionArgumentType.Nothing, int maxcount = 1) {
         OptionDefinition def = new OptionDefinition(uniquekey, sLongOption, sShortOption, sHelpText, argtype, maxcount);
         OptionDefinition old = IsOptionDefined(def);
         if (old != null)
            throw new Exception(string.Format("Die Option '{0}' steht im Konflikt mit der Option '{1}'.", def.ToString(), old.ToString()));
         else {
            Match ma;
            if (def.LongName().Length > 0) {
               ma = rex_opt_long.Match(def.LongName());
               if (!ma.Success)
                  throw new ArgumentException(string.Format("Der lange Name für die Option '{0}' ist ungültig.", def));
            }
            if (def.ShortName().Length > 0) {
               ma = rex_opt_short.Match(def.ShortName());
               if (!ma.Success)
                  throw new ArgumentException(string.Format("Der kurze Name für die Option '{0}' ist ungültig.", def));
            }
            DefinedOptions.Add(def.iKey, def);
         }
      }

      /// <summary>
      /// Ist diese Option definiert?
      /// <para>Es genügt die Existenz des gleichen <see cref="OptionDefinition.iKey"/>. Andernfalls wird noch getestet, ob schon eine Option mit der
      /// gleichen nichtleeren <see cref="OptionDefinition.sLongOption"/> oder <see cref="OptionDefinition.sShortOption"/> existiert.</para>
      /// </summary>
      /// <param name="def"></param>
      /// <returns>schon existierende Definition</returns>
      protected OptionDefinition IsOptionDefined(OptionDefinition def) {
         if (DefinedOptions.ContainsKey(def.iKey))
            return DefinedOptions[def.iKey];

         foreach (var item in DefinedOptions)
            if ((def.sLongOption.Length > 0 && item.Value.sLongOption == def.sLongOption) ||
                (def.sShortOption.Length > 0 && item.Value.sShortOption == def.sShortOption))
               return item.Value;
         return null;
      }

      /// <summary>
      /// Ist diese Option definiert?
      /// </summary>
      /// <param name="sOption">Optionstext (lang oder kurz)</param>
      /// <param name="bShort">kurze oder lange Version</param>
      /// <returns>OptionDefinition, wenn erlaubt, sonst null</returns>
      protected OptionDefinition IsOptionDefined(string sOption, bool bShort) {
         foreach (var item in DefinedOptions) {
            if ((bShort ? item.Value.sShortOption : item.Value.sLongOption) == sOption)
               return item.Value;
         }
         return null;
      }

      /// <summary>
      /// Ist diese Option definiert?
      /// </summary>
      /// <param name="sOption">Optionstext</param>
      /// <param name="bShort">kurze oder lange Version</param>
      /// <param name="argtype">Definitionstyp</param>
      /// <returns>OptionDefinition, wenn erlaubt, sonst null</returns>
      protected OptionDefinition IsOptionDefined(string sOption, bool bShort, OptionArgumentType argtype) {
         foreach (var item in DefinedOptions) {
            if ((bShort ? item.Value.sShortOption : item.Value.sLongOption) == sOption &&
                item.Value.Type == argtype)
               return item.Value;
         }
         return null;
      }

      /// <summary>
      /// Ist diese Option definiert?
      /// </summary>
      /// <param name="key"></param>
      /// <returns>OptionDefinition, wenn erlaubt, sonst null</returns>
      protected OptionDefinition IsOptionDefined(int key) {
         return DefinedOptions.ContainsKey(key) ?
                     DefinedOptions[key] :
                     null;
      }

      #endregion

      #region Einlesen und Interpretieren der Kommandozeile

      protected void ClearParse() {
         Parameters = new List<string>();
         SampledOptions = new List<SampledOption>();
      }

      /// <summary>
      /// die Kommandozeile wird eingelesen (kann mehrfach verwendet werden)
      /// </summary>
      /// <param name="sArgs"></param>
      public void Parse(string[] sArgs) {
         ClearParse();

         for (int pos = 0; pos < sArgs.Length; pos++) {

            string testopt = sArgs[pos];
            string testarg = pos < sArgs.Length - 1 ? sArgs[pos + 1] : null;

            Match ma = rex_opt_long.Match(testopt);
            if (ma.Success) { // eine lange Option --xxx oder /xxx

               //CaptureCollection cc = ma.Captures;
               string sOptionPrefix = ma.Groups[1].Value; // '--' oder '/'
               string sOption = ma.Groups[2].Value; // Optionstext
               if (sOption == testopt.Substring(sOptionPrefix.Length)) {
                  // Hat das Argument die Syntax einer Option? Dann wird es NICHT verwendet.
                  if (hasOptionSyntax(testarg))
                     testarg = null;
                  else
                     pos++;
                  RegisterOption(sOption, false, testarg);
               } else { // nur der Anfang ist mit Option identisch ...
                  if (testopt[sOption.Length + sOptionPrefix.Length] == '=' || // ... danach folgt '=' oder ':', also ein Argument
                      testopt[sOption.Length + sOptionPrefix.Length] == ':') {
                     testarg = testopt.Substring(sOption.Length + sOptionPrefix.Length + 1);
                     RegisterOption(sOption, false, testarg);
                  }
               }
               continue;

            } else {

               ma = rex_opt_short.Match(testopt);
               if (ma.Success) { // eine kurze Option -x oder -xyz

                  string sOption = ma.Groups[1].Value;
                  if (sOption.Length > 1) // bei mehreren zusammengefassten Optionen folgt NIE ein Argument
                     testarg = null;
                  // Hat das Argument die Syntax einer Option? Dann wird es NICHT verwendet.
                  if (hasOptionSyntax(testarg))
                     testarg = null;
                  else
                     pos++;
                  for (int j = 0; j < sOption.Length; j++)
                     RegisterOption(sOption.Substring(j, 1), true, testarg);

               } else { // dann ist es nur ein Parameter

                  Parameters.Add(sArgs[pos]);

               }

            }
         }
      }

      /// <summary>
      /// hat der Text die Syntax einer Option (wenigsten am Anfang)
      /// </summary>
      /// <param name="txt"></param>
      /// <returns></returns>
      protected bool hasOptionSyntax(string txt) {
         if (!string.IsNullOrEmpty(txt)) {
            Match ma = rex_opt_long.Match(txt);
            if (ma.Success)
               return true;

            ma = rex_opt_short.Match(txt);
            if (ma.Success)
               return true;
         }
         return false;
      }

      /// <summary>
      /// die Option wird, wenn angegeben mit Argument, registriert 
      /// <para>Falls sie zu oft verwendet wird oder falls ein Argument nicht richtig interpretierbar ist, wird eine Exception ausgelöst.</para>
      /// </summary>
      /// <param name="sOption">Optionstext</param>
      /// <param name="bShort">lange oder kurze Option</param>
      /// <param name="sArgument">wenn ungleich null, muss das Argument verwendet werden<para>ev. mehrere Argumente mit ';' verknüpft</para></param>
      /// <returns></returns>
      protected void RegisterOption(string sOption, bool bShort, string sArgument) {
         OptionDefinition def = IsOptionDefined(sOption, bShort);
         if (def == null)
            throw new Exception(string.Format("Die Option '{0}{1}' ist nicht erlaubt.", bShort ? "-" : "--", sOption));

         if (def != null) {
            // erstmal alle ';' als Trennstelle im Argument ansehen
            string[] sArgumentList = sArgument != null ? sArgument.Split(new char[] { ';' }) : new string[0];
            // aber: '\;' ist KEINE Trennstelle (ist ein '...\;...' o.ä. tatsächlich als Trennstelle gedacht, MUSS der Umweg über 
            // 2 getrennte Optionsangaben gegangen werden)

            if (sArgumentList.Length > 1) {
               List<string> sTmp = new List<string>();
               sTmp.AddRange(sArgumentList);
               for (int i = 0; i < sTmp.Count - 1; i++)
                  if (sTmp[i].Length > 0 && sTmp[i][sTmp[i].Length - 1] == '\\') {     // letztes Zeichen ein '\' --> es war ein "maskiertes" ';'
                     sTmp[i] = sTmp[i].Substring(0, sTmp[i].Length - 1);
                     sTmp[i] += ";";
                     sTmp[i] += sTmp[i + 1];
                     sTmp.RemoveAt(i + 1);
                     i--;
                  }
               sArgumentList = new string[sTmp.Count];
               sTmp.CopyTo(sArgumentList);
            }

            // Jetzt ex. eine gültige Argumentliste mit min. 0, 1 oder mehr Argument.
            for (int i = 0; i < sArgumentList.Length || (sArgument == null && i < 1); i++)
               if (GetSampledOptionPosition(def.iKey, def.iMaxCount - 1) < 0)
                  SampledOptions.Add(new SampledOption(def.iKey, sOption, bShort, def.Type, sArgument == null ? null : sArgumentList[i]));
               else
                  throw new Exception(string.Format("Die Option '{0}' darf höchstens {1} mal verwendet werden.", def.Name(), def.iMaxCount));
         } else
            throw new Exception(string.Format("Die Option '{0}{1}' ist nicht erlaubt.", bShort ? "-" : "--", sOption));
      }

      /// <summary>
      /// liefert die Position der schon eingesammelten Option in der Liste (oder -1)
      /// </summary>
      /// <param name="key">Options-Schlüssel</param>
      /// <param name="no">no-tes Auftreten</param>
      /// <returns>Index</returns>
      protected int GetSampledOptionPosition(int key, int no) {
         int count = 0;
         for (int i = 0; i < SampledOptions.Count; i++)
            if (SampledOptions[i].iKey == key) {
               if (count == no)
                  return i;
               count++;
            }
         return -1;
      }

      #endregion

      #region Abfrage von Optionen und Parametern

      /// <summary>
      /// Liste der reinen Parameter der Kommandozeile
      /// </summary>
      public List<string> Parameters { get; private set; }

      /// <summary>
      /// Anzahl der erlaubten Optionen
      /// </summary>
      public int DefinedOptionsCount { get { return DefinedOptions.Count; } }

      /// <summary>
      /// prüft ob der Optionsschlüssel gültig ist und löst andernfalls eine Exception aus
      /// </summary>
      /// <param name="key">Options-Schlüssel</param>
      protected void CheckValidOption(int key) {
         if (IsOptionDefined(key) == null)
            throw new Exception(string.Format("Der Options-Schlüssel ({0}) existiert nicht.", key));
      }

      /// <summary>
      /// liefert den definierten Typ dieser Option
      /// </summary>
      /// <param name="key">Options-Schlüssel</param>
      /// <returns></returns>
      public OptionArgumentType OptionType(int key) {
         CheckValidOption(key);
         return DefinedOptions[key].Type;
      }

      /// <summary>
      /// liefert den Namen dieser Option
      /// </summary>
      /// <param name="key">Options-Schlüssel</param>
      /// <returns></returns>
      public string OptionName(int key) {
         CheckValidOption(key);
         return DefinedOptions[key].Name();
      }

      /// <summary>
      /// liefert, wie oft diese Option in der Kommandozeile angewendet wurde
      /// </summary>
      /// <param name="key">Options-Schlüssel</param>
      /// <returns></returns>
      public int OptionAssignment(int key) {
         CheckValidOption(key);
         int count = 0;
         for (int i = 0; i < SampledOptions.Count; i++)
            if (SampledOptions[i].iKey == key)
               count++;
         return count;
      }


      /// <summary>
      /// liefert, ob die Option verwendet wurde
      /// </summary>
      /// <param name="key">Options-Schlüssel</param>
      /// <returns></returns>
      public bool OptionIsUsed(int key, bool lazy) {
         bool used = false;
         try {
            SampledOption opt = GetSampledOption(key, 0);
            used = true;
         } catch { }
         return used;
      }

      /// <summary>
      /// Wurde ein Argument angegeben?
      /// </summary>
      /// <param name="key">Options-Schlüssel</param>
      /// <param name="no">Nummer des Auftretens dieser Option (kleiner als OptionAssignment()!)</param>
      /// <returns></returns>
      public bool ArgIsUsed(int key, int no = 0) {
         SampledOption opt = GetSampledOption(key, no);
         return opt.ArgUsed();
      }


      /// <summary>
      /// liefert das Argument der Option als String
      /// </summary>
      /// <param name="key">Options-Schlüssel</param>
      /// <param name="no">Nummer des Auftretens dieser Option (kleiner als OptionAssignment()!)</param>
      /// <returns></returns>
      public string StringValue(int key, int no = 0) {
         SampledOption opt = GetSampledOption(key, no);
         return opt.AsString();
      }

      /// <summary>
      /// liefert das Argument der Option als Integer
      /// <para>löst eine Exception aus, wenn der Optionstyp nicht identisch ist</para>
      /// </summary>
      /// <param name="key">Options-Schlüssel</param>
      /// <param name="no">Nummer des Auftretens dieser Option (kleiner als OptionAssignment()!)</param>
      /// <returns></returns>
      public int IntegerValue(int key, int no = 0) {
         SampledOption opt = GetSampledOption(key, no);
         return opt.AsInteger();
      }

      /// <summary>
      /// liefert das Argument der Option als UnsignedInteger
      /// </summary>
      /// <param name="key">Options-Schlüssel</param>
      /// <param name="no">Nummer des Auftretens dieser Option (kleiner als OptionAssignment()!)</param>
      /// <returns></returns>
      public uint UnsignedIntegerValue(int key, int no = 0) {
         SampledOption opt = GetSampledOption(key, no);
         return opt.AsUnsignedInteger();
      }

      /// <summary>
      /// liefert das Argument der Option als PositivInteger
      /// </summary>
      /// <param name="key">Options-Schlüssel</param>
      /// <param name="no">Nummer des Auftretens dieser Option (kleiner als OptionAssignment()!)</param>
      /// <returns></returns>
      public uint PositivIntegerValue(int key, int no = 0) {
         SampledOption opt = GetSampledOption(key, no);
         return opt.AsPositivInteger();
      }

      /// <summary>
      /// liefert das Argument der Option als Long
      /// </summary>
      /// <param name="key">Options-Schlüssel</param>
      /// <param name="no">Nummer des Auftretens dieser Option (kleiner als OptionAssignment()!)</param>
      /// <returns></returns>
      public long LongValue(int key, int no = 0) {
         SampledOption opt = GetSampledOption(key, no);
         return opt.AsLong();
      }

      /// <summary>
      /// liefert das Argument der Option als UnsignedLong
      /// </summary>
      /// <param name="key">Options-Schlüssel</param>
      /// <param name="no">Nummer des Auftretens dieser Option (kleiner als OptionAssignment()!)</param>
      /// <returns></returns>
      public ulong UnsignedLongValue(int key, int no = 0) {
         SampledOption opt = GetSampledOption(key, no);
         return opt.AsUnsignedLong();
      }

      /// <summary>
      /// liefert das Argument der Option als PositivLong
      /// </summary>
      /// <param name="key">Options-Schlüssel</param>
      /// <param name="no">Nummer des Auftretens dieser Option (kleiner als OptionAssignment()!)</param>
      /// <returns></returns>
      public ulong PositivLongValue(int key, int no = 0) {
         SampledOption opt = GetSampledOption(key, no);
         return opt.AsPositivLong();
      }

      /// <summary>
      /// liefert das Argument der Option als Double
      /// </summary>
      /// <param name="key">Options-Schlüssel</param>
      /// <param name="no">Nummer des Auftretens dieser Option (kleiner als OptionAssignment()!)</param>
      /// <returns></returns>
      public double DoubleValue(int key, int no = 0) {
         SampledOption opt = GetSampledOption(key, no);
         return opt.AsDouble();
      }

      /// <summary>
      /// liefert das Argument der Option als Boolean
      /// </summary>
      /// <param name="key">Options-Schlüssel</param>
      /// <param name="no">Nummer des Auftretens dieser Option (kleiner als OptionAssignment()!)</param>
      /// <returns></returns>
      public bool BooleanValue(int key, int no = 0) {
         SampledOption opt = GetSampledOption(key, no);
         return opt.AsBoolean();
      }

      /// <summary>
      /// liefert die eingesammelte Option oder löst eine Exception aus
      /// </summary>
      /// <param name="key">Options-Schlüssel</param>
      /// <param name="no">Nummer des Auftretens dieser Option (kleiner als OptionAssignment()!)</param>
      /// <returns></returns>
      protected SampledOption GetSampledOption(int key, int no) {
         CheckValidOption(key);
         int count = 0;
         for (int i = 0; i < SampledOptions.Count; i++)
            if (SampledOptions[i].iKey == key) {
               if (count == no)
                  return SampledOptions[i];
               count++;
            }
         return null;
      }

      #endregion


      const int MINGAP_HELPTEXT = 3;

      /// <summary>
      /// liefert je Option die Hilfezeile
      /// </summary>
      /// <returns></returns>
      public List<string> GetHelpText() {
         int[] optkey = new int[DefinedOptionsCount];
         DefinedOptions.Keys.CopyTo(optkey, 0);             // alle definierten Schlüssel holen
         Array.Sort<int>(optkey);                           // ... und sortieren
         List<string> txt = new List<string>();
         int iOptArgLength = 0;
         for (int i = 0; i < optkey.Length; i++) {
            txt.Add(string.Format("{0}{1}", DefinedOptions[optkey[i]].Name(), DefinedOptions[optkey[i]].Type != OptionArgumentType.Nothing ? "=arg" : ""));
            iOptArgLength = Math.Max(iOptArgLength, txt[i].Length);
         }
         iOptArgLength += MINGAP_HELPTEXT;
         for (int i = 0; i < txt.Count; i++) {
            txt[i] += new string(' ', iOptArgLength - txt[i].Length);
            txt[i] += DefinedOptions[optkey[i]].sHelpText;
         }
         // notfalls noch umbrechen
         for (int i = 0; i < txt.Count; i++) {
            int nl = txt[i].IndexOf('\n');
            if (nl > 0) {        // Zeile trennen
               string newline = new string(' ', iOptArgLength) + txt[i].Substring(nl + 1);
               txt[i] = txt[i].Substring(0, nl);
               txt.Insert(i + 1, newline);
            }
         }
         return txt;
      }

      public override string ToString() {
         return string.Format("{0} definierte Optionen; {1} Optionen erkannt",
                                 DefinedOptions != null ? DefinedOptionsCount : 0,
                                 SampledOptions != null ? SampledOptions.Count : 0);
      }

   }
}
