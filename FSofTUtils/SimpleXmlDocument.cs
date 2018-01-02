/* Erweiterung der Klasse XmlDocument
 * zur einfacheren Verarbeitung und zur Verwendung für Konfigurations- und Datendateien
 * 
 * copyright by FSofT
 * 
 * letzte Änderung
 * 7.5.2017
 * 
 */
#define NO_DEBUGOUTPUT
#define HIDE_OBSOLETE

using System;
using System.IO;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Schema;
using System.Text;
using System.Collections.Generic;

namespace FSoftUtils {


   /// <summary>
   /// Erweiterung von XmlDocument um insbesondere per XPath Daten lesen und verändern zu können
   /// 
   ///      ExistXPath("/*/node1/@attribute1")
   ///         Testet die Existenz des XPATH
   /// 
   /// Lesen von Daten mit Read...(), z.B.:
   /// 
   ///      ReadValue("/*/node1[@attribut1=wert1]", 999)
   ///         Der Ergebnistyp hängt vom Typ des default-Wertes ab. Der default-Wert wird immer dann geliefert, wenn der XPATH nicht existiert, eine Konvertierung in
   ///         den default-Typ nicht möglich ist oder es mehr als 1 Ergebnis gibt.
   ///         
   ///      ReadInt("/*/node1", 0), ReadBool usw.
   ///         Liefert ein Ergebnis-Array oder null, wenn der XPATH nicht ex.. Ist die Konvertierung nicht möglich wird der default-Wert geliefert.
   ///         
   ///      ReadAttributes("/*/node1")
   ///         Liefert alle Attribut-Wert-Paare für (jeden) Knoten "node1"
   /// 
   /// Ändern von Daten (ACHTUNG: Der XPath kann prinzipiell mehrere Zielpositionen adressieren.):
   ///         
   ///      Change("/*/node1", "wert1")
   ///         Für (jeden) "node1" wird der Wert neu gesetzt.
   ///      Change("/*/node1/@attribut1", "wert2")
   ///         Für (jeden) "node1" wird das Attribut neu gesetzt.
   ///         Mehrere Attribute eines Knotens können gleichzeitig mit Append() gesetzt werden.
   ///      Change("/*/node1[1]", "wert1")
   ///         Für den 1. "node1" wird der Wert neu gesetzt.
   ///
   ///      Remove("/*/node1/@attribut1")
   ///         Das Attribut wird in (jedem) "node1" gelöscht.
   ///      Remove("/*/node1")
   ///         (Jeder) "node1" mit allen untergeordneten Knoten/Attributen wird gelöscht.
   ///      Remove("/*/node1[3]")
   ///         Der 3. "node1" wird mit allen untergeordneten Knoten/Attributen gelöscht.
   ///         
   ///      Append("/*/node1", "node2", "wert2")
   ///         An (jeden) "node1" wird ein neuer untergeordneter Knoten "node2" mit dem Wert "wert2" angehängt.
   ///      Append("/*/node1", "node2", "wert2", new Dictionary<string, string>() { { "attribut1", "wert3" }, { "attribut2", null } })
   ///         An (jeden) "node1" wird ein neuer untergeordneter Knoten "node2" mit dem Wert "wert2" und den 2 Attributen angehängt.
   ///      Append("/*/node1", "node2", null, new Dictionary<string, string>() { { "attribut1", "wert3" }, { "attribut2", null } })
   ///         An (jeden) "node1" wird ein neuer untergeordneter Knoten "node2" ohne Wert aber mit den 2 Attributen angehängt.
   ///      Append("/*/node1", "node2", "wert2", null, true)
   ///         An (jeden) "node1" wird ein neuer untergeordneter Knoten "node2" angehängt, wenn er nicht schon ex. Auf jeden Fall wird der Wert von "node2" auf "wert2" gesetzt.
   ///      Append("/*/node1", null, null, new Dictionary<string, string>() { { "attribut1", "wert3" }, { "attribut2", null } })
   ///         An (jeden) "node1" werden die 2 Attributen angehängt bzw. geändert.
   ///         Ein einzelnes Attribut kann auch mit Change() gesetzt werden.
   /// 
   ///      InsertNode(string xpath4node, string nodename, string nodevalue = null, Dictionary<string, string> attributes = null, bool afterxpath = true)
   ///      
   ///      InsertNode("/*/node1[3]", "node1", "wert1", new Dictionary<string, string>() { { "attribut1", "wert3" }, { "attribut2", null } }, false)
   ///         Vor dem 3. "node1" wird ein neuer "node1" mit dem Wert "wert1" und den 2 Attributen eingefügt.
   ///      InsertNode("/*/node1[3]", "node1")
   ///         Nach dem 3. "node1" wird ein neuer "node1" ohne Wert eingefügt.
   /// 
   /// </summary>
   public class SimpleXmlDocument2 : XmlDocument {

      /// <summary>
      /// letzter Schreibzeitpunkt der Datei
      /// </summary>
      DateTime lastwrite;
      /// <summary>
      /// interner Standard-Navigator
      /// </summary>
      XPathNavigator navigator;
      /// <summary>
      /// interner NamespaceManager
      /// </summary>
      XmlNamespaceManager NsMng;


      /// <summary>
      /// 
      /// </summary>
      /// <param name="sFile">XML-Datei</param>
      /// <param name="sRoot">Wurzelname</param>
      /// <param name="sXsdFile">XSD-Datei</param>
      /// <param name="sEncoding">Codierung:
      /// utf-16, Unicode;
      /// windows-1250, Mitteleuropäisch (Windows);
      /// Windows-1252, Westeuropäisch (Windows);
      /// us-ascii, US-ASCII;
      /// IBM273, IBM EBCDIC (Deutschland);
      /// iso-8859-1, Westeuropäisch (ISO);
      /// iso-8859-2, Mitteleuropäisch (ISO);
      /// utf-7, Unicode (UTF-7);
      /// utf-8, Unicode (UTF-8);
      /// utf-32, Unicode (UTF-32);
      /// utf-32BE, Unicode (UTF-32-Big-Endian);
      /// </param>
      public SimpleXmlDocument2(string sFile = null, string sRoot = null, string sXsdFile = null, string sEncoding = null) {
         CheckNewFile = false;
         Validating = true;
         NsMng = null;
         navigator = null;
         Declaration = CreateXmlDeclaration("1.0", sEncoding != null ? sEncoding : "Windows-1252", null);
         Rootname = string.IsNullOrEmpty(sRoot) ? "dummy" : sRoot;
         XmlFilename = string.IsNullOrEmpty(sFile) ? "dummy.xml" : sFile;
         XsdFilename = string.IsNullOrEmpty(sXsdFile) ? "" : sXsdFile;
         lastwrite = DateTime.MinValue;
      }

