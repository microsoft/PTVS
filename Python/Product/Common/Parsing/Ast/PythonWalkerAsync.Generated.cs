// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Common.Core.Text;

namespace Microsoft.PythonTools.Common.Parsing.Ast {
    /// <summary>
    /// PythonWalker class - The Python AST Walker (default result is true)
    /// </summary>
    public class PythonWalkerAsync {
        // AndExpression
        public virtual Task<bool> WalkAsync(AndExpression node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(AndExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // AwaitExpression
        public virtual Task<bool> WalkAsync(AwaitExpression node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(AwaitExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // BackQuoteExpression
        public virtual Task<bool> WalkAsync(BackQuoteExpression node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(BackQuoteExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // BinaryExpression
        public virtual Task<bool> WalkAsync(BinaryExpression node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(BinaryExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // CallExpression
        public virtual Task<bool> WalkAsync(CallExpression node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(CallExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // ConditionalExpression
        public virtual Task<bool> WalkAsync(ConditionalExpression node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(ConditionalExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // ConstantExpression
        public virtual Task<bool> WalkAsync(ConstantExpression node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(ConstantExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // DictionaryComprehension
        public virtual Task<bool> WalkAsync(DictionaryComprehension node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(DictionaryComprehension node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // DictionaryExpression
        public virtual Task<bool> WalkAsync(DictionaryExpression node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(DictionaryExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // ErrorExpression
        public virtual Task<bool> WalkAsync(ErrorExpression node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(ErrorExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // ExpressionWithAnnotation
        public virtual Task<bool> WalkAsync(ExpressionWithAnnotation node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(ExpressionWithAnnotation node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // GeneratorExpression
        public virtual Task<bool> WalkAsync(GeneratorExpression node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(GeneratorExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // IndexExpression
        public virtual Task<bool> WalkAsync(IndexExpression node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(IndexExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // LambdaExpression
        public virtual Task<bool> WalkAsync(LambdaExpression node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(LambdaExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // ListComprehension
        public virtual Task<bool> WalkAsync(ListComprehension node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(ListComprehension node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // ListExpression
        public virtual Task<bool> WalkAsync(ListExpression node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(ListExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // MemberExpression
        public virtual Task<bool> WalkAsync(MemberExpression node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(MemberExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // NameExpression
        public virtual Task<bool> WalkAsync(NameExpression node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(NameExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // OrExpression
        public virtual Task<bool> WalkAsync(OrExpression node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(OrExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // ParenthesisExpression
        public virtual Task<bool> WalkAsync(ParenthesisExpression node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(ParenthesisExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // SetComprehension
        public virtual Task<bool> WalkAsync(SetComprehension node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(SetComprehension node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // SetExpression
        public virtual Task<bool> WalkAsync(SetExpression node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(SetExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // SliceExpression
        public virtual Task<bool> WalkAsync(SliceExpression node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(SliceExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // TupleExpression
        public virtual Task<bool> WalkAsync(TupleExpression node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(TupleExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // UnaryExpression
        public virtual Task<bool> WalkAsync(UnaryExpression node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(UnaryExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // YieldExpression
        public virtual Task<bool> WalkAsync(YieldExpression node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(YieldExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // YieldFromExpression
        public virtual Task<bool> WalkAsync(YieldFromExpression node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(YieldFromExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // StarredExpression
        public virtual Task<bool> WalkAsync(StarredExpression node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(StarredExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // AssertStatement
        public virtual Task<bool> WalkAsync(AssertStatement node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(AssertStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // AssignmentStatement
        public virtual Task<bool> WalkAsync(AssignmentStatement node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(AssignmentStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // AugmentedAssignStatement
        public virtual Task<bool> WalkAsync(AugmentedAssignStatement node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(AugmentedAssignStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // BreakStatement
        public virtual Task<bool> WalkAsync(BreakStatement node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(BreakStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // ClassDefinition
        public virtual Task<bool> WalkAsync(ClassDefinition node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(ClassDefinition node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // ContinueStatement
        public virtual Task<bool> WalkAsync(ContinueStatement node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(ContinueStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // DelStatement
        public virtual Task<bool> WalkAsync(DelStatement node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(DelStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // EmptyStatement
        public virtual Task<bool> WalkAsync(EmptyStatement node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(EmptyStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // ExecStatement
        public virtual Task<bool> WalkAsync(ExecStatement node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(ExecStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // ExpressionStatement
        public virtual Task<bool> WalkAsync(ExpressionStatement node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(ExpressionStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // ForStatement
        public virtual Task<bool> WalkAsync(ForStatement node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(ForStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // FromImportStatement
        public virtual Task<bool> WalkAsync(FromImportStatement node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(FromImportStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // FunctionDefinition
        public virtual Task<bool> WalkAsync(FunctionDefinition node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(FunctionDefinition node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // GlobalStatement
        public virtual Task<bool> WalkAsync(GlobalStatement node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(GlobalStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // NonlocalStatement
        public virtual Task<bool> WalkAsync(NonlocalStatement node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(NonlocalStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // IfStatement
        public virtual Task<bool> WalkAsync(IfStatement node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(IfStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // ImportStatement
        public virtual Task<bool> WalkAsync(ImportStatement node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(ImportStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // PrintStatement
        public virtual Task<bool> WalkAsync(PrintStatement node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(PrintStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // PythonAst
        public virtual Task<bool> WalkAsync(PythonAst node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(PythonAst node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // RaiseStatement
        public virtual Task<bool> WalkAsync(RaiseStatement node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(RaiseStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // ReturnStatement
        public virtual Task<bool> WalkAsync(ReturnStatement node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(ReturnStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // SuiteStatement
        public virtual Task<bool> WalkAsync(SuiteStatement node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(SuiteStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // TryStatement
        public virtual Task<bool> WalkAsync(TryStatement node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(TryStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // WhileStatement
        public virtual Task<bool> WalkAsync(WhileStatement node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(WhileStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // WithStatement
        public virtual Task<bool> WalkAsync(WithStatement node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(WithStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // WithItem
        public virtual Task<bool> WalkAsync(WithItem node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(WithItem node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // Arg
        public virtual Task<bool> WalkAsync(Arg node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(Arg node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // ComprehensionFor
        public virtual Task<bool> WalkAsync(ComprehensionFor node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(ComprehensionFor node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // ComprehensionIf
        public virtual Task<bool> WalkAsync(ComprehensionIf node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(ComprehensionIf node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // DottedName
        public virtual Task<bool> WalkAsync(DottedName node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(DottedName node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // IfStatementTest
        public virtual Task<bool> WalkAsync(IfStatementTest node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(IfStatementTest node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // ModuleName
        public virtual Task<bool> WalkAsync(ModuleName node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(ModuleName node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // Parameter
        public virtual Task<bool> WalkAsync(Parameter node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(Parameter node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // RelativeModuleName
        public virtual Task<bool> WalkAsync(RelativeModuleName node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(RelativeModuleName node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // SublistParameter
        public virtual Task<bool> WalkAsync(SublistParameter node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(SublistParameter node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // TryStatementHandler
        public virtual Task<bool> WalkAsync(TryStatementHandler node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(TryStatementHandler node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // ErrorStatement
        public virtual Task<bool> WalkAsync(ErrorStatement node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(ErrorStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // DecoratorStatement
        public virtual Task<bool> WalkAsync(DecoratorStatement node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(DecoratorStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // FString
        public virtual Task<bool> WalkAsync(FString node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(FString node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // FormatSpecifier
        public virtual Task<bool> WalkAsync(FormatSpecifier node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(FormatSpecifier node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // FormattedValue
        public virtual Task<bool> WalkAsync(FormattedValue node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(FormattedValue node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // NamedExpression
        public virtual Task<bool> WalkAsync(NamedExpression node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(NamedExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }


    /// <summary>
    /// PythonWalkerNonRecursive class - The Python AST Walker (default result is false)
    /// </summary>
    public class PythonWalkerNonRecursiveAsync : PythonWalkerAsync {
        // AndExpression
        public override Task<bool> WalkAsync(AndExpression node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(AndExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // AndExpression
        public override Task<bool> WalkAsync(AwaitExpression node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(AwaitExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // BackQuoteExpression
        public override Task<bool> WalkAsync(BackQuoteExpression node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(BackQuoteExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // BinaryExpression
        public override Task<bool> WalkAsync(BinaryExpression node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(BinaryExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // CallExpression
        public override Task<bool> WalkAsync(CallExpression node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(CallExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // ConditionalExpression
        public override Task<bool> WalkAsync(ConditionalExpression node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(ConditionalExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // ConstantExpression
        public override Task<bool> WalkAsync(ConstantExpression node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(ConstantExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // DictionaryComprehension
        public override Task<bool> WalkAsync(DictionaryComprehension node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(DictionaryComprehension node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // DictionaryExpression
        public override Task<bool> WalkAsync(DictionaryExpression node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(DictionaryExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // ErrorExpression
        public override Task<bool> WalkAsync(ErrorExpression node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(ErrorExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // ExpressionWithAnnotation
        public override Task<bool> WalkAsync(ExpressionWithAnnotation node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(ExpressionWithAnnotation node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // GeneratorExpression
        public override Task<bool> WalkAsync(GeneratorExpression node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(GeneratorExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // IndexExpression
        public override Task<bool> WalkAsync(IndexExpression node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(IndexExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // LambdaExpression
        public override Task<bool> WalkAsync(LambdaExpression node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(LambdaExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // ListComprehension
        public override Task<bool> WalkAsync(ListComprehension node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(ListComprehension node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // ListExpression
        public override Task<bool> WalkAsync(ListExpression node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(ListExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // MemberExpression
        public override Task<bool> WalkAsync(MemberExpression node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(MemberExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // NameExpression
        public override Task<bool> WalkAsync(NameExpression node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(NameExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // OrExpression
        public override Task<bool> WalkAsync(OrExpression node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(OrExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // ParenthesisExpression
        public override Task<bool> WalkAsync(ParenthesisExpression node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(ParenthesisExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // SetComprehension
        public override Task<bool> WalkAsync(SetComprehension node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(SetComprehension node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // SetExpression
        public override Task<bool> WalkAsync(SetExpression node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(SetExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // SliceExpression
        public override Task<bool> WalkAsync(SliceExpression node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(SliceExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // TupleExpression
        public override Task<bool> WalkAsync(TupleExpression node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(TupleExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // UnaryExpression
        public override Task<bool> WalkAsync(UnaryExpression node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(UnaryExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // YieldExpression
        public override Task<bool> WalkAsync(YieldExpression node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(YieldExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // YieldFromExpression
        public override Task<bool> WalkAsync(YieldFromExpression node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(YieldFromExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // StarredExpression
        public override Task<bool> WalkAsync(StarredExpression node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(StarredExpression node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // AssertStatement
        public override Task<bool> WalkAsync(AssertStatement node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(AssertStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // AssignmentStatement
        public override Task<bool> WalkAsync(AssignmentStatement node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(AssignmentStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // AugmentedAssignStatement
        public override Task<bool> WalkAsync(AugmentedAssignStatement node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(AugmentedAssignStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // BreakStatement
        public override Task<bool> WalkAsync(BreakStatement node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(BreakStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // ClassDefinition
        public override Task<bool> WalkAsync(ClassDefinition node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(ClassDefinition node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // ContinueStatement
        public override Task<bool> WalkAsync(ContinueStatement node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(ContinueStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // DelStatement
        public override Task<bool> WalkAsync(DelStatement node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(DelStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // EmptyStatement
        public override Task<bool> WalkAsync(EmptyStatement node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(EmptyStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // ExecStatement
        public override Task<bool> WalkAsync(ExecStatement node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(ExecStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // ExpressionStatement
        public override Task<bool> WalkAsync(ExpressionStatement node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(ExpressionStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // ForStatement
        public override Task<bool> WalkAsync(ForStatement node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(ForStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // FromImportStatement
        public override Task<bool> WalkAsync(FromImportStatement node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(FromImportStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // FunctionDefinition
        public override Task<bool> WalkAsync(FunctionDefinition node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(FunctionDefinition node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // GlobalStatement
        public override Task<bool> WalkAsync(GlobalStatement node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(GlobalStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // NonlocalStatement
        public override Task<bool> WalkAsync(NonlocalStatement node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(NonlocalStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // IfStatement
        public override Task<bool> WalkAsync(IfStatement node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(IfStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // ImportStatement
        public override Task<bool> WalkAsync(ImportStatement node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(ImportStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // PrintStatement
        public override Task<bool> WalkAsync(PrintStatement node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(PrintStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // PythonAst
        public override Task<bool> WalkAsync(PythonAst node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(PythonAst node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // RaiseStatement
        public override Task<bool> WalkAsync(RaiseStatement node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(RaiseStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // ReturnStatement
        public override Task<bool> WalkAsync(ReturnStatement node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(ReturnStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // SuiteStatement
        public override Task<bool> WalkAsync(SuiteStatement node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(SuiteStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // TryStatement
        public override Task<bool> WalkAsync(TryStatement node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(TryStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // WhileStatement
        public override Task<bool> WalkAsync(WhileStatement node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(WhileStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // WithStatement
        public override Task<bool> WalkAsync(WithStatement node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(WithStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // WithItem
        public override Task<bool> WalkAsync(WithItem node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(WithItem node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // Arg
        public override Task<bool> WalkAsync(Arg node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(Arg node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // ComprehensionFor
        public override Task<bool> WalkAsync(ComprehensionFor node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(ComprehensionFor node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // ComprehensionIf
        public override Task<bool> WalkAsync(ComprehensionIf node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(ComprehensionIf node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // DottedName
        public override Task<bool> WalkAsync(DottedName node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(DottedName node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // IfStatementTest
        public override Task<bool> WalkAsync(IfStatementTest node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(IfStatementTest node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // ModuleName
        public override Task<bool> WalkAsync(ModuleName node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(ModuleName node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // Parameter
        public override Task<bool> WalkAsync(Parameter node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(Parameter node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // RelativeModuleName
        public override Task<bool> WalkAsync(RelativeModuleName node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(RelativeModuleName node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // SublistParameter
        public override Task<bool> WalkAsync(SublistParameter node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(SublistParameter node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // TryStatementHandler
        public override Task<bool> WalkAsync(TryStatementHandler node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(TryStatementHandler node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // ErrorStatement
        public override Task<bool> WalkAsync(ErrorStatement node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(ErrorStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // DecoratorStatement
        public override Task<bool> WalkAsync(DecoratorStatement node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task PostWalkAsync(DecoratorStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    /// <summary>
    /// PythonWalkerWithLocation class - The Python AST Walker (default result
    /// is true if the node contains Location, otherwise false)
    /// </summary>
    public class PythonWalkerWithLocationAsync : PythonWalkerAsync {
        public readonly int Location;

        private SourceLocation _loc = SourceLocation.Invalid;

        public PythonWalkerWithLocationAsync(int location) {
            Location = location;
        }

        /// <summary>
        /// Required when ExtendedStatements is set.
        /// </summary>
        public PythonAst Tree { get; set; }

        /// <summary>
        /// When enabled, statements will be walked if Location is on the same line.
        /// Note that this may walk multiple statements if they are on the same line. Ensure
        /// your walker state can handle this!
        /// </summary>
        public bool ExtendedStatements { get; set; }

        private bool Contains(Statement stmt) {
            if (Location < stmt.StartIndex) {
                return false;
            }
            if (Location <= stmt.EndIndex) {
                return true;
            }
            if (!ExtendedStatements || Tree == null) {
                return false;
            }
            if (!_loc.IsValid) {
                _loc = Tree.IndexToLocation(Location);
            }
            var start = Tree.IndexToLocation(stmt.StartIndex);
            return _loc.Line == start.Line && _loc.Column > start.Column;
        }

        // AndExpression
        public override Task<bool> WalkAsync(AndExpression node, CancellationToken cancellationToken = default)
            => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // AndExpression
        public override Task<bool> WalkAsync(AwaitExpression node, CancellationToken cancellationToken = default)
            => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // BackQuoteExpression
        public override Task<bool> WalkAsync(BackQuoteExpression node, CancellationToken cancellationToken = default)
            => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // BinaryExpression
        public override Task<bool> WalkAsync(BinaryExpression node, CancellationToken cancellationToken = default)
            => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // CallExpression
        public override Task<bool> WalkAsync(CallExpression node, CancellationToken cancellationToken = default)
            => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // ConditionalExpression
        public override Task<bool> WalkAsync(ConditionalExpression node, CancellationToken cancellationToken = default)
            => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // ConstantExpression
        public override Task<bool> WalkAsync(ConstantExpression node, CancellationToken cancellationToken = default)
            => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // DictionaryComprehension
        public override Task<bool> WalkAsync(DictionaryComprehension node, CancellationToken cancellationToken = default)
            => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // DictionaryExpression
        public override Task<bool> WalkAsync(DictionaryExpression node, CancellationToken cancellationToken = default)
            => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // ErrorExpression
        public override Task<bool> WalkAsync(ErrorExpression node, CancellationToken cancellationToken = default)
            => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // ExpressionWithAnnotation
        public override Task<bool> WalkAsync(ExpressionWithAnnotation node, CancellationToken cancellationToken = default)
            => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // GeneratorExpression
        public override Task<bool> WalkAsync(GeneratorExpression node, CancellationToken cancellationToken = default)
            => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // IndexExpression
        public override Task<bool> WalkAsync(IndexExpression node, CancellationToken cancellationToken = default)
            => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // LambdaExpression
        public override Task<bool> WalkAsync(LambdaExpression node, CancellationToken cancellationToken = default)
            => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // ListComprehension
        public override Task<bool> WalkAsync(ListComprehension node, CancellationToken cancellationToken = default)
            => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // ListExpression
        public override Task<bool> WalkAsync(ListExpression node, CancellationToken cancellationToken = default)
            => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // MemberExpression
        public override Task<bool> WalkAsync(MemberExpression node, CancellationToken cancellationToken = default)
            => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // NameExpression
        public override Task<bool> WalkAsync(NameExpression node, CancellationToken cancellationToken = default)
            => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // OrExpression
        public override Task<bool> WalkAsync(OrExpression node, CancellationToken cancellationToken = default)
            => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // ParenthesisExpression
        public override Task<bool> WalkAsync(ParenthesisExpression node, CancellationToken cancellationToken = default)
            => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // SetComprehension
        public override Task<bool> WalkAsync(SetComprehension node, CancellationToken cancellationToken = default)
            => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // SetExpression
        public override Task<bool> WalkAsync(SetExpression node, CancellationToken cancellationToken = default)
            => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // SliceExpression
        public override Task<bool> WalkAsync(SliceExpression node, CancellationToken cancellationToken = default)
            => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // TupleExpression
        public override Task<bool> WalkAsync(TupleExpression node, CancellationToken cancellationToken = default)
            => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // UnaryExpression
        public override Task<bool> WalkAsync(UnaryExpression node, CancellationToken cancellationToken = default)
            => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // YieldExpression
        public override Task<bool> WalkAsync(YieldExpression node, CancellationToken cancellationToken = default)
            => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // YieldFromExpression
        public override Task<bool> WalkAsync(YieldFromExpression node, CancellationToken cancellationToken = default)
            => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // StarredExpression
        public override Task<bool> WalkAsync(StarredExpression node, CancellationToken cancellationToken = default)
            => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // AssertStatement
        public override Task<bool> WalkAsync(AssertStatement node, CancellationToken cancellationToken = default)
            => Task.FromResult(Contains(node));

        // AssignmentStatement
        public override Task<bool> WalkAsync(AssignmentStatement node, CancellationToken cancellationToken = default)
            => Task.FromResult(Contains(node));

        // AugmentedAssignStatement
        public override Task<bool> WalkAsync(AugmentedAssignStatement node, CancellationToken cancellationToken = default)
            => Task.FromResult(Contains(node));

        // BreakStatement
        public override Task<bool> WalkAsync(BreakStatement node, CancellationToken cancellationToken = default)
            => Task.FromResult(Contains(node));

        // ClassDefinition
        public override Task<bool> WalkAsync(ClassDefinition node, CancellationToken cancellationToken = default)
            => Task.FromResult(Contains(node));

        // ContinueStatement
        public override Task<bool> WalkAsync(ContinueStatement node, CancellationToken cancellationToken = default)
            => Task.FromResult(Contains(node));

        // DelStatement
        public override Task<bool> WalkAsync(DelStatement node, CancellationToken cancellationToken = default)
            => Task.FromResult(Contains(node));

        // EmptyStatement
        public override Task<bool> WalkAsync(EmptyStatement node, CancellationToken cancellationToken = default)
            => Task.FromResult(Contains(node));

        // ExecStatement
        public override Task<bool> WalkAsync(ExecStatement node, CancellationToken cancellationToken = default)
            => Task.FromResult(Contains(node));

        // ExpressionStatement
        public override Task<bool> WalkAsync(ExpressionStatement node, CancellationToken cancellationToken = default)
            => Task.FromResult(Contains(node));

        // ForStatement
        public override Task<bool> WalkAsync(ForStatement node, CancellationToken cancellationToken = default)
            => Task.FromResult(Contains(node));

        // FromImportStatement
        public override Task<bool> WalkAsync(FromImportStatement node, CancellationToken cancellationToken = default)
            => Task.FromResult(Contains(node));

        // FunctionDefinition
        public override Task<bool> WalkAsync(FunctionDefinition node, CancellationToken cancellationToken = default)
            => Task.FromResult(Contains(node));

        // GlobalStatement
        public override Task<bool> WalkAsync(GlobalStatement node, CancellationToken cancellationToken = default)
            => Task.FromResult(Contains(node));

        // NonlocalStatement
        public override Task<bool> WalkAsync(NonlocalStatement node, CancellationToken cancellationToken = default)
            => Task.FromResult(Contains(node));

        // IfStatement
        public override Task<bool> WalkAsync(IfStatement node, CancellationToken cancellationToken = default)
            => Task.FromResult(Contains(node));

        // ImportStatement
        public override Task<bool> WalkAsync(ImportStatement node, CancellationToken cancellationToken = default)
            => Task.FromResult(Contains(node));

        // PrintStatement
        public override Task<bool> WalkAsync(PrintStatement node, CancellationToken cancellationToken = default)
            => Task.FromResult(Contains(node));

        // PythonAst
        public override Task<bool> WalkAsync(PythonAst node, CancellationToken cancellationToken = default)
            => Task.FromResult(Contains(node));

        // RaiseStatement
        public override Task<bool> WalkAsync(RaiseStatement node, CancellationToken cancellationToken = default)
            => Task.FromResult(Contains(node));

        // ReturnStatement
        public override Task<bool> WalkAsync(ReturnStatement node, CancellationToken cancellationToken = default)
            => Task.FromResult(Contains(node));

        // SuiteStatement
        public override Task<bool> WalkAsync(SuiteStatement node, CancellationToken cancellationToken = default)
            => Task.FromResult(Contains(node));

        // TryStatement
        public override Task<bool> WalkAsync(TryStatement node, CancellationToken cancellationToken = default)
            => Task.FromResult(Contains(node));

        // WhileStatement
        public override Task<bool> WalkAsync(WhileStatement node, CancellationToken cancellationToken = default)
            => Task.FromResult(Contains(node));

        // WithStatement
        public override Task<bool> WalkAsync(WithStatement node, CancellationToken cancellationToken = default)
            => Task.FromResult(Contains(node));

        // WithItem
        public override Task<bool> WalkAsync(WithItem node, CancellationToken cancellationToken = default)
            => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // Arg
        public override Task<bool> WalkAsync(Arg node, CancellationToken cancellationToken = default)
            => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // ComprehensionFor
        public override Task<bool> WalkAsync(ComprehensionFor node, CancellationToken cancellationToken = default)
            => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // ComprehensionIf
        public override Task<bool> WalkAsync(ComprehensionIf node, CancellationToken cancellationToken = default)
            => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // DottedName
        public override Task<bool> WalkAsync(DottedName node, CancellationToken cancellationToken = default)
            => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // IfStatementTest
        public override Task<bool> WalkAsync(IfStatementTest node, CancellationToken cancellationToken = default)
            => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // ModuleName
        public override Task<bool> WalkAsync(ModuleName node, CancellationToken cancellationToken = default)
            => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // Parameter
        public override Task<bool> WalkAsync(Parameter node, CancellationToken cancellationToken = default)
            => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // RelativeModuleName
        public override Task<bool> WalkAsync(RelativeModuleName node, CancellationToken cancellationToken = default)
            => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // SublistParameter
        public override Task<bool> WalkAsync(SublistParameter node, CancellationToken cancellationToken = default)
            => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // TryStatementHandler
        public override Task<bool> WalkAsync(TryStatementHandler node, CancellationToken cancellationToken = default)
            => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // ErrorStatement
        public override Task<bool> WalkAsync(ErrorStatement node, CancellationToken cancellationToken = default)
            => Task.FromResult(Contains(node));

        // DecoratorStatement
        public override Task<bool> WalkAsync(DecoratorStatement node, CancellationToken cancellationToken = default)
            => Task.FromResult(Contains(node));

        // FString
        public override Task<bool> WalkAsync(FString node, CancellationToken cancellationToken = default)
            => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // FormatSpecifier
        public override Task<bool> WalkAsync(FormatSpecifier node, CancellationToken cancellationToken = default)
            => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // FormattedValue
        public override Task<bool> WalkAsync(FormattedValue node, CancellationToken cancellationToken = default)
            => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);

        // NamedExpression
        public override Task<bool> WalkAsync(NamedExpression node, CancellationToken cancellationToken = default)
            => Task.FromResult(Location >= node.StartIndex && Location <= node.EndIndex);
    }
}
