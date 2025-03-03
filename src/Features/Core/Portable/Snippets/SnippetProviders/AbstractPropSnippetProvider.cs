﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.LanguageService;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Snippets.SnippetProviders
{
    internal abstract class AbstractPropSnippetProvider : AbstractSnippetProvider
    {
        public override string SnippetIdentifier => "prop";

        public override string SnippetDescription => FeaturesResources.property_;

        /// <summary>
        /// Generates the property syntax.
        /// Requires language specificity for the TypeSyntax as well as the
        /// type of the PropertySyntax.
        /// </summary>
        protected abstract Task<SyntaxNode> GenerateSnippetSyntaxAsync(Document document, int position, CancellationToken cancellationToken);

        protected override async Task<ImmutableArray<TextChange>> GenerateSnippetTextChangesAsync(Document document, int position, CancellationToken cancellationToken)
        {
            var propertyDeclaration = await GenerateSnippetSyntaxAsync(document, position, cancellationToken).ConfigureAwait(false);
            return ImmutableArray.Create(new TextChange(TextSpan.FromBounds(position, position), propertyDeclaration.NormalizeWhitespace().ToFullString()));
        }

        protected override Func<SyntaxNode?, bool> GetSnippetContainerFunction(ISyntaxFacts syntaxFacts)
        {
            return syntaxFacts.IsPropertyDeclaration;
        }
    }
}