      string _sXmlFile;
      /// <summary>
      /// setzt oder liefert den XML-Dateinamen
      /// </summary>
      public string XmlFilename {
         get {
            return _sXmlFile;
         }
         set {
            _sXmlFile = value;
            XsdFilename = Path.GetFileNameWithoutExtension(_sXmlFile) + ".xsd";
         }
      }

      /// <summary>
      /// setzt oder liefert den XSD-Dateinamen
      /// </summary>
      public string XsdFilename { get; set; }

      string _sXmlRootName;
      /// <summary>
      /// setzt oder liefert den Name des Wurzelknotens
      /// </summary>
      public string Rootname {
         get {
            return _sXmlRootName;
         }
         set {
            _sXmlRootName = value;
            if (string.IsNullOrEmpty(_sXmlRootName))
               throw new Exception("Kein Name für die XML-Root angegeben!");
         }
      }

      /// <summary>
      /// setzt oder liefert die XML-Deklaration
      /// </summary>
      public XmlDeclaration Declaration { get; set; }

      /// <summary>
      /// Erfolgt beim Lesen und der Schreiben der XML-Datei eine Validierung?
      /// </summary>
      public bool Validating { get; set; }

      /// <summary>
      /// 'true', wenn die XML-Datei existiert
      /// </summary>
      /// <returns></returns>
      public bool FileExist() {
         return File.Exists(XmlFilename);
      }

      /// <summary>
      /// Soll beim Datenlesen jeweils getestet werden, ob die Datei geändert wurde?
      /// </summary>
      public bool CheckNewFile { get; set; }

      /// <summary>
      /// falls die Datei einen jüngeren Zeitstempel als beim letzten Einlesen hat werden die Daten neu eingelesen
      /// </summary>
      void ReadNewerFile() {
         if (CheckNewFile &&
             File.GetLastWriteTime(XmlFilename) > lastwrite)
            LoadData();
      }

      /// <summary>
      /// Die Konfigurationsdatei wird neu (und ev. mit Validierung!) gelesen. Bei einem Fehler wird eine Exception ausgelöst.
      /// </summary>
      /// <returns>false, wenn das Dateiedatum nicht neuer als das registrierte Dateidatum ist</returns>
      public bool LoadData() {
         if (FileExist()) {
            try {
               DateTime writetime = File.GetLastWriteTime(XmlFilename);
               if (!CheckNewFile ||
                   writetime > lastwrite) {
                  lastwrite = writetime;
                  if (Validating) {
                     XmlReaderSettings xml_rset = new XmlReaderSettings();
                     xml_rset.Schemas.Add(null, XsdFilename);
                     xml_rset.ValidationType = ValidationType.Schema;
                     xml_rset.ValidationEventHandler += new ValidationEventHandler(XmlValidationEventHandler);
                     XmlReader xreader = XmlReader.Create(XmlFilename, xml_rset);
                     Load(xreader);
                     xreader.Close();
                     Rootname = DocumentElement.Name;
                  } else
                     Load(XmlFilename);
                  navigator = CreateNavigator();
                  return true;
               } else
                  return false;
            } catch (Exception ex) {
               throw new Exception(string.Format("Fehler beim Lesen der Datei '{0}': {1}", XmlFilename, ex.Message));
            }
         } else
            throw new Exception(string.Format("Fehler beim Lesen der Datei '{0}': Die Datei existiert nicht!", XmlFilename));
      }

      /// <summary>
      /// fügt einen Namespace für XPATH an
      /// </summary>
      /// <param name="sPrefix"></param>
      /// <param name="sUrl">wenn null, dann wird die interne Namespace-URI verwendet</param>
      public void AddNamespace(string sPrefix, string sUrl = null) {
         if (!string.IsNullOrEmpty(sUrl) ||
             !string.IsNullOrEmpty(DocumentElement.NamespaceURI)) {
            if (string.IsNullOrEmpty(sUrl))
               sUrl = DocumentElement.NamespaceURI;
            if (NsMng == null)
               NsMng = new System.Xml.XmlNamespaceManager(NameTable);
            NsMng.AddNamespace(sPrefix, sUrl);
         }
      }

      static void XmlValidationEventHandler(object sender, ValidationEventArgs e) {
         string ext = "";
         if (e.Exception is XmlSchemaValidationException) {
            XmlSchemaValidationException xmle = (XmlSchemaValidationException)e.Exception;
            ext = xmle.Message + ": " + System.Environment.NewLine;
            if (xmle.SourceObject is XmlAttribute) {
               XmlAttribute attr = xmle.SourceObject as XmlAttribute;
               if (attr.OwnerElement != null)
                  ext += attr.OwnerElement.OuterXml;
            } else
               if (xmle.SourceObject is XmlElement) {
               XmlElement elem = xmle.SourceObject as XmlElement;
               if (elem.ParentNode != null)
                  ext += elem.ParentNode.OuterXml;
            } else
                  if (xmle.SourceObject is XmlNode) {
               XmlNode n = xmle.SourceObject as XmlNode;
               ext += n.OuterXml;
            } else
                     if (xmle.SourceObject != null)
               ext = xmle.SourceObject.ToString();
         }
         if (ext.Length > 300)
            ext = ext.Substring(0, 300) + " ...";
         throw new Exception(ext.Length == 0 ? e.Message : ext);
      }

      /// <summary>
      /// XML-Datei speichern
      /// </summary>
      /// <param name="filename">Name der XML-Datei</param>
      /// <param name="formatted">wenn true, wird der Text formatiert ausgegeben, sonst "1-zeilig"</param>
      public bool SaveData(string filename = null, bool formatted = true) {
         if (filename == null)
            filename = XmlFilename;
         if (!ValidateInternData())
            return false;

         try {
            System.Xml.XmlWriterSettings xmlWriterSettings = new System.Xml.XmlWriterSettings();
            xmlWriterSettings.Encoding = System.Text.Encoding.UTF8;
            xmlWriterSettings.Indent = formatted;
            xmlWriterSettings.IndentChars = " ";
            xmlWriterSettings.NewLineChars = "\r\n";
            xmlWriterSettings.NewLineHandling = System.Xml.NewLineHandling.Replace;
            xmlWriterSettings.NewLineOnAttributes = false;
            //xmlWriterSettings.OmitXmlDeclaration = true;
            xmlWriterSettings.ConformanceLevel = System.Xml.ConformanceLevel.Document;
            using (System.Xml.XmlWriter writer = System.Xml.XmlWriter.Create(filename, xmlWriterSettings)) {
               Save(writer);
               writer.Flush();
               writer.Close();
            }

         } catch (Exception ex) {
            throw new Exception(ex.Message);
         }
         return true;
      }

