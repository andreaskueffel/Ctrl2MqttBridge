using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace MqttBridge.Classes
{

    public class IniFile
    {
        const char commentChar = ';';
        
        const char separator = '=';
        
        const char sectionDelimiter = '.';
        string Filename { get; }
        public IniFile(string filename)
        {
            Filename = filename;
            if (!File.Exists(filename))
                File.WriteAllText(filename, "");
            var fileContent = File.ReadAllLines(filename);
            string actSection = "";
            foreach(var line in fileContent)
            {
                //Kommentar
                if (line.StartsWith(commentChar.ToString()))
                    continue;
                //Sektion
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    actSection = line.Substring(1, line.Length - 2);
                    continue;
                }
                //Wert
                if (line.Contains(separator))
                {
                    string key = actSection + sectionDelimiter + line.Split(separator)[0];
                    string value = line.Substring(line.IndexOf(separator) + 1);
                    IniContent.Add(key.ToLower(), value);
                }
            }
        }

        Dictionary<String, String> IniContent = new Dictionary<string, string>();

        public List<string> GetValues(string section)
        {
            List<string> ret = new List<string>();
            foreach(var kvp in IniContent)
            {
                if (kvp.Key.Split(sectionDelimiter)[0] == section)
                    ret.Add(kvp.Key.Substring(kvp.Key.IndexOf(sectionDelimiter) + 1) + separator + kvp.Value);
            }
            return ret;
        }

        public string GetValue(string key)
        {
            if (IniContent.ContainsKey(key.ToLower()))
                return IniContent[key.ToLower()];
            else
                return null;
        }
        /// <summary>
        /// Setzt einen Wert in der Ini-Datei. Existiert der Schlüssel nicht wird er angefügt.
        /// <br></br>
        /// Schlüssel immer im Format "Section.Key"
        /// </summary>
        /// <param name="key">Schlüssel im Format Section.Key</param>
        /// <param name="value">Wert als String</param>
        public void SetValue(string key, string value)
        {
            if (IniContent.ContainsKey(key.ToLower()))
                IniContent[key.ToLower()] = value;
            else
                IniContent.Add(key.ToLower(), value);
        }

        int GetIndexForKey(string section, string key, List<string> content)
        {
            section = section.ToLower();
            key = key.ToLower();
            string actSection = "";
            bool sectionFound = false;

            for(int i=0; i<content.Count; i++)
            {
                string line = content[i];
                //Leerzeile
                if (String.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                //Kommentar
                if (line.StartsWith(commentChar.ToString()))
                {
                    continue;
                }
                //Sektion
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    actSection = line.Substring(1, line.Length - 2).ToLower();

                    //Wenn Section change aber Schlüssel nicht vorhanden hier einfügen
                    if (actSection == section)
                    {
                        sectionFound = true;
                    }
                    continue;
                }
                //Wert
                if (sectionFound && line.Contains(separator))
                {

                    if (line.Split(separator)[0].ToLower() == key)
                        return i;
                }
            }
            return -1;
        }

        int GetIndexForSection(string section, List<string> content)
        {
            section = section.ToLower();

            for (int i = 0; i < content.Count; i++)
            {
                string line = content[i];
                //Leerzeile
                if (String.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                //Kommentar
                if (line.StartsWith(commentChar.ToString()))
                {
                    continue;
                }
                //Sektion
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    if (line.Substring(1, line.Length - 2).ToLower() == section)
                        return i;
                }
            }
            return -1;
        }

        public void Save()
        {
            var fileContent = File.ReadAllLines(Filename);
            List<string> newContent = new List<string>();
            for (int i = 0; i < fileContent.Length; i++)
            {
                newContent.Add(fileContent[i]);
            }

            foreach (var kvp in IniContent)
            {
                string section = kvp.Key.Split(sectionDelimiter)[0];
                string key = kvp.Key.Split(sectionDelimiter)[1];
                if (GetIndexForSection(section, newContent )==-1)
                {
                    //Section einfügen und Key
                    newContent.Add("[" + kvp.Key.Substring(0, kvp.Key.IndexOf(sectionDelimiter)).ToUpper() + "]");
                    newContent.Add(kvp.Key.Substring(kvp.Key.IndexOf(sectionDelimiter) + 1).ToUpper() + separator + kvp.Value);
                    continue;
                }
                int keyIndex = GetIndexForKey(section, key, newContent);
                if (keyIndex == -1)
                {
                    int index = GetIndexForSection(section, newContent);
                    newContent.Insert(index+1, kvp.Key.Substring(kvp.Key.IndexOf(sectionDelimiter) + 1).ToUpper() + separator + kvp.Value);
                    continue;
                }
                else
                {
                    newContent[keyIndex] = kvp.Key.Substring(kvp.Key.IndexOf(sectionDelimiter) + 1).ToUpper() + separator + kvp.Value;
                }

            }
            File.WriteAllLines(Filename, newContent);
        }
    }
}
