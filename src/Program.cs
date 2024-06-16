using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using CommandLine;
using DiffClip;
using Newtonsoft.Json.Linq;
using LibGit2Sharp;  
namespace DiffClip
{
    public class Options
    {
        [Value(0, MetaName = "directoryPath", Required = true, HelpText = "The path to the directory within the repository to analyse.")]
        public string DirectoryPath { get; set; }

        [Option('s', "summarise", Required = false, HelpText = "Create a summary of the diff and copy to clipboard.")]
        public bool CreateSummary { get; set; }
    }
    
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(RunDiffClip)
                .WithNotParsed(HandleParseError);
        }

        private static void RunDiffClip(Options opts)
        {
            if (!AttachConsole(-1))
            {
                FreeConsole();
            }

            try
            {
                if (!Directory.Exists(opts.DirectoryPath))
                {
                    ShowMessageBox($"The directory '{opts.DirectoryPath}' does not exist.", "Error", 0x00000010);
                    return;
                }

                var gitDiff = GenerateGitDiff(opts.DirectoryPath);

                if (opts.CreateSummary)
                {
                    string summary = SummarizeDiff(gitDiff);
                    summary.CopyToClipboard();
                    ShowMessageBox("Diff summary copied to clipboard.", "Success", 0x00000040);
                }
                else
                {
                    gitDiff.CopyToClipboard();
                    ShowMessageBox("Git diff has been copied to the clipboard.", "Success", 0x00000040);
                }
            }
            catch (Exception ex)
            {
                ShowMessageBox($"An error occurred: {ex.Message}", "Error", 0x00000010);
            }
        }
        private static void HandleParseError(IEnumerable<Error> errs)
        {
            foreach (var err in errs)
            {
                Console.Error.WriteLine(err.ToString());
            }
        }
        private static string GenerateGitDiff(string repoPath)
        {
            using (var repo = new Repository(repoPath))
            {
                var headCommit = repo.Head.Tip;  // Get the head commit
                var changes = repo.Diff.Compare<Patch>(headCommit.Tree, DiffTargets.WorkingDirectory | DiffTargets.Index);

                var sb = new StringBuilder();
                
                // Pass the commit hash to DumpFileDiffs
                sb.AppendLine("===== START OF GIT DIFF =====");
                DumpFileDiffs(changes, sb, headCommit.Sha);
                sb.AppendLine("===== END OF GIT DIFF =====");
                
                return sb.ToString();
            }
        }

        private static void DumpFileDiffs(Patch changes, StringBuilder sb, string commitHash)
        {
            
            sb.AppendLine($"Diff against commit: {commitHash}");  // Display the commit hash
            sb.AppendLine($"Added lines: {changes.LinesAdded}");
            sb.AppendLine($"Deleted lines: {changes.LinesDeleted}");

            if (changes.LinesAdded == 0 && changes.LinesDeleted == 0)
            {
                sb.AppendLine("Note: No file changes have been made since the last commit.");
                return;
            }
    
            foreach (var p in changes)
            {
                sb.AppendLine($"File: {p.Path}");
                sb.AppendLine($"Summary of changes: +{p.LinesAdded} lines, -{p.LinesDeleted} lines");
                sb.AppendLine($"--- Begin file changes for: {p.Path} ---");

                // Combine and sort all changes by line number
                var allChanges = p.AddedLines.Select(line => new { Line = line.LineNumber, Content = $"+ {line.Content}", Type = "Added" })
                    .Concat(p.DeletedLines.Select(line => new { Line = line.LineNumber, Content = $"- {line.Content}", Type = "Deleted" }))
                    .OrderBy(change => change.Line)
                    .ToList();

                foreach (var change in allChanges)
                {
                    sb.Append($"Line {change.Line}: {change.Content}");
                }

                sb.AppendLine();
                sb.AppendLine($"--- End of changes for {p.Path} ---");
            }
        }

        private static string SummarizeDiff(string diff)
        {
            // Placeholder for real summarization logic
            return "Summary of the changes: " + diff.Substring(0, Math.Min(100, diff.Length)) + "...";
        }
        
        private static void ShowMessageBox(string text, string caption, uint type)
        {
            MessageBox(IntPtr.Zero, text, caption, type);
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int MessageBox(IntPtr hWnd, string lpText, string lpCaption, uint uType);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeConsole();
    }
}