      /// <summary>
      /// testet die aktuell im Speicher befindlichen XML-Daten, ob sie XML-konform sind
      /// </summary>
      /// <returns></returns>
      public bool ValidateInternData() {
         return ValidateInternDataMsg() == "";
      }

      /// <summary>
      /// testet die aktuell im Speicher befindlichen XML-Daten, ob sie XML-konform sind
      /// </summary>
      /// <returns>leere Zeichenkette oder Fehlertext</returns>
      public string ValidateInternDataMsg() {
         if (!Validating)
            return "";
         try {
            if (this.Schemas.Count == 0)
               this.Schemas.Add(null, this.XsdFilename);
            this.Validate(new ValidationEventHandler(XmlValidationEventHandler));
         } catch (Exception exception) {
            return exception.Message;
         }
         return "";
      }

      /// <summary>
      /// 'true', wenn Daten existieren
      /// </summary>
      /// <returns></returns>
      public bool InternDataExist() {
         return DocumentElement.LocalName == Rootname && DocumentElement.HasChildNodes;
      }

      /// <summary>
      /// ev. schon vorhandene Daten werden gelöscht und eine neue Struktur erzeugt, die nur die Deklaration und das Root-Element hat
      /// </summary>
      /// <param name="sXslStylesheet"></param>
      /// <param name="sComment"></param>
      public void CreateInternData(string sXslStylesheet = null, string sComment = null) {
         if (this.HasChildNodes) this.RemoveAll();
         AppendChild(Declaration);
         AppendChild(CreateNode(XmlNodeType.Element, Rootname, null));
         if (XsdFilename.Length > 0) {
            string tmp = this.InnerXml;
            tmp = tmp.Substring(0, tmp.Length - 2) +
                  " xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:noNamespaceSchemaLocation=\"" + Path.GetFileName(XsdFilename) + "\" />";
            this.RemoveAll();
            this.LoadXml(tmp);
         }
         navigator = CreateNavigator();
         if (sXslStylesheet != null) {
            //<?xml-stylesheet href=".... .xsl" type="text/xsl" ?>
            XmlProcessingInstruction pi = CreateProcessingInstruction("xml-stylesheet", "type='text/xsl' href='" + sXslStylesheet + "'");
            InsertAfter(pi, ChildNodes.Item(0));
         }
         if (sComment != null) {
            XmlNode comment = CreateNode(XmlNodeType.Comment, null, null);
            comment.Value = sComment;
            DocumentElement.AppendChild(comment);
         }
      }

      #region XPath-Funktionen zur Datenabfrage, zum Daten anfügen, löschen und ändern

      // z.B.:
      // ReadValue("/bookstore/book[@genre=\"novel\"]/author/last-name");
      // ReadValue("/bookstore/book/@genre");

      /// <summary>
      /// wählt den/die Knoten aus oder löst eine Exception aus
      /// </summary>
      /// <param name="xpath"></param>
      /// <returns></returns>
      XPathNodeIterator NavigatorSelect(string xpath) {
         return NsMng != null ?
                     navigator.Select(xpath, NsMng) :
                     navigator.Select(xpath);
      }

      /// <summary>
      /// Test, ob ein Navigator ex.
      /// </summary>
      void CheckNavigator() {
         if (navigator == null)
            throw new Exception("Es muss erst LoadData() oder CreateInternData() aufgerufen werden!");
      }

      /// <summary>
      /// Existiert der XPath?
      /// </summary>
      /// <param name="xpath">XPath</param>
      /// <returns></returns>
      public bool ExistXPath(string xpath) {
         CheckNavigator();
         try {
            return NavigatorSelect(xpath).Count > 0;
         } catch { }
         return false;
      }

      /// <summary>
      /// liefert die Anzahl der Knoten, die auf 'xpath' passen
      /// </summary>
      /// <param name="xpath"></param>
      /// <returns></returns>
      public int NodeCount(string xpath) {
         return NavigatorSelect(xpath).Count;
      }

      /// <summary>
      /// liefert die Daten entsprechend 'xpath' als Object-Array
      /// </summary>
      /// <param name="xpath"></param>
      /// <returns></returns>
      public object[] ReadValueAsObject(string xpath) {
         ReadNewerFile();
         CheckNavigator();
         object[] ret = null;
         try {
            XPathNodeIterator nodes = NavigatorSelect(xpath);
            if (nodes.Count == 0)
               return null;
            ret = new object[nodes.Count];
            int i = 0;
            while (nodes.MoveNext())
               ret[i++] = nodes.Current.TypedValue;
         } catch { }
         return ret;
      }

      /// <summary>
      /// liefert die Daten entsprechend 'xpath'; es muss genau 1 passender Knoten existieren, sonst wird 'defvalue' geliefert
      /// </summary>
      /// <param name="path">XPath</param>
      /// <param name="defvalue">vordefinierter Wert</param>
      /// <returns></returns>
      public string ReadValue(string xpath, string defvalue) {
         object[] o = ReadValueAsObject(xpath);
         if (o == null || o.Length != 1)
            return defvalue;
         return o[0].ToString();
      }
      /// <summary>
      /// liefert die Daten entsprechend 'xpath'; es muss genau 1 passender Knoten existieren, sonst wird 'defvalue' geliefert
      /// </summary>
      /// <param name="path">XPath</param>
      /// <param name="defvalue">vordefinierter Wert</param>
      /// <returns></returns>
      public bool ReadValue(string xpath, bool defvalue) {
         bool ret = defvalue;
         object[] o = ReadValueAsObject(xpath);
         if (o == null || o.Length != 1)
            return ret;
         try {
            ret = Convert.ToBoolean(o[0]);            // 'true' / 'false'
         } catch {
            try {
               ret = Convert.ToDouble(o[0]) != 0.0;   // alle Zahlen != 0 als true
            } catch { }
         }
         return ret;
      }
      /// <summary>
      /// liefert die Daten entsprechend 'xpath'; es muss genau 1 passender Knoten existieren, sonst wird 'defvalue' geliefert
      /// </summary>
      /// <param name="path">XPath</param>
      /// <param name="defvalue">vordefinierter Wert</param>
      /// <returns></returns>
      public int ReadValue(string xpath, int defvalue) {
         int ret = defvalue;
         object[] o = ReadValueAsObject(xpath);
         if (o == null || o.Length != 1)
            return ret;
         try {
            ret = Convert.ToInt32(o[0]);
         } catch { }
         return ret;
      }
      /// <summary>
      /// liefert die Daten entsprechend 'xpath'; es muss genau 1 passender Knoten existieren, sonst wird 'defvalue' geliefert
      /// </summary>
      /// <param name="path">XPath</param>
      /// <param name="defvalue">vordefinierter Wert</param>
      /// <returns></returns>
      public uint ReadValue(string xpath, uint defvalue) {
         uint ret = defvalue;
         object[] o = ReadValueAsObject(xpath);
         if (o == null || o.Length != 1)
            return ret;
         try {
            ret = Convert.ToUInt32(o[0]);
         } catch { }
         return ret;
      }
      /// <summary>
      /// liefert die Daten entsprechend 'xpath'; es muss genau 1 passender Knoten existieren, sonst wird 'defvalue' geliefert
      /// </summary>
      /// <param name="path">XPath</param>
      /// <param name="defvalue">vordefinierter Wert</param>
      /// <returns></returns>
      public double ReadValue(string xpath, double defvalue) {
         double ret = defvalue;
         object[] o = ReadValueAsObject(xpath);
         if (o == null || o.Length != 1)
            return ret;
         try {
            if (o[0].GetType() == Type.GetType("System.String"))     // Mono erkennt z.Z. NICHT den Typ Double --> Umwandlung aus String
               ret = Convert.ToDouble(o[0], System.Globalization.CultureInfo.GetCultureInfo("en-US"));
            else
               ret = Convert.ToDouble(o[0]);
         } catch { }
         return ret;
      }

