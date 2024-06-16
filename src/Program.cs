using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using CommandLine;
using DiffClip;
using Newtonsoft.Json.Linq;
using LibGit2Sharp;
using OpenAI_API;
using OpenAI_API.Models;

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
        static async Task Main(string[] args)
        {
            await Parser.Default.ParseArguments<Options>(args)
                .WithNotParsed(HandleParseError).WithParsedAsync(RunDiffClip);
        }

        private static async Task RunDiffClip(Options opts)
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
                    var summary = await SummarizeDiff(gitDiff);
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

        private static async Task<string> SummarizeDiff(string gitDiff)
        {
            var apiKey = LoadApiKey();
            
            // Initialize the OpenAI API client
            var api = new OpenAIAPI(apiKey);

            // Create a new chat conversation
            var chat = api.Chat.CreateConversation();
            chat.Model = Model.ChatGPTTurbo;
            chat.RequestParameters.Temperature = 0;

            // Add system instructions
            chat.AppendSystemMessage("You are a helpful professional software developer whose role is to summarise git diffs and turn them into meaningful commit messages. Write with brevity, clarity, and professionalism, and use correct CRLFs and spacing to make it easy to read.");

            // Add user input
            string prompt = $@"
As a highly skilled code reviewer, your role is to generate clear, professional, and detailed commit messages based on git diffs. Each commit message should start with a single-line summary that concisely captures the overall intent of the changes, followed by a blank line and then a more detailed explanation where necessary. The detailed section should explain the reason behind the changes, mention any parts of the system that are affected, and note any additional consequences or benefits that might not be immediately obvious from the code changes.

Here is the git diff you need to summarize:
{gitDiff}

Please format the commit message with a summary line, followed by a detailed explanation as needed.";
            chat.AppendUserInput(prompt);
            
            return await chat.GetResponseFromChatbotAsync();
        }
        private static string LoadApiKey()
        {
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
            var configText = File.ReadAllText(configPath);
            var config = JObject.Parse(configText);
            
            return config["OpenAIKey"]?.ToString() ?? string.Empty;
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
