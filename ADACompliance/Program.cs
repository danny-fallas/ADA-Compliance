﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace ADACompliance
{
    class Program
    {
        public static string someHTML =
            @"<html>
<body>
<table>
{0}
</table>
</body>
</html>";

        public class ResultSet
        {
            public string errorTitle { get; set; }
            public string errorDescription { get; set; }
            public string errorSnippet { get; set; }
            public string position { get; set; }
        }

        public static List<string> InternalLinks { get; set; }

        public static List<ResultSet> CheckADACompliance(string site)
        {
            List<ResultSet> returnMe = new List<ResultSet>();
            string requestUrl;
            requestUrl = "https://tenon.io/async.php";

            try
            {
                using (var client = new WebClient())
                {
                    var values = new NameValueCollection();
                    values["url"] = site;
                    values["certainty"] = "80";
                    values["priority"] = "80";

                    var response = client.UploadValues(requestUrl, values);

                    var responseString = Encoding.Default.GetString(response);

                    var json = new JavaScriptSerializer();
                    var data = json.Deserialize<object>(responseString);

                    var resultSet = ((dynamic)data)["resultSet"];

                    foreach (var result in resultSet)
                    {
                        //if ((int)((dynamic)result)["priority"] < 90) continue;

                        ResultSet currentResultSet = new ResultSet();
                        currentResultSet.errorTitle = ((dynamic)result)["errorTitle"];
                        currentResultSet.errorDescription = ((dynamic)result)["errorDescription"];
                        currentResultSet.errorSnippet = ((dynamic)result)["errorSnippet"];
                        currentResultSet.position = @"Column: " +
                            ((dynamic)((dynamic)result)["position"])["column"] +
                            @"-Line: " +
                            ((dynamic)((dynamic)result)["position"])["line"];

                        returnMe.Add(currentResultSet);
                    }
                }
            }
            catch { return null; }

            return returnMe;
        }

        public static void GetInternalLinks(string inputURL)
        {
            //Console.WriteLine("Inspecting: "+inputURL);
            try
            {
                Uri url = new Uri(inputURL);
                WebClient w = new WebClient();
                string s = w.DownloadString(url);
                w.Dispose();

                List<string> thisPageLinks = new List<string>();
                foreach (var i in Find(s))
                {
                    string newUrl = url.Scheme + "://" + url.Host + i;
                    if (i.StartsWith("/") && !InternalLinks.Contains(newUrl) && !i.Contains("."))
                    {
                        InternalLinks.Add(newUrl);
                        thisPageLinks.Add(newUrl);
                    }
                }

                foreach (string link in thisPageLinks)
                {
                    GetInternalLinks(link);
                }
            }
            catch { }
        }

        public static List<string> Find(string file)
        {
            List<string> list = new List<string>();

            // 1.
            // Find all matches in file.
            MatchCollection m1 = Regex.Matches(file, @"(<a.*?>.*?</a>)",
                RegexOptions.Singleline);

            // 2.
            // Loop over each match.
            foreach (Match m in m1)
            {
                string value = m.Groups[1].Value;

                // 3.
                // Get href attribute.
                Match m2 = Regex.Match(value, @"href=\""(.*?)\""",
                RegexOptions.Singleline);
                if (m2.Success)
                {
                    list.Add(m2.Groups[1].Value);
                }
            }
            return list;
        }

        public static void PrintOutputFile(string data)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output.html");
            File.WriteAllText(path, string.Format(someHTML, data));

            Console.WriteLine("Opening output file ...");
            System.Diagnostics.Process.Start(path);
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Enter the URL to inspect (i.e: http://www.something.com):");
            InternalLinks = new List<string>();
            string usrInput = Console.ReadLine();
            InternalLinks.Add(usrInput);
            GetInternalLinks(usrInput);

            Console.WriteLine("Checking ADA compliance ...");
            string tableRows = string.Empty;
            foreach (string link in InternalLinks)
            {
                var issues = CheckADACompliance(link);
                if (issues != null)
                {
                    foreach (ResultSet issue in issues)
                    {
                        string tableRow = @"<tr>";
                        tableRow += "<td>" + link + "</td>";
                        tableRow += "<td>" + issue.errorTitle + "</td>";
                        tableRow += "<td>" + issue.errorDescription + "</td>";
                        tableRow += "<td>" + issue.errorSnippet + "</td>";
                        tableRow += "<td>" + issue.position + "</td>";
                        tableRow += "</tr>";

                        tableRows += tableRow + Environment.NewLine;
                    }
                }
            }

            Console.WriteLine("Creating output file ...");
            PrintOutputFile(tableRows);
        }
    }
}