      /// <summary>
      /// liefert ein Datenarray entsprechend 'xpath'
      /// </summary>
      /// <param name="path">XPath</param>
      /// <returns></returns>
      public string[] ReadString(string xpath) {
         string[] ret = null;
         object[] o = ReadValueAsObject(xpath);
         if (o != null) {
            ret = new string[o.Length];
            for (int i = 0; i < o.Length; i++)
               ret[i] = o[i].ToString();
         }
         return ret;
      }
      /// <summary>
      /// liefert ein Datenarray entsprechend 'xpath'
      /// </summary>
      /// <param name="path">XPath</param>
      /// <returns></returns>
      public int[] ReadInt(string xpath, int defvalue) {
         int[] ret = null;
         object[] o = ReadValueAsObject(xpath);
         if (o != null) {
            ret = new int[o.Length];
            for (int i = 0; i < o.Length; i++)
               try {
                  ret[i] = Convert.ToInt32(o[i]);
               } catch {
                  ret[i] = defvalue;
               }
         }
         return ret;
      }
      /// <summary>
      /// liefert ein Datenarray entsprechend 'xpath'
      /// </summary>
      /// <param name="path">XPath</param>
      /// <returns></returns>
      public uint[] ReadUInt(string xpath, uint defvalue) {
         uint[] ret = null;
         object[] o = ReadValueAsObject(xpath);
         if (o != null) {
            ret = new uint[o.Length];
            for (int i = 0; i < o.Length; i++)
               try {
                  ret[i] = Convert.ToUInt32(o[i]);
               } catch {
                  ret[i] = defvalue;
               }
         }
         return ret;
      }
      /// <summary>
      /// liefert ein Datenarray entsprechend 'xpath'
      /// </summary>
      /// <param name="path">XPath</param>
      /// <returns></returns>
      public bool[] ReadBool(string xpath, bool defvalue) {
         bool[] ret = null;
         object[] o = ReadValueAsObject(xpath);
         if (o != null) {
            ret = new bool[o.Length];
            for (int i = 0; i < o.Length; i++)
               try {
                  ret[i] = Convert.ToBoolean(o[i]);
               } catch {
                  ret[i] = defvalue;
               }
         }
         return ret;
      }
      /// <summary>
      /// liefert ein Datenarray entsprechend 'xpath'
      /// </summary>
      /// <param name="path">XPath</param>
      /// <returns></returns>
      public double[] ReadDouble(string xpath, double defvalue) {
         double[] ret = null;
         object[] o = ReadValueAsObject(xpath);
         if (o != null) {
            ret = new double[o.Length];
            for (int i = 0; i < o.Length; i++)
               try {
                  ret[i] = Convert.ToDouble(o[i]);
               } catch {
                  ret[i] = defvalue;
               }
         }
         return ret;
      }

      /// <summary>
      /// liefert alle Attribut-Wert-Paare für jeden per xpath adressierten Knoten
      /// <para>Das jeweilige Dictionary ist leer, wenn keine Attribute ex.</para>
      /// </summary>
      /// <param name="xpath"></param>
      /// <returns></returns>
      public List<Dictionary<string, string>> ReadAttributes(string xpath) {
         List<Dictionary<string, string>> attr = null;
         ReadNewerFile();
         CheckNavigator();
         try {
            XPathNodeIterator nodes = NavigatorSelect(xpath);
            if (nodes != null && nodes.Count > 0) {
               attr = new List<Dictionary<string, string>>();
               while (nodes.MoveNext()) {
                  attr.Add(new Dictionary<string, string>());
                  navigator.MoveTo(nodes.Current);
                  if (navigator.MoveToFirstAttribute()) {         // es gibt Attribute
                     attr[attr.Count - 1].Add(navigator.Name, navigator.Value);
                     while (navigator.MoveToNextAttribute())
                        attr[attr.Count - 1].Add(navigator.Name, navigator.Value);
                  }
               }
            }
         } catch { }
         return attr;
      }

      /// <summary>
      /// liefert, wenn der XPath eindeutig ist, den gesamten XML-Text, sonst null
      /// </summary>
      /// <param name="xpath"></param>
      /// <returns></returns>
      public string GetXmlText(string xpath) {
         CheckNavigator();
         int count = 0;
         XPathNodeIterator it = NavigatorSelect(xpath);
         count = it.Count;
         if (count == 1) {
            it.MoveNext();
            XPathNavigator nav = it.Current;
            return nav.OuterXml;
         }
         return null;
      }

      /// <summary>
      /// Positionen für <see cref="InsertXmlText"/>
      /// </summary>
      public enum InsertPosition {
         /// <summary>
         /// nebengeordnet, davor
         /// </summary>
         Before,
         /// <summary>
         /// nebengeordnet, danach
         /// </summary>
         After,
         /// <summary>
         /// untergeordnet, als 1.
         /// </summary>
         PrependChild,
         /// <summary>
         /// untergeordnet, als letzter
         /// </summary>
         AppendChild,
      }

      /// <summary>
      /// fügt den XML-Text am XPath entsprechend der Position ein; fkt. NICHT für zusätzliche Attribute
      /// </summary>
      /// <param name="xpath"></param>
      /// <param name="xml"></param>
      /// <param name="pos">Einfügeposition bzgl. des Pfades</param>
      /// <returns>0 wenn der XPath nicht gefunden wurde, 1 wenn ok, sonst nur Anzahl der (nicht eindeutigen) Einfügepositionen</returns>
      public int InsertXmlText(string xpath, string xml, InsertPosition pos) {
         CheckNavigator();
         int count = 0;
         XPathNodeIterator it = NavigatorSelect(xpath);
         count = it.Count;
         if (count == 1) {
            it.MoveNext();
            XPathNavigator nav = it.Current;
            switch (pos) {
               case InsertPosition.Before:
                  nav.InsertBefore(xml);
                  break;
               case InsertPosition.After:
                  nav.InsertAfter(xml);
                  break;
               case InsertPosition.PrependChild:
                  nav.PrependChild(xml);
                  break;
               case InsertPosition.AppendChild:
                  nav.AppendChild(xml);
                  break;
            }
         }
         return count;
      }

#if !HIDE_OBSOLETE

