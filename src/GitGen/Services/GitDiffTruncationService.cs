using System.Text;
using System.Text.RegularExpressions;

namespace GitGen.Services;

/// <summary>
///     Service for intelligently truncating git diffs to fit within token limits.
/// </summary>
public class GitDiffTruncationService
{
    private readonly IConsoleLogger _logger;
    
    // Conservative estimate: 3 characters per token for safety
    private const int CharsPerToken = 3;
    
    // Regex patterns for parsing git diff
    private static readonly Regex FileHeaderPattern = new(@"^diff --git a/.+ b/.+$", RegexOptions.Multiline);
    private static readonly Regex HunkHeaderPattern = new(@"^@@.*@@", RegexOptions.Multiline);

    public GitDiffTruncationService(IConsoleLogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Truncates a git diff to fit within the specified token limit.
    /// </summary>
    /// <param name="diff">The original git diff.</param>
    /// <param name="maxTokens">The maximum number of tokens allowed.</param>
    /// <param name="systemPromptTokens">Estimated tokens used by the system prompt.</param>
    /// <returns>A truncated version of the diff that should fit within the token limit.</returns>
    public string TruncateDiff(string diff, int maxTokens, int systemPromptTokens)
    {
        // Calculate available tokens for the diff
        var availableTokens = maxTokens - systemPromptTokens;
        
        // Reserve some tokens for safety margin (10%)
        var targetTokens = (int)(availableTokens * 0.9);
        
        // Convert to character limit
        var targetChars = targetTokens * CharsPerToken;
        
        _logger.Debug($"Truncating diff: original length={diff.Length}, target chars={targetChars}");
        
        if (diff.Length <= targetChars)
        {
            return diff;
        }

        // Parse the diff into files
        var files = ParseDiffIntoFiles(diff);
        
        if (files.Count == 0)
        {
            // Fallback: simple truncation
            return TruncateSimple(diff, targetChars);
        }

        // Intelligent truncation: try to keep complete files
        return TruncateIntelligently(files, targetChars);
    }

    private List<DiffFile> ParseDiffIntoFiles(string diff)
    {
        var files = new List<DiffFile>();
        var lines = diff.Split('\n');
        DiffFile? currentFile = null;
        var contentBuilder = new StringBuilder();

        foreach (var line in lines)
        {
            if (FileHeaderPattern.IsMatch(line))
            {
                // Save previous file if exists
                if (currentFile != null)
                {
                    currentFile.Content = contentBuilder.ToString();
                    files.Add(currentFile);
                }

                // Start new file
                currentFile = new DiffFile { Header = line };
                contentBuilder.Clear();
                contentBuilder.AppendLine(line);
            }
            else if (currentFile != null)
            {
                contentBuilder.AppendLine(line);
            }
        }

        // Save last file
        if (currentFile != null)
        {
            currentFile.Content = contentBuilder.ToString();
            files.Add(currentFile);
        }

        return files;
    }

    private string TruncateIntelligently(List<DiffFile> files, int targetChars)
    {
        var result = new StringBuilder();
        var currentLength = 0;

        // First pass: include all file headers
        foreach (var file in files)
        {
            if (currentLength + file.Header.Length > targetChars)
                break;

            result.AppendLine(file.Header);
            currentLength += file.Header.Length + 1;
        }

        // Second pass: add as much content as possible
        foreach (var file in files)
        {
            var remainingSpace = targetChars - currentLength;
            if (remainingSpace <= 0)
                break;

            // Skip the header (already added)
            var contentWithoutHeader = file.Content.Substring(file.Header.Length).TrimStart('\n');
            
            if (contentWithoutHeader.Length <= remainingSpace)
            {
                // Can fit entire file content
                result.Append(contentWithoutHeader);
                currentLength += contentWithoutHeader.Length;
            }
            else
            {
                // Truncate this file's content
                var truncatedContent = TruncateFileContent(contentWithoutHeader, remainingSpace);
                result.Append(truncatedContent);
                result.AppendLine("\n... (content truncated) ...");
                break;
            }
        }

        // Add summary of what was truncated
        var truncatedFiles = files.Count - files.Take(result.ToString().Split('\n').Count(l => FileHeaderPattern.IsMatch(l))).Count();
        if (truncatedFiles > 0)
        {
            result.AppendLine($"\n... ({truncatedFiles} more files truncated) ...");
        }

        return result.ToString();
    }

    private string TruncateFileContent(string content, int maxLength)
    {
        if (content.Length <= maxLength)
            return content;

        // Try to truncate at a hunk boundary
        var lastHunkIndex = content.LastIndexOf("\n@@", maxLength);
        if (lastHunkIndex > maxLength / 2) // Only use if we're not losing too much
        {
            return content.Substring(0, lastHunkIndex);
        }

        // Otherwise, truncate at a line boundary
        var lastNewlineIndex = content.LastIndexOf('\n', maxLength);
        if (lastNewlineIndex > 0)
        {
            return content.Substring(0, lastNewlineIndex);
        }

        // Fallback: hard truncate
        return content.Substring(0, maxLength);
    }

    private string TruncateSimple(string diff, int targetChars)
    {
        if (diff.Length <= targetChars)
            return diff;

        // Find a good truncation point (preferably at a line boundary)
        var truncateAt = Math.Min(diff.Length, targetChars);
        var lastNewline = diff.LastIndexOf('\n', truncateAt);
        
        if (lastNewline > targetChars / 2) // Only use if we're not losing too much
        {
            truncateAt = lastNewline;
        }

        return diff.Substring(0, truncateAt) + "\n\n... (diff truncated) ...";
    }

    private class DiffFile
    {
        public string Header { get; set; } = "";
        public string Content { get; set; } = "";
    }
}