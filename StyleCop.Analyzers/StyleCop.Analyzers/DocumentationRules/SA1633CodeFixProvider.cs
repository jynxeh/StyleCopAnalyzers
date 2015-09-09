﻿namespace StyleCop.Analyzers.DocumentationRules
{
    using System.Collections.Immutable;
    using System.Composition;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CodeActions;
    using Microsoft.CodeAnalysis.CodeFixes;
    using Microsoft.CodeAnalysis.CSharp;
    using StyleCop.Analyzers.Helpers;
    using StyleCop.Analyzers.Settings.ObjectModel;

    /// <summary>
    /// Implements a code fix for SA1633.
    /// </summary>
    /// <remarks>
    /// <para>To fix a violation of this rule, add a standard file header at the top of the file.</para>
    /// </remarks>
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SA1633CodeFixProvider))]
    [Shared]
    public class SA1633CodeFixProvider : CodeFixProvider
    {
        /// <inheritdoc/>
        public override ImmutableArray<string> FixableDiagnosticIds { get; }
            = ImmutableArray.Create(FileHeaderAnalyzers.SA1633DescriptorMissing.Id);

        /// <inheritdoc/>
        public override FixAllProvider GetFixAllProvider()
        {
            return CustomFixAllProviders.BatchFixer;
        }

        /// <inheritdoc/>
        public override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            foreach (Diagnostic diagnostic in context.Diagnostics.Where(d => this.FixableDiagnosticIds.Contains(d.Id)))
            {
                context.RegisterCodeFix(CodeAction.Create(DocumentationResources.SA1633CodeFix, token => GetTransformedDocumentAsync(context.Document, token), equivalenceKey: nameof(SA1633CodeFixProvider)), diagnostic);
            }

            return SpecializedTasks.CompletedTask;
        }

        private static async Task<Document> GetTransformedDocumentAsync(Document document, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var settings = document.Project.AnalyzerOptions.GetStyleCopSettings();

            var fileHeader = FileHeaderHelpers.ParseFileHeader(root);
            var newSyntaxRoot = fileHeader.IsMissing ? AddHeader(root, document.Name, settings) : ReplaceHeader(document, root, settings);

            return document.WithSyntaxRoot(newSyntaxRoot);
        }

        private static SyntaxNode ReplaceHeader(Document document, SyntaxNode root, StyleCopSettings settings)
        {
            // Skip single line comments, whitespace, and end of line trivia until a blank line is encountered.
            SyntaxTriviaList trivia = root.GetLeadingTrivia();

            bool onBlankLine = false;
            while (trivia.Any())
            {
                bool done = false;
                switch (trivia[0].Kind())
                {
                case SyntaxKind.SingleLineCommentTrivia:
                    trivia = trivia.RemoveAt(0);
                    onBlankLine = false;
                    break;

                case SyntaxKind.WhitespaceTrivia:
                    trivia = trivia.RemoveAt(0);
                    break;

                case SyntaxKind.EndOfLineTrivia:
                    trivia = trivia.RemoveAt(0);

                    if (onBlankLine)
                    {
                        done = true;
                    }
                    else
                    {
                        onBlankLine = true;
                    }

                    break;

                default:
                    done = true;
                    break;
                }

                if (done)
                {
                    break;
                }
            }

            return root.WithLeadingTrivia(CreateNewHeader(document.Name, settings).Add(SyntaxFactory.CarriageReturnLineFeed).Add(SyntaxFactory.CarriageReturnLineFeed).AddRange(trivia));
        }

        private static SyntaxNode AddHeader(SyntaxNode root, string name, StyleCopSettings settings)
        {
            var newTrivia = CreateNewHeader(name, settings).Add(SyntaxFactory.CarriageReturnLineFeed).Add(SyntaxFactory.CarriageReturnLineFeed);
            newTrivia = newTrivia.AddRange(root.GetLeadingTrivia());

            return root.WithLeadingTrivia(newTrivia);
        }

        private static SyntaxTriviaList CreateNewHeader(string filename, StyleCopSettings settings)
        {
            var copyrightText = "// " + GetCopyrightText(settings.DocumentationRules.CopyrightText);
            var newHeader = settings.DocumentationRules.XmlHeader
                ? WrapInXmlComment(copyrightText, filename, settings)
                : copyrightText;
            return SyntaxFactory.ParseLeadingTrivia(newHeader);
        }

        private static string WrapInXmlComment(string copyrightText, string filename, StyleCopSettings settings)
        {
            return $@"// <copyright file=""{filename}"" company=""{settings.DocumentationRules.CompanyName}"">
{copyrightText}
// </copyright>";
        }

        private static string GetCopyrightText(string copyrightText)
        {
            return string.Join("\n// ", copyrightText.Split('\n'));
        }
    }
}