      /// <summary>
      /// liefert die Daten entsprechend 'xpath' als Zeichenketten-Array
      /// </summary>
      /// <param name="path">XPath</param>
      /// <returns></returns>
      [Obsolete("besser: ReadString()")]
      public string[] ReadValue(string xpath) {
         object[] o = ReadValueAsObject(xpath);
         if (o == null)
            return null;
         string[] ret = new string[o.Length];
         for (int i = 0; i < o.Length; i++)
            ret[i] = o[i].ToString();
         return ret;
      }

      /// <summary>
      /// einen Knoten (ev. mit Attribut) am XPath-Ziel (als letztes Child) anfügen
      /// </summary>
      /// <param name="path">XPath</param>
      /// <param name="nodename">Name des neuen untergeordneten Knoten</param>
      /// <param name="nodevalue">Wert des Knotens</param>
      /// <param name="attribute">Attributname des Knotens</param>
      /// <param name="avalue">Wert der Attribute des Knotens</param>
      /// <returns>true, wenn erfolgreich</returns>
      [Obsolete("besser: Append()")]
      public bool AppendNodeOnXpath(string xpath, string nodename, string nodevalue = null, string attribute = null, string avalue = null) {
         return AppendNodeOnXpath(xpath,
                                  nodename,
                                  nodevalue,
                                  attribute != null ? new string[] { attribute } : null,
                                  avalue != null ? new string[] { avalue } : null);
      }
      /// <summary>
      /// einen Knoten mit mehreren Attributen am XPath-Ziel (als letztes Child) anfügen
      /// </summary>
      /// <param name="path">XPath</param>
      /// <param name="nodename">Name des neuen untergeordneten Knoten; wenn null oder leer, werden die Attribute an xpath angehängt</param>
      /// <param name="nodevalue">Wert des Knotens</param>
      /// <param name="attribute">Attributnamen des Knotens</param>
      /// <param name="avalue">Werte der Attribute des Knotens</param>
      /// <returns>true, wenn erfolgreich</returns>
      [Obsolete("besser: Append()")]
      public bool AppendNodeOnXpath(string xpath, string nodename, string nodevalue, string[] attribute, string[] avalue = null) {
         CheckNavigator();
         if (navigator.CanEdit) {
            XPathNodeIterator nodes = NavigatorSelect(xpath);     // auf xpath positionieren
            if (nodes != null &&
                nodes.Count == 0)                                 // i.A. sollte es genau 1 Knoten als Ergebnismenge sein
               return false;
            while (nodes.MoveNext()) {
               navigator.MoveTo(nodes.Current);                   // auf einen Knoten positionieren, der xpath entspricht
               if (!string.IsNullOrEmpty(nodename)) {
                  navigator.AppendChildElement(navigator.Prefix, nodename, navigator.LookupNamespace(navigator.Prefix), nodevalue);
                  if (attribute != null) {
                     navigator.MoveToFirstChild();
                     while (navigator.MoveToNext(XPathNodeType.Element)) ;
                     appendattribut(attribute, avalue);
                  }
               } else
                  appendattribut(attribute, avalue);
            }
            return true;
         }
         return false;
      }

      /// <summary>
      /// neues Attribut am XPath-Ziel hinzufügen
      /// </summary>
      /// <param name="path">XPath</param>
      /// <param name="attribute">Attributname</param>
      /// <param name="avalue">Wert des Attributs</param>
      /// <returns></returns>
      [Obsolete("besser: Append()")]
      public bool AppendAttributeOnXpath(string xpath, string attribute, string avalue = null) {
         CheckNavigator();
         if (navigator.CanEdit) {
            XPathNodeIterator nodes = NavigatorSelect(xpath);
            if (nodes != null &&
                nodes.Count == 0)
               return false;
            while (nodes.MoveNext()) {
               navigator.MoveTo(nodes.Current);
               string[] attr = { attribute };
               string[] aval = { avalue };
               appendattribut(attr, aval);
            }
            return true;
         }
         return false;
      }
      /// <summary>
      /// neue Attribute am XPath-Ziel hinzufügen
      /// </summary>
      /// <param name="path">XPath</param>
      /// <param name="attribute">Attributnamen</param>
      /// <param name="avalue">Werte des Attributs</param>
      /// <returns></returns>
      [Obsolete("besser: Append()")]
      public bool AppendAttributeOnXpath(string xpath, string[] attribute = null, string[] avalue = null) {
         CheckNavigator();
         if (navigator.CanEdit) {
            XPathNodeIterator nodes = NavigatorSelect(xpath);
            if (nodes != null &&
                nodes.Count == 0)
               return false;
            while (nodes.MoveNext()) {
               navigator.MoveTo(nodes.Current);
               appendattribut(attribute, avalue);
            }
            return true;
         }
         return false;
      }

      /// <summary>
      /// die Attribute werden an der akt. Navigatorposition angefügt
      /// </summary>
      /// <param name="attribute"></param>
      /// <param name="avalue"></param>
      [Obsolete("besser: appendattribut(Dictionary)")]
      void appendattribut(string[] attribute, string[] avalue) {
         if (attribute != null) {
            string prefix = navigator.Prefix;
            string ns = navigator.LookupNamespace(prefix);
            for (int i = 0; i < attribute.Length; i++)
               if (!string.IsNullOrEmpty(attribute[i]))
                  navigator.CreateAttribute(prefix,
                                            attribute[i],
                                            ns,
                                            avalue != null && avalue.Length > i ? avalue[i] : null);
         }
      }

#endif

