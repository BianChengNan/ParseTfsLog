using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParseTfsLog
{
    class Program
    {
       public class Options
        {
            [Option('t', "tfs", Required = false, Default = "tfs.txt", HelpText = "需要解析的 tfs 日志文件")]
            public string File { get; set; }

            [Option('u', "user", Required = false, Default = "*", HelpText = "包含的用户，可以通过逗号指定多个用户")]
            public string User { get; set; }

            [Option('c', "commitMessage", Required = false, Default = "", HelpText = "包含的提交信息关键字，可以通过逗号指定多个关键字")]
            public string CommitMessage { get; set; }

            [Option('e', "ExcludeCommitMessage", Required = false, Default = "", HelpText = "排除的提交信息关键字，可以通过逗号指定多个关键字")]
            public string ExcludeCommitMessage { get; set; }

            [Option('o', "output", Required = false, Default = "", HelpText = "过滤后的 tfs 日志输出文件名")]
            public string Output { get; set; }

            [Option('f', "fileStatistics", Required = false, Default = "", HelpText = "保存变更文件的文件名信息")]
            public string FileStatistics { get; set; }

            [Option('g', "group", Required = false, Default = "", HelpText = "分组关键字，可以通过逗号指定多个关键字")]
            public string GroupKeys { get; set; }

            [Option('p', "path", Required = false, Default = @".\output\", HelpText = "导出结果根目录")]
            public string Path { get; set; }

            [Option('b', "exportBugNumber", Required = false, Default = false, HelpText = "是否导出bug号？ 默认不导出")]
            public bool ExportBugNumber { get; set; }
        }
        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args).WithParsed(option =>
            {
                var ht = new System.Diagnostics.Stopwatch();
                ht.Start();
                ParseTfsLog.ParseAndSave(option.File, option.User, option.CommitMessage, option.ExcludeCommitMessage, option.Path, option.Output, option.FileStatistics, option.GroupKeys, option.ExportBugNumber);
                ht.Stop();
                System.Console.WriteLine(string.Format("Done. cost {0}ms.", ht.ElapsedMilliseconds));
            });
        }
    }
}
