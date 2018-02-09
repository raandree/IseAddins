using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml.Serialization;
using System.Reflection;

namespace IseAddons
{
    #region FunctionDefinitions and FunctionContainer
    [Serializable]
    public class FunctionDefinition
    {
        public string FullPath { get; set; }

        public int LineNumber { get; set; }

        public string Name { get; set; }

        [XmlIgnore]
        public string FullName
        {
            get
            {
                return string.Format("{0} {1}", FullPath, Name);
            }
        }

        public FunctionDefinition(string fullPath, string name, int lineNumber)
        {
            this.FullPath = fullPath;
            this.Name = name;
            this.LineNumber = lineNumber;
        }

        public FunctionDefinition()
        { }

        public override string ToString()
        {
            return Name;
        }
    }

    public class FunctionContainer : DictionaryXmlStore<string, FunctionDefinition>
    {
        public FunctionDefinition GetFunctionByName(string Name)
        {
            return this.Where(f => f.Value.Name == Name).FirstOrDefault().Value;
        }

        public List<FunctionDefinition> GetFunctionsByName(string Name)
        {
            return this.Where(f => f.Value.Name == Name).Select(f => f.Value).ToList();
        }

        public List<FunctionDefinition> GetFunctionsByFile(string fullPath)
        {
            return this.Where(f => f.Value.FullPath == fullPath).Select(f => f.Value).ToList();
        }

        public void RemoveFunctionByFile(string fullPath)
        {
            var functions = GetFunctionsByFile(fullPath);

            functions.ForEach(f => this.Remove(f.FullName));
        }

        public void Add(FunctionDefinition f)
        {
            if (this.ContainsKey(f.FullName))
            {
                return;
            }
            this.Add(f.FullName, f);
        }

        public static new FunctionContainer Import(string path, string keyProperty)
        {
            var functions = new FunctionContainer();

            var import = DictionaryXmlStore<string, FunctionDefinition>.Import(path, keyProperty);

            import.ForEach(i => functions.Add(i.Value));

            return functions;
        }
    }

    [Serializable]
    public class DictionaryXmlStore<TKey, TValue> : Dictionary<TKey, TValue>
    {
        private string keyProperty;
        public string KeyProperty
        {
            get { return keyProperty; }
        }

        public void AddFromFile(string path)
        {
            var serializer = new XmlSerializer(typeof(List<TValue>));
            FileStream fileStream = new FileStream(path, FileMode.Open);

            var items = (List<TValue>)serializer.Deserialize(fileStream);

            fileStream.Close();

            foreach (var item in items)
            {
                TKey key = (TKey)item.GetType().GetProperty(keyProperty).GetValue(item, null);
                this.Add(key, item);
            }
        }

        public static DictionaryXmlStore<TKey, TValue> Import(string path, string keyProperty)
        {
            var serializer = new XmlSerializer(typeof(List<TValue>));
            FileStream fileStream = new FileStream(path, FileMode.Open);
            List<TValue> items;

            try
            {
                items = (List<TValue>)serializer.Deserialize(fileStream);
            }
            catch
            {
                items = new List<TValue>();
            }

            fileStream.Close();

            var dictionary = new DictionaryXmlStore<TKey, TValue>();
            foreach (var item in items)
            {
                TKey key = (TKey)item.GetType().GetProperty(keyProperty).GetValue(item, null);
                dictionary.Add(key, item);
            }

            dictionary.keyProperty = keyProperty;

            return dictionary;
        }

        public void Export(string path)
        {
            var functions = this.Values.ToList();

            var serializer = new XmlSerializer(functions.GetType());
            File.Delete(path);
            FileStream fileStream = new FileStream(path, FileMode.CreateNew);

            serializer.Serialize(fileStream, functions);

            fileStream.Close();
        }
    }

    [Serializable]
    public class ListXmlStore<T> : List<T>
    {
        public void AddFromFile(string path)
        {
            var serializer = new XmlSerializer(typeof(ListXmlStore<T>));
            FileStream fileStream = new FileStream(path, FileMode.Open);

            this.AddRange((ListXmlStore<T>)serializer.Deserialize(fileStream));

            fileStream.Close();
        }

        public static ListXmlStore<T> Import(string path)
        {
            var serializer = new XmlSerializer(typeof(ListXmlStore<T>));
            FileStream fileStream = new FileStream(path, FileMode.Open);

            var items = (ListXmlStore<T>)serializer.Deserialize(fileStream);

            fileStream.Close();

            return items;
        }

        public void Export(string path)
        {
            var serializer = new XmlSerializer(typeof(ListXmlStore<T>));
            File.Delete(path);
            FileStream fileStream = new FileStream(path, FileMode.CreateNew);

            serializer.Serialize(fileStream, this);

            fileStream.Close();
        }
    }
    #endregion

    #region AppFileManager
    public class AppFileManager
    {
        private string appName;
        private string appFilePath;
        private Dictionary<string, FileInfo> files = new Dictionary<string, FileInfo>();

        public string AppName
        {
            get { return appName; }
        }

        public AppFileManager(string appName)
        {
            this.appName = this.appName = appName;
            this.appFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appName);

            if (!Directory.Exists(appFilePath))
            {
                Directory.CreateDirectory(appFilePath);
            }
        }

        public AppFileManager()
            : this(Path.GetFileNameWithoutExtension(Assembly.GetCallingAssembly().ManifestModule.Name))
        { }

        public void Add(string name)
        {
            var fullName = Path.Combine(this.appFilePath, name);

            var file = new FileInfo(fullName);

            this.files.Add(name, file);
        }

        public void Remove(string name)
        {
            this.files.Remove(name);
        }

        public void Delete(string name, bool remove = false)
        {
            this.files[name].Delete();

            if (remove)
            {
                this.Remove(name);
            }
        }

        public FileInfo Get(string name)
        {
            return this.files[name];
        }
    }
    #endregion

    public class BreakPoint
    {
        public string ScriptFullName { get; set; }
        public int LineNumber { get; set; }
        public bool Enabled { get; set; }

        public BreakPoint()
        { }
    }
}