      /// <summary>
      /// An den XPATH wird ein neuer Knoten mit mehreren Attributen als letzter Knoten der Liste angefügt. Ist kein Knotenname angegeben, werden
      /// die Attribute an den XPATH selbst angefügt. Schon vorhandene Attribute werden aktualisiert.
      /// </summary>
      /// <param name="xpath">XPATH für den neuen Knoten mit Attributen oder XPATH für neue Attribute</param>
      /// <param name="nodename">Name des neuen Knotens oder null</param>
      /// <param name="nodevalue">Wert des neuen Knotens oder null</param>
      /// <param name="attributes">Attribut-Werte-Paare (null-Werte werden als leere Zeichenkette interpretiert)</param>
      /// <param name="unique">wenn true, wird ein ev. schon mit dem Namen existierender Knoten verwendet (also ist der Knotename eindeutig)</param>
      /// <returns>false, wenn nichts angefügt werden konnte (weil z.B. der xpath nicht existiert)</returns>
      public bool Append(string xpath, string nodename, string nodevalue = null, Dictionary<string, string> attributes = null, bool unique = false) {
         CheckNavigator();
         if (navigator.CanEdit) {
            XPathNodeIterator nodes = NavigatorSelect(xpath);     // auf xpath positionieren
            if (nodes != null &&
                nodes.Count == 0)                                 // i.A. sollte es genau 1 Knoten als Ergebnismenge sein
               return false;
            while (nodes.MoveNext()) {
               navigator.MoveTo(nodes.Current);                   // auf einen Knoten positionieren, der xpath entspricht
               if (!string.IsNullOrEmpty(nodename)) {
                  XPathNavigator testnavi = null;
                  if (unique)
                     testnavi = navigator.SelectSingleNode(nodename);

                  if (testnavi == null) {                         // Knoten wird auf jeden Fall angehängt
                     navigator.AppendChildElement(navigator.Prefix,
                                                  nodename,
                                                  navigator.LookupNamespace(navigator.Prefix),
                                                  nodevalue);
                     if (attributes != null) {
                        navigator.MoveToFirstChild();
                        while (navigator.MoveToNext(XPathNodeType.Element)) ;    // Simulation: MoveToLastChild()
                        appendattribut(attributes);
                     }
                  } else {                                        // alter Knoten ex. und soll verwendet werden
                     testnavi.SetValue(nodevalue);
                     appendattribut(attributes, testnavi);
                  }
               } else
                  appendattribut(attributes);                     // nur Attribute am xpath anfügen/ändern
            }
            return true;
         }
         return false;
      }

      /// <summary>
      /// Zum XPATH wird nebengeordnet ein neuer Knoten davor oder danach eingefügt.
      /// </summary>
      /// <param name="xpath">XPATH für den neuen Knoten mit Attributen oder XPATH für neue Attribute</param>
      /// <param name="nodename">Name des neuen Knotens oder null</param>
      /// <param name="nodevalue">Wert des neuen Knotens oder null</param>
      /// <param name="attributes">Attribut-Werte-Paare (null-Werte werden als leere Zeichenkette interpretiert)</param>
      /// <param name="afterxpath">bei true wird der neue Knoten danach eingefügt, sonst davor</param>
      /// <returns></returns>
      public bool InsertNode(string xpath4node, string nodename, string nodevalue = null, Dictionary<string, string> attributes = null, bool afterxpath = true) {
         CheckNavigator();
         if (navigator.CanEdit) {
            XPathNodeIterator it = NavigatorSelect(xpath4node);     // auf xpath positionieren
            if (it != null &&
                it.Count == 0)                                 // i.A. sollte es genau 1 Knoten als Ergebnismenge sein
               return false;
            while (it.MoveNext()) {
               navigator.MoveTo(it.Current);                   // auf einen Knoten positionieren, der xpath entspricht

               if (afterxpath) {
                  navigator.InsertElementAfter(navigator.Prefix,
                                               nodename,
                                               navigator.LookupNamespace(navigator.Prefix),
                                               nodevalue);
                  navigator.MoveToNext();
               } else {
                  navigator.InsertElementBefore(navigator.Prefix,
                                                nodename,
                                                navigator.LookupNamespace(navigator.Prefix),
                                                nodevalue);
                  navigator.MoveToPrevious();
               }
               appendattribut(attributes);
            }
            return true;
         }
         return false;
      }

      /// <summary>
      /// An der mit XPATH bezeichneten (eindeutigen!) Position wird der Node (mit allen Childs) angefügt.
      /// </summary>
      /// <param name="xpath"></param>
      /// <param name="node"></param>
      /// <param name="pos"></param>
      /// <returns></returns>
      public bool InsertNode(string xpath, XmlNode node, InsertPosition pos) {
         CheckNavigator();
         int count = 0;
         XPathNodeIterator it = NavigatorSelect(xpath);
         count = it.Count;
         if (count == 1) {
            it.MoveNext();
            XPathNavigator nav = it.Current;
            switch (pos) {
               case InsertPosition.Before:
                  nav.InsertBefore(node.CreateNavigator());
                  break;
               case InsertPosition.After:
                  nav.InsertAfter(node.CreateNavigator());
                  break;
               case InsertPosition.PrependChild:
                  nav.PrependChild(node.CreateNavigator());
                  break;
               case InsertPosition.AppendChild:
                  nav.AppendChild(node.CreateNavigator());
                  break;
            }
            return true;
         }
         return false;
      }

      /// <summary>
      /// die Attribute werden an der akt. Navigatorposition angefügt bzw. aktualisiert
      /// </summary>
      /// <param name="attributes">Attribut-Werte-Paare (null-Werte werden als leere Zeichenkette interpretiert)</param>
      /// <param name="parentnavi">Navigator, an dem die Attribute eingefügt werden sollen oder null für den internen Standardnavigator</param>
      void appendattribut(Dictionary<string, string> attributes, XPathNavigator parentnavi = null) {
         if (attributes != null) {
            if (parentnavi == null)
               parentnavi = navigator;
            string prefix = parentnavi.Prefix;
            string ns = parentnavi.LookupNamespace(prefix);
            foreach (var item in attributes) {
               if (!string.IsNullOrEmpty(item.Key)) {
                  XPathNavigator testnavi = parentnavi.SelectSingleNode("@" + item.Key);
                  if (testnavi == null)     // Attr. ex. noch nicht
                     parentnavi.CreateAttribute(prefix,
                                               item.Key,
                                               ns,
                                               !string.IsNullOrEmpty(item.Value) ? item.Value : "");
                  else
                     testnavi.SetValue(!string.IsNullOrEmpty(item.Value) ? item.Value : "");
               }
            }
         }
      }

