using Cottle;
using Cottle.Documents;
using Cottle.Settings;
using Cottle.Stores;
using Cottle.Values;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;
using Newtonsoft.Json.Linq;
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
            IStore store = new BuiltinStore();

            JObject content = JObject.Parse(json);

            foreach (JProperty property in content.Properties())
                store[property.Name] = TemplateHelper.Convert(property.Value);

            foreach (var pair in metadata)
                store[pair.Key] = pair.Value;

            return store;
        }

        private static Value Convert(JToken token)
        {
            if (token is JArray)
            {
                List<Value> array = new List<Value>();
                foreach(var element in (JArray)token)
                    array.Add(TemplateHelper.Convert(element));

                return array;
            }
            if (token is JObject)
            {
                Dictionary<Value, Value> dico = new Dictionary<Value, Value>();
                foreach (JProperty property in ((JObject)token).Properties())
                    dico[property.Name] = TemplateHelper.Convert(property.Value);

                return dico;
            }
            if (token is JValue)
            {
                JValue value = (JValue)token;
                switch (value.Type)
                {
                    case JTokenType.Array:
                        List<Value> array = new List<Value>();
                        foreach(var element in (JArray)token)
                            array.Add(TemplateHelper.Convert(element));

                    return array;

                    case JTokenType.Boolean:
                        return (bool)value.Value;

                    case JTokenType.Bytes:
                        List<Value> bytes = new List<Value>();
                        foreach (byte b in (byte[])value.Value)
                            bytes.Add(b);

                        return bytes;

                    case JTokenType.Comment:
                    case JTokenType.Property:
                    case JTokenType.Raw:
                    case JTokenType.String:
                        return (string)(value.Value);

                    case JTokenType.Date:
                        return ((DateTime)(value.Value)).ToString("yyyy-MM-ddTHH:mm:ss");

                    case JTokenType.Float:
                        return (double)(value.Value);

                    case JTokenType.Guid:
                        return ((Guid)(value.Value)).ToString();

                    case JTokenType.Integer:
                        return (long)(value.Value);

                    case JTokenType.None:
                    case JTokenType.Null:
                    case JTokenType.Undefined:
                        return VoidValue.Instance;

                    case JTokenType.Object:
                        Dictionary<Value, Value> dico = new Dictionary<Value, Value>();
                        foreach (JProperty property in ((JObject)token).Properties())
                            dico[property.Name] = TemplateHelper.Convert(property.Value);

                        return dico;

                    case JTokenType.TimeSpan:
                        return ((TimeSpan)(value.Value)).ToString();

                    case JTokenType.Uri:
                        return ((Uri)(value.Value)).ToString();
                    
                }
            }

            return null;
        }

        #region Types

        private delegate void RunTemplateHandler(string templatePath, string outputPath, params KeyValuePair<string, string>[] metadata);

        #endregion
    }
}
