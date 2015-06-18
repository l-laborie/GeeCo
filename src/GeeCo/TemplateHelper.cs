using Cottle;
using Cottle.Documents;
using Cottle.Functions;
using Cottle.Settings;
using Cottle.Stores;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GeeCo
{
    public static class TemplateHelper
    {
        public static void RunTemplatedScript(string scriptPath, string jsonPath)
        {
            IDocument document;
            string json;
            string script;
            CustomSetting setting;
            IStore store;

            setting = new CustomSetting();
            setting.BlockBegin = "{{";
            setting.BlockContinue = "||";
            setting.BlockEnd = "}}";

            using (Stream stream = new FileStream(scriptPath, FileMode.Open))
            {
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                {
                    document = new SimpleDocument(reader, setting);
                }
            }

            using (Stream stream = new FileStream(jsonPath, FileMode.Open))
            {
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                {
                    json = reader.ReadToEnd();
                }
            }
            store = TemplateHelper.BuildStore(json);
            script = document.Render(store);

            using (V8ScriptEngine engine = new V8ScriptEngine())
            {
                engine.AddHostObject("RunTemplate", new RunTemplateHandler((t, o, m) => TemplateHelper.RunTemplate(t, o, json, m)));
                engine.AddHostObject("CopyFolder", new Action<string, string>((i, o) =>
                {
                    if (!Directory.Exists(i))
                        return;

                    if (!Directory.Exists(o))
                        Directory.CreateDirectory(o);

                    foreach (var file in Directory.GetFiles(i))
                        File.Copy(file, Path.Combine(o, Path.GetFileName(file)), true);
                }));
                engine.AddHostObject("BuildKeyValuePair", new Func<string, string, KeyValuePair<string, string>>((k, v) => new KeyValuePair<string, string>(k, v)));
                engine.AddHostObject("lib", new HostTypeCollection("mscorlib", "System.Core"));

                engine.Execute(script);
            }
        }

        public static void RunTemplate(string templatePath, string outputPath, string json, params KeyValuePair<string, string>[] metadata)
        {
            IDocument document;
            CustomSetting setting;
            IStore store;

            setting = new CustomSetting();
            setting.BlockBegin = "{{";
            setting.BlockContinue = "||";
            setting.BlockEnd = "}}";

            using(Stream stream = new FileStream(templatePath, FileMode.Open))
            {
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                {
                    document = new SimpleDocument(reader, setting);
                }
            }

            store = TemplateHelper.BuildStore(json, metadata);

            if (!Directory.GetParent(outputPath).Exists)
                Directory.GetParent(outputPath).Create();

            if (File.Exists(outputPath))
                File.Delete(outputPath);

            using (Stream stream = new FileStream(outputPath, FileMode.OpenOrCreate))
            {
                using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8))
                {
                    writer.Write(document.Render(store));
                }
            }
        }

        private static IStore BuildStore(string json, params KeyValuePair<string, string>[] metadata)
        {
            Global global = JsonConvert.DeserializeObject<Global>(json);

            IStore store = new BuiltinStore();

            global.Fill(store);

            foreach (var pair in metadata)
                store[pair.Key] = pair.Value;

            return store;
        }

        #region Types

        private delegate void RunTemplateHandler(string templatePath, string outputPath, params KeyValuePair<string, string>[] metadata);

        private struct Global
        {
            public Database database { get; set; }

            public void Fill(IStore store)
            {
                store["database"] = this.database.Store();
            }
        }

        private struct Database
        {
            public string name { get; set; }
            public string project_guid { get; set; }
            public string solution_guid { get; set; }

            public List<Table> tables { get; set; }

            public Value Store()
            {
                Dictionary<Value, Value> result = new Dictionary<Value, Value>();
                result["name"] = this.name;
                result["project_guid"] = this.project_guid;
                result["solution_guid"] = this.solution_guid;

                Dictionary<Value, Value> tables = new Dictionary<Value, Value>();
                foreach (var table in this.tables)
                {
                    tables[table.name] = table.Store();
                }
                result["tables"] = tables;

                return result;
            }
        }

        private struct Table
        {
            public string name { get; set; }
            public string clients { get; set; }
            public Prevision size { get; set; }
            public Prevision activity { get; set; }
            public string data_scope { get; set; }
            public bool replication { get; set; }
            public List<Column> columns { get; set; }
            public List<ForeignKey> foreign_keys { get; set; }
            public List<Index> indexes { get; set; }
            public List<ReverseForeignKeys> reverse_foreign_keys { get; set; }

            public Value Store()
            {
                Dictionary<Value, Value> result = new Dictionary<Value, Value>();
                result["name"] = this.name;
                result["clients"] = this.clients;
                result["size"] = this.size.Store();
                result["activity"] = this.activity.Store();
                result["data_scope"] = this.data_scope;
                result["replication"] = this.replication;

                Dictionary<Value, Value> columns = new Dictionary<Value, Value>();
                foreach (var column in this.columns)
                {
                    columns[column.name] = column.Store();
                }
                result["columns"] = columns;

                if (this.foreign_keys != null)
                {
                    Dictionary<Value, Value> foreign_keys = new Dictionary<Value, Value>();
                    foreach (var foreign_key in this.foreign_keys)
                    {
                        foreign_keys[foreign_key.name] = foreign_key.Store();
                    }
                    result["foreign_keys"] = foreign_keys;
                }

                if (this.indexes != null)
                {
                    Dictionary<Value, Value> indexes = new Dictionary<Value, Value>();
                    foreach (var index in this.indexes)
                    {
                        indexes[index.name] = index.Store();
                    }
                    result["indexes"] = indexes;
                }

                if (this.reverse_foreign_keys != null)
                {
                    List<Value> reverse_foreign_keys = new List<Value>();
                    foreach (var reverse_foreign_key in this.reverse_foreign_keys)
                    {
                        reverse_foreign_keys.Add(reverse_foreign_key.Store());
                    }
                    result["reverse_foreign_keys"] = reverse_foreign_keys;

                }
                return result;
            }
        }

        private struct Prevision
        {
            public string today { get; set; }
            public string three_month { get; set; }
            public string one_year { get; set; }
            public string three_years { get; set; }

            public Value Store()
            {
                Dictionary<Value, Value> result = new Dictionary<Value, Value>();
                result["today"] = this.today;
                result["three_month"] = this.three_month;
                result["one_year"] = this.one_year;
                result["three_years"] = this.three_years;

                return result;
            }
        }

        private struct Column
        {
            public string name { get; set; }
            public string type { get; set; }
            public bool nullable { get; set; }
            public bool identity { get; set; }
            public object exemple_value { get; set; }
            public bool partitioning { get; set; }
            public bool primary_key { get; set; }
            public string Default { get; set; }
            public bool insert_set_to_null { get; set; }
            public bool read_only { get; set; }
            public string update_auto { get; set; }

            public Value Store()
            {
                Dictionary<Value, Value> result = new Dictionary<Value, Value>();
                result["name"] = this.name;
                result["type"] = this.type;
                result["nullable"] = this.nullable;
                result["identity"] = this.identity;
                result["exemple_value"] = this.exemple_value == null? null : this.exemple_value.ToString();
                result["partitioning"] = this.partitioning;
                result["primary_key"] = this.primary_key;
                result["default"] = this.Default;
                result["insert_set_to_null"] = this.insert_set_to_null;
                result["read_only"] = this.read_only;
                result["update_auto"] = this.update_auto;

                return result;
            }
        }

        private struct ForeignKey
        {
            public string name { get; set; }
            public string table { get; set; }
            public List<ForeignKeyColumn> columns { get; set; }

            public Value Store()
            {
                Dictionary<Value, Value> result = new Dictionary<Value, Value>();
                result["name"] = this.name;
                result["table"] = this.table;

                List<Value> columns = new List<Value>();
                foreach (var column in this.columns)
                {
                    columns.Add(column.Store());
                }
                result["columns"] = columns;

                return result;
            }
        }

        private struct ForeignKeyColumn
        {
            public string local { get; set; }
            public string foreign { get; set; }

            public Value Store()
            {
                Dictionary<Value, Value> result = new Dictionary<Value, Value>();
                result["local"] = this.local;
                result["foreign"] = this.foreign;

                return result;
            }
        }

        private struct Index
        {
            public string name { get; set; }
            public List<string> columns { get; set; }

            public Value Store()
            {
                Dictionary<Value, Value> result = new Dictionary<Value, Value>();
                result["name"] = this.name;

                List<Value> columns = new List<Value>();
                foreach (var column in this.columns)
                {
                    columns.Add(column);
                }
                result["columns"] = columns;

                return result;
            }
        }

        private struct ReverseForeignKeys
        {
            public string table { get; set; }
            public List<ForeignKeyColumn> columns { get; set; }

            public Value Store()
            {
                Dictionary<Value, Value> result = new Dictionary<Value, Value>();
                result["table"] = this.table;

                Dictionary<Value, Value> columns = new Dictionary<Value, Value>();
                foreach (var column in this.columns)
                {
                    columns[column.local] = column.Store();
                }
                result["columns"] = columns;

                return result;
            }
        }

        #endregion
    }
}