      /// <summary>
      /// löscht alle Nodes und deren untergeordnete Nodes auf die der 'xpath' passt
      /// </summary>
      /// <param name="path"></param>
      /// <returns>false, wenn nichts gelöscht wurde</returns>
      public bool Remove(string xpath) {
         CheckNavigator();
         if (navigator.CanEdit) {
            XPathNodeIterator it = NavigatorSelect(xpath);
            if (it.Count == 0)
               return false;
            XmlNode[] node2del = new XmlNode[it.Count];
            XmlNode[] nodeparent2del = new XmlNode[it.Count];
            int n = 0;
            while (it.MoveNext()) {
               if (it.Current is IHasXmlNode) {
                  node2del[n] = ((IHasXmlNode)it.Current).GetNode();
                  if (node2del[n].NodeType == XmlNodeType.Attribute) {     // Dann wird der Parent explizit benötigt, da er in XmlAttribute null ist!
                     XPathNavigator nodesNavigatorParent = it.Current.Clone();
                     nodesNavigatorParent.MoveToParent();
                     nodeparent2del[n] = ((IHasXmlNode)nodesNavigatorParent).GetNode();
                  }
                  n++;
               }
            }

            navigator.MoveToRoot();

            // jetzt löschen
            for (int i = 0; i < node2del.Length; i++)
               if (node2del[i] != null) {
                  if (node2del[i].NodeType == XmlNodeType.Attribute)
                     nodeparent2del[i].Attributes.Remove((XmlAttribute)(node2del[i]));
                  else {
                     if (node2del[i].ParentNode != null)                // kann nur bei der Root sein
                        node2del[i].ParentNode.RemoveChild(node2del[i]);
                  }
               }
            return true;
         }
         return false;
      }

      /// <summary>
      /// liefert, wenn der XPath eindeutig ist, den Knoten und entfernt ihn (mit allen Childs) aus dem Dokument
      /// </summary>
      /// <param name="xpath"></param>
      /// <returns></returns>
      public XmlNode UnlinkUniqueNode(string xpath) {
         CheckNavigator();
         int count = 0;
         XPathNodeIterator it = NavigatorSelect(xpath);
         count = it.Count;
         if (count == 1) {
            it.MoveNext();
            XmlNode parent = ((IHasXmlNode)it.Current).GetNode();
            if (parent.ParentNode != null) {
               return parent.ParentNode.RemoveChild(parent);
            } else {
               it.Current.MoveToParent();
               parent = ((IHasXmlNode)it).GetNode();
               return parent.ParentNode.RemoveChild(parent);
            }
         }
         return null;
      }

      /// <summary>
      /// Knoten oder Attribute ändern auf die der 'xpath' passt
      /// </summary>
      /// <param name="path">XPath</param>
      /// <param name="value">neuer Wert</param>
      /// <returns>true, wenn der Wert geändert wurde</returns>
      public bool Change(string xpath, string value) {
         CheckNavigator();
         if (navigator.CanEdit) {
            XPathNodeIterator nodes = NavigatorSelect(xpath);
            if (nodes != null &&
                nodes.Count == 0)
               return false;
            while (nodes.MoveNext()) {
               navigator.MoveTo(nodes.Current);
               navigator.SetValue(value);
            }
            return true;
         }
         return false;
      }

      /* Problem:
       * XPATH mag keine (') und (") innerhalb der Argumente. Wenn ein (') auftaucht könnte man die Zeichenketten in (") einschließen
       * bzw. wenn (") enthalten ist können die Zeichenkette in (') eingeschlossen werden. Problematisch wird es, wenn sowohl (') als
       * auch (") in einer Zeichenkette auftauchen können.
       * Dann kann man die Funktion concate() verwenden. Die Originalzeichenkette wird zerlegt, so dass die enthaltenen (') und (") jeweils
       * einzeln behandelt werden. (') werden in (") und (") in (') eingeschlossen. Aufwendig, aber wohl die einzige Möglichkeit.
       * ACHTUNG!
       * Sonderzeichen (&) usw. müssen NICHT umgewandelt werden.
       */
      public static string GenerateConcatForXPath(string sXPathQueryString) {
         string returnString = string.Empty;
         string searchString = sXPathQueryString;
         char[] quoteChars = new char[] { '\'', '"' };

         int quotePos = searchString.IndexOfAny(quoteChars);
         if (quotePos == -1)
            returnString = "'" + searchString + "'";
         else {
            returnString = "concat(";
            while (quotePos != -1) {
               string subString = searchString.Substring(0, quotePos);
               returnString += "'" + subString + "', ";
               if (searchString.Substring(quotePos, 1) == "'")
                  returnString += "\"'\", ";
               else
                  returnString += "'\"', ";
               searchString = searchString.Substring(quotePos + 1, searchString.Length - quotePos - 1);
               quotePos = searchString.IndexOfAny(quoteChars);
            }
            returnString += "'" + searchString + "')";
         }
         return returnString;
      }

      #endregion

      #region Funktionen zum Erzeugen und Testen eines gesamten durch ein XmlElement-Array definierten Pfades

      /// <summary>
      /// erzeugt ein Knotenelement mit Attributen (für CreateAbsolutePath())
      /// </summary>
      /// <param name="nodename">Knotenname</param>
      /// <param name="nodevalue">Knotenwert</param>
      /// <param name="attribut">Attribute</param>
      /// <param name="avalue">Attributwerte</param>
      /// <returns></returns>
      public XmlElement CreateNodeElement(string nodename, string nodevalue = null, string[] attribut = null, string[] avalue = null) {
         XmlElement newElem = CreateElement(nodename, NamespaceURI);
         if (nodevalue != null)
            newElem.InnerText = nodevalue;
         if (attribut != null) {
            for (int i = 0; i < attribut.Length; i++)
               if (!string.IsNullOrEmpty(attribut[i]) &&
                   avalue[i] != null)
                  newElem.SetAttribute(attribut[i], avalue[i]);
         }
#if DEBUG && !NO_DEBUGOUTPUT
         Console.WriteLine("CreateNodeElement: " + GetXmlElement4Debug(newElem));
#endif
         return newElem;
      }

