using System;
using System.IO;

// C# 10 file‑scoped namespace with top‑level statements
namespace AuditSummary;

string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
string fileName = "AuditSummary.md";
string content = $"# Audit Summary\n\nGenerated on {today}\n\n* Summary of findings goes here.\n";
File.WriteAllText(fileName, content);
Console.WriteLine($"Report written to {fileName}");