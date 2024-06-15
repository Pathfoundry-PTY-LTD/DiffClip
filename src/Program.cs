using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using DiffClip;
using Newtonsoft.Json.Linq;
using LibGit2Sharp;  

namespace DiffClip
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            if (!AttachConsole(-1))
            {
                FreeConsole();
            }

            RunDiffClip(args);
        }

        private static void RunDiffClip(string[] args)
        {
            if (args.Length != 1)
            {
                ShowMessageBox("Usage: DiffClip <directoryPath>", "Error", 0x00000010);
                return;
            }

            string directoryPath = args[0];
            string configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

            if (!File.Exists(configFilePath))
            {
                ShowMessageBox("Configuration file 'config.json' not found.", "Error", 0x00000010);
                return;
            }

            try
            {
                // Verify that the directory exists
                if (!Directory.Exists(directoryPath))
                {
                    ShowMessageBox($"The directory '{directoryPath}' does not exist.", "Error", 0x00000010);
                    return;
                }

                // Generate the Git diff and copy to clipboard
                string gitDiff = GenerateGitDiff(directoryPath);
                gitDiff.CopyToClipboard();
                ShowMessageBox("Git diff has been copied to the clipboard.", "Success", 0x00000040);
            }
            catch (Exception ex)
            {
                ShowMessageBox($"An error occurred: {ex.Message}", "Error", 0x00000010);
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
                DumpFileDiffs(changes, sb, headCommit.Sha);
        
                return sb.ToString();
            }
        }

        private static void DumpFileDiffs(Patch changes, StringBuilder sb, string commitHash)
        {
            sb.AppendLine("===== START OF GIT DIFF =====");
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

            sb.AppendLine("===== END OF GIT DIFF =====");
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