      /// <summary>
      /// erzeugt einen vollständigen Pfad mit den gewünschten Knoten, falls der Pfad noch nicht existiert, oder testet nur die Existenz
      /// </summary>
      /// <param name="nodepath">Beschreibung der Knoten (unterhalb (!) der Root); wird intern geklont; ein leerer Value bleibt unberücksichtigt</param>
      /// <param name="depth">Anzahl der gültigen Elemente in nodepath (darf kleiner sein als die Arraylänge)</param>
      /// <param name="create">true, wenn der Knoten auch erzeugt werden soll</param>
      /// <param name="no">Nummer des Knotens mit der Beschreibung (es kann mehrere passende Knoten geben!; 1 für den 1.)</param>
      /// <returns></returns>
      public XmlElement CreateOrTestAbsolutePath(XmlElement[] nodepath, int depth = -1, bool create = true, int no = 1) {
         XmlNode node = DocumentElement;
         XmlElement childnode = null;
         if (depth < 0)
            depth = nodepath.Length;
         for (int d = 0; d < depth; d++) {
            bool found = false;
            foreach (XmlNode testnode in node) {               // alle Childs testen
               if (testnode.NodeType == XmlNodeType.Element)
                  if (testnode.Name == nodepath[d].Name) {                                   // Stimmt der Knotenname überein?
                     if (d < depth - 1 ||                                                    // Knotenwert nur beim letzten Knoten interessant
                         nodepath[d].InnerText.Length == 0 ||                                // kein Knotenwert vorgegeben
                         nodepath[d].InnerText == testnode.InnerText) {                      // Knotenwert gleich
                        found = true;
                        // alle Knotenattribute vergleichen
                        foreach (XmlAttribute a in nodepath[d].Attributes) {
                           if (!(((XmlElement)testnode).HasAttribute(a.Name) &&              // nicht existierendes 
                                 ((XmlElement)testnode).GetAttribute(a.Name) == a.Value)) {  // oder nicht übereinstimmendes Attribut gefunden
                              found = false;
                              break;
                           }
                        }
                        if (found) {
                           if (--no <= 0) {
                              node = testnode;
                              childnode = (XmlElement)node;
                              break;
                           }
                           found = false;
                        }
                     }
                  }
            }
            if (!found) {                          // nicht vorhanden, also ev. erzeugen
               if (!create) {
#if DEBUG && !NO_DEBUGOUTPUT
                  Console.WriteLine("CreateOrTestAbsolutePath: NICHT gefunden und NICHT erzeugt");
#endif
                  return null;           // dann nur zum Test
               }
               XmlNode newnode = nodepath[d].CloneNode(true);
               node.AppendChild(newnode);
               node = newnode;
#if DEBUG && !NO_DEBUGOUTPUT
               Console.WriteLine("CreateOrTestAbsolutePath: NICHT gefunden aber erzeugt");
#endif
            }
         }
#if DEBUG && !NO_DEBUGOUTPUT
         Console.WriteLine("CreateOrTestAbsolutePath: " + GetXmlElement4Debug((XmlElement)node));
#endif
         return (XmlElement)node;                  // tiefstes Element zurückliefern
      }

      /// <summary>
      /// Testet, ob der Pfad mit den gewünschten Knoten existiert; wenn nicht wird null geliefert
      /// </summary>
      /// <param name="nodepath">Beschreibung der Knoten (unterhalb (!) der Root); wird intern geklont; ein leerer Value bleibt unberücksichtigt</param>
      /// <param name="depth">Anzahl der gültigen Elemente in nodepath (darf kleiner sein als die Arraylänge)</param>
      public XmlElement ExistAbsolutePath(XmlElement[] nodepath, int depth = -1) {
         return CreateOrTestAbsolutePath(nodepath, depth >= 0 ? depth : nodepath.Length, false);
      }

      #endregion

      #region Hilfsfunktionen für das Debugging
#if DEBUG

      /// <summary>
      /// zur vereinfachten Darstellung beim Debugging
      /// </summary>
      /// <param name="nodepath"></param>
      /// <returns></returns>
      protected string GetNodepath4Debug(XmlElement[] nodepath) {
         if (nodepath == null || nodepath.Length == 0)
            return "";
         string[] elemtxt = new string[nodepath.Length];
         for (int i = 0; i < nodepath.Length; i++)
            elemtxt[i] = GetXmlElement4Debug(nodepath[i]);
         return string.Join("/", elemtxt);
      }
      /// <summary>
      /// zur vereinfachten Darstellugn beim Debugging
      /// </summary>
      /// <param name="elem"></param>
      /// <returns></returns>
      protected string GetXmlElement4Debug(XmlElement elem) {
         string txt = "";
         if (elem != null) {
            txt = elem.Name + "[";
            foreach (XmlAttribute attr in elem.Attributes)
               txt += "{" + attr.Name + "=" + attr.Value + "}";
            txt += "]";
         }
         return txt;
      }
      /// <summary>
      /// zur vereinfachten Darstellugn beim Debugging
      /// </summary>
      /// <param name="elem"></param>
      /// <returns></returns>
      protected string GetXmlElementWithPath4Debug(XmlElement elem) {
         string txt = "";
         if (elem != null) {
            txt = GetXmlElement4Debug(elem);
            elem = (XmlElement)elem.ParentNode;
            while (elem != null)
               txt = GetXmlElement4Debug(elem) + "/" + txt;
         }
         return txt;
      }

#endif
      #endregion

      /// <summary>
      /// erzeugt ein Child-Element mit Attribut und Wert
      /// </summary>
      /// <param name="parent">Parent-Element</param>
      /// <param name="nodename">Name</param>
      /// <param name="nodeattribut">Attributname</param>
      /// <param name="nodevalue">Wert</param>
      /// <returns></returns>
      protected XmlElement AppendChildElement(XmlElement parent, string nodename, string nodeattribut, string nodevalue) {
         XmlElement newElem = CreateElement(nodename, parent.NamespaceURI);
         if (nodeattribut.Length > 0)
            newElem.SetAttribute(nodeattribut, nodevalue);
         parent.AppendChild(newElem);
#if DEBUG && !NO_DEBUGOUTPUT
         Console.WriteLine(string.Format("AppendChildElement: parent: {0}, newElem: {1}", GetXmlElementWithPath4Debug(parent), GetXmlElement4Debug(newElem)));
#endif
         return newElem;
      }

      /// <summary>
      /// erzeugt eine Kopie der aktuellen Instanz
      /// </summary>
      /// <returns>null bei Fehler</returns>
      public SimpleXmlDocument2 CloneSimpleXmlDocument() {
         SimpleXmlDocument2 doc = new SimpleXmlDocument2(XmlFilename, Rootname);
         doc.CreateInternData();
         doc.DocumentElement.InnerXml = DocumentElement.InnerXml;
         return doc;
      }

      /// <summary>
      /// liefert den Inhalt als formatierten Text
      /// </summary>
      public string AsFormattedText {
         get {
            System.Xml.XmlWriterSettings xmlWriterSettings = new System.Xml.XmlWriterSettings();
            xmlWriterSettings.Encoding = System.Text.Encoding.UTF8;
            xmlWriterSettings.Indent = true;
            xmlWriterSettings.IndentChars = " ";
            xmlWriterSettings.NewLineChars = "\r\n";
            xmlWriterSettings.NewLineHandling = System.Xml.NewLineHandling.Replace;
            xmlWriterSettings.NewLineOnAttributes = false;
            //xmlWriterSettings.OmitXmlDeclaration = true;
            xmlWriterSettings.ConformanceLevel = System.Xml.ConformanceLevel.Document;

            using (MemoryStream ms = new MemoryStream()) {
               using (System.Xml.XmlWriter writer = System.Xml.XmlWriter.Create(ms, xmlWriterSettings)) {
                  Save(writer);
                  writer.Flush();
                  writer.Close();
               }
               ms.Position = 0;
               using (StreamReader reader = new StreamReader(ms)) {
                  return reader.ReadToEnd();
               }
            }
         }
      }

      public override string ToString() {
         return string.Format("{0}, XSD: {1}, Root: {2}", XmlFilename, XsdFilename, Rootname);
      }

   }

}

