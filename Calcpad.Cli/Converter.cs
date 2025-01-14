﻿using Calcpad.OpenXml;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Calcpad.Cli
{
    internal class Converter
    {
        private readonly StringBuilder _sb = new();
        private readonly string _htmlWorksheet;

        internal Converter()
        {
            var appUrl = "file:///" + Program.AppPath.Replace("\\", "/");
            _htmlWorksheet = File.ReadAllText(Program.AppPath + "template.html").Replace("jquery", appUrl + "jquery");
        }

        internal void ToHtml(string html, string path)
        {
            File.WriteAllText(path, HtmlApplyWorksheet(html));
            if (File.Exists(path))
                Run(path);
        }

        internal void ToOpenXml(string html, string path)
        {
            html = GetHtmlData(HtmlApplyWorksheet(html));
            new OpenXmlWriter().Convert(html, path);
            if (File.Exists(path))
                Run(path);
        }
        internal void ToPdf(string html, string path)
        {
            var htmlFile = Path.ChangeExtension(path, ".html");
            File.WriteAllText(htmlFile, HtmlApplyWorksheet(html));
            var startInfo = new ProcessStartInfo
            {
                FileName = Program.AppPath + "wkhtmltopdf.exe"
            };
            const string s = " --enable-local-file-access --disable-smart-shrinking --page-size A4  --margin-bottom 15 --margin-left 15 --margin-right 10 --margin-top 15 ";
            if (htmlFile.Contains(' ', StringComparison.Ordinal))
                startInfo.Arguments = s + '\"' + htmlFile + "\" \"" + path + '\"';
            else
                startInfo.Arguments = s + htmlFile + " " + path;

            startInfo.UseShellExecute = true;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            var process = Process.Start(startInfo);
            process.WaitForExit();
            File.Delete(htmlFile);
            if (File.Exists(path))
                Run(path);
        }

        private static void Run(string fileName) 
        {
            Process process = new()
            {
                StartInfo = new ProcessStartInfo(fileName)
                {
                    UseShellExecute = true
                }
            };
            process.Start();
        }

        private string HtmlApplyWorksheet(string s)
        {
            
            _sb.Append(_htmlWorksheet);
            _sb.Append(s);
            _sb.Append(" </body></html>");
            return _sb.ToString();
        }

        private static string GetHtmlData(string html)
        {
            var sb = new StringBuilder(500);
            const string header =
@"Version:1.0
StartHTML:0000000001
EndHTML:0000000002
StartFragment:0000000003
EndFragment:0000000004";
            const string startFragmentText = "<!DOCTYPE HTML><!--StartFragment-->";
            const string endFragmentText = "<!--EndFragment-->";
            var startHtml = header.Length;
            var startFragment = startHtml + startFragmentText.Length;
            var endFragment = startFragment + html.Length;
            var endHtml = endFragment + endFragmentText.Length;
            sb.Append(header);
            sb.Replace("0000000001", $"{startHtml,8}");
            sb.Replace("0000000002", $"{endHtml,8}");
            sb.Replace("0000000003", $"{startFragment,8}");
            sb.Replace("0000000004", $"{endFragment,8}");
            sb.Append(startFragmentText);
            sb.Append(html);
            sb.Append(endFragmentText);
            return sb.ToString();
        }
    }
}
