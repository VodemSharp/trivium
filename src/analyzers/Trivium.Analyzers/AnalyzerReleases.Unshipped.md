; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

 Rule ID       | Category | Severity | Notes
---------------|----------|----------|---------------------------------------------------
 TRIVSTYLE0001 | Naming   | Warning  | Name CancellationToken parameters and locals 'ct'
 TRIVSTYLE0002 | Usage    | Warning  | Assign awaited calls to a local instead of inlining them
 TRIVSTYLE0003 | Formatting | Warning | Put an object initializer on its own line
 TRIVSTYLE0004 | Formatting | Warning | Add a blank line after a multi-line statement
 TRIVSTYLE0005 | Usage    | Warning  | Assign awaited calls in conditions to a local instead of inlining them
