using Mono.Options;
using System;
using System.Collections.Generic;
using System.IO;

namespace GeeCo
{
    class Program
    {
        static void Main(string[] args)
        {
            OptionSet optionSet;
            Dictionary<string, string> dictionaryConfig;
            List<KeyValuePair<string, string>> metadata;

            optionSet = new OptionSet();
            dictionaryConfig = new Dictionary<string, string>();
            metadata = new List<KeyValuePair<string, string>>();

            optionSet
                .Add
                ("h|help", "Prints this message and exits.", s =>
                    {
                        optionSet.WriteOptionDescriptions(System.Console.Out);
                        Environment.Exit(0);
                    }
                )
                .Add("s|script=", "Set the path of script", s => dictionaryConfig.Add("scriptPath", s))
                .Add("j|json=", "Set the path of json file describe the values", s => dictionaryConfig.Add("jsonPath", s))
                .Add("w|work=", "Set the work folder", s => dictionaryConfig.Add("workPath", s));

            optionSet.Parse(args);

            Directory.SetCurrentDirectory(dictionaryConfig["workPath"]);
            TemplateHelper.RunTemplatedScript(dictionaryConfig["scriptPath"], dictionaryConfig["jsonPath"]);
        }
    }
}
