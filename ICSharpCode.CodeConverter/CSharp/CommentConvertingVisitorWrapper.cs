﻿using System;
using System.Linq;
using System.Threading.Tasks;
using ICSharpCode.CodeConverter.Shared;
using ICSharpCode.CodeConverter.Util;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.VisualBasic;
using CS = Microsoft.CodeAnalysis.CSharp;
using CSSyntax = Microsoft.CodeAnalysis.CSharp.Syntax;
using VBasic = Microsoft.CodeAnalysis.VisualBasic;
using VBSyntax = Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace ICSharpCode.CodeConverter.CSharp
{
    internal class CommentConvertingVisitorWrapper
    {
        private readonly VBasic.VisualBasicSyntaxVisitor<Task<CS.CSharpSyntaxNode>> _wrappedVisitor;
        private readonly SyntaxTree _syntaxTree;

        public CommentConvertingVisitorWrapper(VisualBasicSyntaxVisitor<Task<CSharpSyntaxNode>> wrappedVisitor, SyntaxTree syntaxTree)
        {
            _wrappedVisitor = wrappedVisitor;
            _syntaxTree = syntaxTree;
        }

        public async Task<T> Accept<T>(SyntaxNode vbNode, bool addSourceMapping) where T : CS.CSharpSyntaxNode
        {
            return await ConvertHandled<T>(vbNode, addSourceMapping);
        }

        public async Task<SeparatedSyntaxList<TOut>> Accept<TIn, TOut>(SeparatedSyntaxList<TIn> vbNodes, bool addSourceMapping) where TIn : VBasic.VisualBasicSyntaxNode where TOut : CS.CSharpSyntaxNode
        {
            var convertedNodes = await vbNodes.SelectAsync(n => ConvertHandled<TOut>(n, addSourceMapping));
            var convertedSeparators = vbNodes.GetSeparators().Select(s =>
                CS.SyntaxFactory.Token(CS.SyntaxKind.CommaToken)
                    .WithConvertedTrailingTriviaFrom(s, TriviaKinds.FormattingOnly)
                    .WithSourceMappingFrom(s)
            );
            return CS.SyntaxFactory.SeparatedList(convertedNodes, convertedSeparators);
        }

        private async Task<T> ConvertHandled<T>(SyntaxNode vbNode, bool addSourceMapping) where T : CS.CSharpSyntaxNode
        {
            try {
                var converted = (T)await _wrappedVisitor.Visit(vbNode);
                return addSourceMapping && _syntaxTree == vbNode.SyntaxTree
                    ? WithSourceMapping(vbNode, converted)
                    : converted.WithoutSourceMapping();
            } catch (Exception e) {
                var dummyStatement = (T)(object)CS.SyntaxFactory.EmptyStatement();
                return dummyStatement.WithCsTrailingErrorComment((VBasic.VisualBasicSyntaxNode)vbNode, e);
            }
        }

        /// <remarks>
        /// If lots of special cases, move to wrapping the wrappedVisitor in another visitor, but I'd rather use a simple switch here initially.
        /// </remarks>
        private static T WithSourceMapping<T>(SyntaxNode vbNode, T converted) where T : CS.CSharpSyntaxNode
        {
            switch (vbNode) {
                case VBSyntax.CompilationUnitSyntax vbCus when converted is CSSyntax.CompilationUnitSyntax csCus:
                    converted = (T)(object)csCus.WithEndOfFileToken(
                        csCus.EndOfFileToken.WithSourceMappingFrom(vbCus.EndOfFileToken)
                     );
                    break;
            }
            return vbNode.CopyAnnotationsTo(converted).WithVbSourceMappingFrom(vbNode);
        }
    }
}
