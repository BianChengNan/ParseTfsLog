using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using System.Diagnostics.Tracing;

namespace ParseTfsLog
{
    class ItemInfo
    {
        public string status;
        public string path;
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat(@"  {0} {1}{2}", status, path, Environment.NewLine);
            return sb.ToString();
        }
    }

    class TfsLogInfo
    {
        public string changeset = "";
        public string commitUser = "";
        public string commitDate = "";
        public string commitMessage = "";
        public List<ItemInfo> itemInfoList = new List<ItemInfo>();

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat(@"{0}{1}", ParseTfsLog.itemSplitter, Environment.NewLine);
            sb.AppendFormat(@"变更集: {0}{1}", changeset, Environment.NewLine);
            sb.AppendFormat(@"用户: {0}{1}", commitUser, Environment.NewLine);
            sb.AppendFormat(@"日期: {0}{1}", commitDate, Environment.NewLine);
            sb.AppendFormat(@"{0}", Environment.NewLine);
            sb.AppendFormat(@"注释: {0}  {1}{2}", Environment.NewLine, commitMessage, Environment.NewLine);
            sb.AppendFormat(@"{0}", Environment.NewLine);
            sb.AppendFormat(@"项:{0}", Environment.NewLine);
            foreach (var itemInfo in itemInfoList)
            {
                sb.AppendFormat(@"{0}", itemInfo.ToString());
            }

            sb.AppendFormat(@"{0}", Environment.NewLine);

            return sb.ToString();
        }
    }

    class ParseTfsLog
    {
        public static string itemSplitter = "-------------------------------------------------------------------------------";

        private static Regex changesetRegex = new Regex(@"变更集:\s*(.*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static Regex userRegex = new Regex(@"用户:\s*(.*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static Regex dateRegex = new Regex(@"日期:\s*(.*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static Regex commitMessageRegex = new Regex(@"注释:\s*(.*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static Regex commitItemsRegex = new Regex(@"项:\s*(.*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static Regex itemsRegex = new Regex(@"\s*(\w*)\s+(.*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static Regex splitterRegex = new Regex(@"-{100,}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static Regex bugNumberRegex = new Regex(@"(PC-\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static void ParseAndSave(string tfsLogFile, string user, string commitMessage, string excludeCommitMessage, string exportRootPath, string outFile, string statisticsFileName, string groupKeys, bool exportBugNumber)
        {
            EtwLogger.Instance.TraceMessage("start");
            var tfsLogList = Parse(tfsLogFile, user, commitMessage, excludeCommitMessage);
            tfsLogList.Reverse();

            if (!string.IsNullOrEmpty(outFile))
            {
                SaveParsedData(tfsLogList, Path.Combine(exportRootPath, outFile));
            }

            if (!string.IsNullOrEmpty(statisticsFileName))
            {
                SaveFileStatisticsData(tfsLogList, Path.Combine(exportRootPath, statisticsFileName), exportBugNumber);
            }

            if (!string.IsNullOrEmpty(groupKeys))
            {
                SaveGroupedFileStatisticsData(tfsLogList, exportRootPath, groupKeys, exportBugNumber);
            }

            EtwLogger.Instance.TraceMessage("end");
        }

        private static List<TfsLogInfo> Parse(string tfsLogFile, string user, string commitMessage, string excludeCommitMessage)
        {
            List<TfsLogInfo> tfsLogList = new List<TfsLogInfo>();

            if (!File.Exists(tfsLogFile))
            {
                Debug.Fail(string.Format("{0} not exitsted.", tfsLogFile));
                return tfsLogList;
            }

            FileStream fileStream = null;
            StreamReader streamReader = null;

            var userList = Split(user);
            var commitMessageKeywordList = Split(commitMessage);
            var excludeCommitMessageKeywordList = Split(excludeCommitMessage);
            try
            {
                EtwLogger.Instance.LoopBegin("parselog");

                fileStream = new FileStream(tfsLogFile, FileMode.Open, FileAccess.Read);
                streamReader = new StreamReader(fileStream, Encoding.Default);

                fileStream.Seek(0, SeekOrigin.Begin);

                int count = 0;
                List<string> oneLogStingList = null;
                while ((oneLogStingList = ReadOneLogData(streamReader)) != null)
                {
                    EtwLogger.Instance.LoopIteration("parselog", ++count);

                    var oneLog = ParseTfsLog.ParseOneLog(oneLogStingList);
                    if (oneLog.changeset == "")
                    {
                        System.Console.WriteLine("invalid log parsed.");
                        System.Console.ReadKey();
                    }
                    if (IncludeIf(oneLog, userList, commitMessageKeywordList, excludeCommitMessageKeywordList))
                    {
                        tfsLogList.Add(oneLog);
                    }
                }

                EtwLogger.Instance.LoopDone("parselog");
            }
            catch (Exception e)
            {
                Debug.Fail(e.ToString());
            }
            finally
            {
                if (streamReader != null)
                {
                    streamReader.Close();
                }

                if (fileStream != null)
                {
                    fileStream.Close();
                }
            }

            return tfsLogList;
        }

        private static void SaveParsedData(List<TfsLogInfo> logInfoList, string outFile)
        {
            FileStream fileStream = null;
            StreamWriter streamWriter = null;

            List<TfsLogInfo> tfsLogs = new List<TfsLogInfo>();

            try
            {
                fileStream = new FileStream(outFile, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                streamWriter = new StreamWriter(fileStream, Encoding.Default);

                foreach (var logInfo in logInfoList)
                {
                    streamWriter.Write(logInfo.ToString());
                }
            }
            catch (Exception e)
            {
                Debug.Fail(e.ToString());
            }
            finally
            {
                if (streamWriter != null)
                {
                    streamWriter.Close();
                }

                if (fileStream != null)
                {
                    fileStream.Close();
                }
            }
        }

        private static void SaveBugNumber(StreamWriter streamWriter, string groupKey, List<TfsLogInfo> logInfoList)
        {
            streamWriter.Write(string.Format("--------------------------------------------------------------------------------{0}", Environment.NewLine));

            Dictionary<string, List<string>> groupedBugNumberListDic = new Dictionary<string, List<string>>();

            bool bIsOldFunction = groupKey.Contains("旧功能");
            if (bIsOldFunction)
            {
                var groupedLogInfoList = logInfoList.GroupBy(info => info.commitUser).ToList();
                foreach (var oneGroupLogInfos in groupedLogInfoList)
                {
                    List<string> bugNumerList = new List<string>();
                    var oneGroupLogInfoList = oneGroupLogInfos.ToList();
                    foreach (var logInfo in oneGroupLogInfoList)
                    {
                        bugNumerList.AddRange(GetBugNumbers(logInfo));
                    }

                    groupedBugNumberListDic[string.Format("{0}：", oneGroupLogInfos.Key)] = UniqueAndSortBugNumbers(bugNumerList);
                }
            }
            else
            {
                List<string> bugNumerList = new List<string>();
                foreach (var logInfo in logInfoList)
                {
                    bugNumerList.AddRange(GetBugNumbers(logInfo));
                }

                groupedBugNumberListDic[string.Format("PC_WZK_{0}_", groupKey)] = UniqueAndSortBugNumbers(bugNumerList);
            }

            foreach (var item in groupedBugNumberListDic)
            {
                var strBugNumber = string.Join("", item.Value);
                streamWriter.Write(string.Format("{0}{1}{2}", item.Key, string.IsNullOrEmpty(strBugNumber) ? "N/A" : strBugNumber, Environment.NewLine));
            }
        }

        private static void SaveFileStatisticsData(List<TfsLogInfo> logInfoList, string outFile, bool exportBugNumber)
        {
            FileStream fileStream = null;
            StreamWriter streamWriter = null;

            var logfileDictionary = new Dictionary<string, List<TfsLogInfo>>();
            foreach (var logInfo in logInfoList)
            {
                var files = logInfo.itemInfoList.Select(log => log.path).ToList();
                foreach (var filePath in files)
                {
                    List<TfsLogInfo> logInfoListInDictionary;
                    if (logfileDictionary.TryGetValue(filePath, out logInfoListInDictionary))
                    {
                        logInfoListInDictionary.Add(logInfo);
                    }
                    else
                    {
                        logInfoListInDictionary = new List<TfsLogInfo>();
                        logInfoListInDictionary.Add(logInfo);
                        logfileDictionary.Add(filePath, logInfoListInDictionary);
                    }
                }
            }

            try
            {
                var dir = Path.GetDirectoryName(outFile);

                if (!Directory.Exists(dir))
                {
                    try
                    {
                        Directory.CreateDirectory(dir);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(string.Format("Create DIR [{0}] failed with exception {1}", dir, ex));
                    }
                }

                fileStream = new FileStream(outFile, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                streamWriter = new StreamWriter(fileStream, Encoding.Default);

                foreach (var item in logfileDictionary)
                {
                    var changesetList = item.Value.Select(log => log.changeset).ToList();
                    var changeset = string.Join(" | ", changesetList.ToArray());

                    List<string> statusList = null;
                    foreach (var logInfo in item.Value)
                    {
                        statusList = logInfo.itemInfoList.Where(info => info.path == item.Key).Select(info => info.status).ToList();
                    }

                    var strStatus = "unkown";
                    if (statusList != null && statusList.Count > 0)
                    {
                        strStatus = string.Join(" | ", statusList.ToArray());
                        strStatus = strStatus.Replace(",", " | ");
                    }

                    streamWriter.Write(string.Format("{0},{1},{2}{3}", item.Key, strStatus, changeset, Environment.NewLine));
                }

                if (exportBugNumber)
                {
                    var groupKey = System.IO.Path.GetFileNameWithoutExtension(outFile);
                    SaveBugNumber(streamWriter, groupKey, logInfoList);
                }
            }
            catch (Exception e)
            {
                Debug.Fail(e.ToString());
            }
            finally
            {
                if (streamWriter != null)
                {
                    streamWriter.Close();
                }

                if (fileStream != null)
                {
                    fileStream.Close();
                }
            }
        }

        private static void SaveGroupedFileStatisticsData(List<TfsLogInfo> logInfoList, string exportRootPath, string groupKeywords, bool exportBugNumber)
        {
            var groupKeywordList = Split(groupKeywords);
            foreach (var groupKey in groupKeywordList)
            {
                var logInfoListOfThisKey = logInfoList.Where(info => info.commitMessage.Contains(groupKey)).ToList();
                if (logInfoListOfThisKey.Count > 0)
                {
                    string fileName = groupKey + ".csv";
                    SaveFileStatisticsData(logInfoListOfThisKey, Path.Combine(exportRootPath, fileName), exportBugNumber);
                }
            }

            var logInfoListOfRest = logInfoList.Where(info =>
            {
                foreach (var groupKey in groupKeywordList)
                {
                    if (info.commitMessage.Contains(groupKey))
                    {
                        return false;
                    }
                }

                return true;
            }).ToList();

            if (logInfoListOfRest.Count > 0)
            {
                string fileName = "other.csv";
                SaveFileStatisticsData(logInfoListOfRest, Path.Combine(exportRootPath, fileName), exportBugNumber);
            }
        }

        private static List<string> UniqueAndSortBugNumbers(List<string> bugNumerList)
        {
            bugNumerList.Distinct();
            bugNumerList.Sort();
            return bugNumerList;
        }

        private static List<string> GetBugNumbers(TfsLogInfo logInfo)
        {
            var result = new List<string>();

            char[] splitter = new char[]{'_'};
            var splittedCommitMsgPartList = Split(logInfo.commitMessage, splitter);
            foreach (var splitMsg in splittedCommitMsgPartList)
            {
                bugNumberRegex.Matches(splitMsg);
                MatchCollection matches = bugNumberRegex.Matches(splitMsg);
                for (int idx = 0; idx < matches.Count; ++idx )
                {
                    var matchData = matches[idx];
                    result.Add(matchData.Value);
                }
                //if (splitMsg.ToUpper().Contains("PC-"))
                //{
                //    result.AddRange(Split(splitMsg));
                //}
            }
            
            return result;
        }

        public static string Trim(string value)
        {
            char[] trimChars = { ' ', '\t', '、', '\n', '\r' };
            return value.Trim(trimChars);
        }
        public static void Trim(ref List<string> valueList)
        {
            for (int idx = 0; idx < valueList.Count; ++idx)
            {
                valueList[idx] = Trim(valueList[idx]);
            }
        }
        private static void Unique(ref List<string> dataList)
        {
            dataList = dataList.Distinct().ToList();
        }

        private static void RemoveEmpty(ref List<string> dataList)
        {
            dataList.RemoveAll(data => { return string.IsNullOrEmpty(data); });
        }

        private static void RemoveEmptyAndUnique(ref List<string> dataList)
        {
            RemoveEmpty(ref dataList);
            Unique(ref dataList);
        }

        private static List<string> Split(string data, char[] trimChars = null)
        {
            if (trimChars == null)
            {
                trimChars = new char[]{ ',' };
            }
            var result = data.Split(trimChars).ToList();
            RemoveEmptyAndUnique(ref result);
            return result;
        }

        private static String WildCardToRegex(string rex)
        {
            return Regex.Escape(rex).Replace("\\?", ".").Replace("\\*", ".*");
        }

        private static bool IncludeIf(TfsLogInfo info, List<string> userList, List<string> commitMessageKeywordList,List<string> excludeCommitMessageKeywordList)
        {

            foreach (var commitMessage in excludeCommitMessageKeywordList)
            {
                var commitMessageRegex = new Regex(WildCardToRegex(commitMessage), RegexOptions.Compiled | RegexOptions.IgnoreCase);
                MatchCollection matches = commitMessageRegex.Matches(info.commitMessage);
                if (matches.Count > 0)
                {
                    return false;
                }
            }

            foreach (var user in userList)
            {
                var userRegex = new Regex(WildCardToRegex(user), RegexOptions.Compiled | RegexOptions.IgnoreCase);
                MatchCollection matches = userRegex.Matches(info.commitUser);
                if (matches.Count > 0)
                {
                    return true;
                }
            }

            foreach (var commitMessage in commitMessageKeywordList)
            {
                var commitMessageRegex = new Regex(WildCardToRegex(commitMessage), RegexOptions.Compiled | RegexOptions.IgnoreCase);
                MatchCollection matches = commitMessageRegex.Matches(info.commitMessage);
                if (matches.Count > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSplitter(string curLine)
        {
            //var splitterRegex = new Regex(@"-{30,}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            MatchCollection matches = splitterRegex.Matches(curLine);
            return (matches.Count > 0);
        }

        private static List<string> ReadOneLogData(StreamReader streamReader)
        {
            string curLine = streamReader.ReadLine();
            if (curLine == null)
            {
                return null;
            }

            List<string> oneLogStringList = new List<string>();

            while (curLine != null)
            {
                if (IsSplitter(curLine))
                {
                    if (oneLogStringList.Count != 0)
                    {
                        break;
                    }
                }

                oneLogStringList.Add(curLine);

                curLine = streamReader.ReadLine();
            }

            return oneLogStringList;
        }

        public static string GetMatchedValue(MatchCollection matches, int index = 1)
        {
            if (matches.Count <= 0)
            {
                return "";
            }

            return matches[0].Groups[index].Value;
        }

        private static TfsLogInfo ParseOneLog(List<string> logStringList)
        {
            TfsLogInfo logInfo = new TfsLogInfo();
            var bCommitMessage = false;
            var bCommitItem = false;

            for (var index = 0; index < logStringList.Count; index++)
            {
                var curLine = logStringList[index];
                if (IsSplitter(curLine) || curLine == "")
                {
                    continue;
                }

                var matches = changesetRegex.Matches(curLine);
                if (matches.Count > 0)
                {
                    logInfo.changeset = GetMatchedValue(matches);
                    continue;
                }

                matches = userRegex.Matches(curLine);
                if (matches.Count > 0)
                {
                    logInfo.commitUser = GetMatchedValue(matches);
                    continue;
                }

                matches = dateRegex.Matches(curLine);
                if (matches.Count > 0)
                {
                    logInfo.commitDate = GetMatchedValue(matches);
                    continue;
                }

                matches = commitMessageRegex.Matches(curLine);
                if (matches.Count > 0)
                {
                    bCommitMessage = true;
                    continue;
                }

                matches = commitItemsRegex.Matches(curLine);
                if (matches.Count > 0)
                {
                    bCommitMessage = false;
                    bCommitItem = true;
                    continue;
                }

                if (bCommitMessage)
                {
                    logInfo.commitMessage += curLine;
                    continue;
                }

                if (bCommitItem)
                {
                    string strLine = curLine;
                    strLine = strLine.Replace("合并, ", "");

                    matches = itemsRegex.Matches(strLine);

                    var itemInfo = new ItemInfo();
                    if (matches.Count > 0)
                    {
                        itemInfo.status = GetMatchedValue(matches, 1);
                        itemInfo.path = GetMatchedValue(matches, 2);
                    }
                    else
                    {
                        itemInfo.status = "Unkown";
                        itemInfo.path = curLine;
                        continue;
                    }

                    logInfo.itemInfoList.Add(itemInfo);
                }
            }

            return logInfo;
        }

    }
}
