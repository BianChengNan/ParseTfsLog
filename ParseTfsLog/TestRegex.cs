using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ParseTfsLog
{
    class TestRegex
    {
        public static void Test()
        {
            StreamReader sr = new StreamReader("tfs.log", Encoding.Default);
            string input;
            string pattern = @"\s*(\w*)\s*(.*)";
            while (sr.Peek() >= 0)
            {
                input = sr.ReadLine();
                Regex rgx = new Regex(pattern, RegexOptions.IgnoreCase);
                MatchCollection matches = rgx.Matches(input);
                if (matches.Count > 0)
                {
                    Console.WriteLine("{0} ({1} matches):", input, matches.Count);
                    foreach (Match match in matches)
                    {
                        GroupCollection groups = match.Groups;
                        Console.WriteLine("'{0}, {1}' repeated at positions {2} and {3}",
                                          groups[0].Value,
                                          groups[1].Value,
                                          groups[2].Value,
                                          groups[1].Index);
                        Console.WriteLine("   " + match.Value);
                    }
                }
            }
            sr.Close();
        }
    }
}
