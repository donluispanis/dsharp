﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSharp.Compiler.CodeModel.Attributes;
using DSharp.Compiler.CodeModel.Expressions;
using DSharp.Compiler.CodeModel.Members;
using DSharp.Compiler.CodeModel.Names;
using DSharp.Compiler.CodeModel.Statements;
using DSharp.Compiler.CodeModel.Tokens;
using DSharp.Compiler.CodeModel.Types;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DSharp.Compiler.CodeModel
{
    internal class RoslynCodeModelBuilder : ICodeModelBuilder
    {
        private readonly CSharpParseOptions parseOptions;

        public RoslynCodeModelBuilder(CompilerOptions options)
        {
            parseOptions = new CSharpParseOptions(
                LanguageVersion.CSharp3,
                documentationMode: options.EnableDocComments ? DocumentationMode.Parse : DocumentationMode.None,
                kind: SourceCodeKind.Regular,
                preprocessorSymbols: options.Defines);
        }

        public CompilationUnitNode BuildCodeModel(IStreamSource source)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(LoadText(source), options: parseOptions);
            var root = syntaxTree.GetCompilationUnitRoot();

            var parsedUsings = ParseUsings(root.Usings);
            var parsedAttributes = ParseAttributes(root.AttributeLists);
            var parsedNamespaceMembers = ParseMembers(root);

            return new CompilationUnitNode(
                token: null, //Should be BOF, but does it matter!?
                externAliases: new ParseNodeList(), //Not actually sure what goes in here
                usingClauses: parsedUsings,
                attributes: parsedAttributes,
                members: parsedNamespaceMembers);
        }

        private static List<ParseNode> ParseMembers(SyntaxNode node)
        {
            List<ParseNode> parsedMembers = new List<ParseNode>();

            foreach (var member in node.ChildNodes())
            {
                switch (member)
                {
                    case NamespaceDeclarationSyntax namespaceDeclaration:
                        NamespaceNode namespaceNode = ParseNamespace(namespaceDeclaration);
                        parsedMembers.Add(namespaceNode);
                        break;
                    case ClassDeclarationSyntax classDeclarationSyntax:
                        CustomTypeNode classNode = ParseCustomTypeDefinition(TokenType.Class, classDeclarationSyntax);
                        parsedMembers.Add(classNode);
                        break;
                    case InterfaceDeclarationSyntax interfaceDeclarationSyntax:
                        CustomTypeNode interfaceNode = ParseCustomTypeDefinition(TokenType.Interface, interfaceDeclarationSyntax);
                        parsedMembers.Add(interfaceNode);
                        break;
                    case StructDeclarationSyntax structDeclarationSyntax:
                        CustomTypeNode structNode = ParseCustomTypeDefinition(TokenType.Struct, structDeclarationSyntax);
                        parsedMembers.Add(structNode);
                        break;
                    case TypeDeclarationSyntax typeDeclarationSyntax:
                        break;
                    default:
                        break;
                }
            }

            return parsedMembers;
        }

        private static CustomTypeNode ParseCustomTypeDefinition(TokenType type, TypeDeclarationSyntax typeDeclarationSyntax, bool isNestedType = false)
        {
            var attributes = new ParseNodeList(ParseAttributes(typeDeclarationSyntax.AttributeLists));
            var name = CreateAtomicName(typeDeclarationSyntax.Identifier);
            var modifiers = ParseModifiers(typeDeclarationSyntax.Modifiers);
            var typeParameters = ParseTypeParameters(typeDeclarationSyntax.TypeParameterList);
            var baseTypes = ParseBaseTypes(typeDeclarationSyntax.BaseList);
            var constraints = ParseConstraints(typeDeclarationSyntax.ConstraintClauses);
            var members = ParseTypeMembers(typeDeclarationSyntax.Members);

            var classNode = new CustomTypeNode(
                null,
                type,
                attributes,
                modifiers, // Parse actual modifiers
                name,
                typeParameters: typeParameters,
                baseTypes: baseTypes,
                constraintClauses: constraints,
                members: members,
                isNestedType: isNestedType);
            return classNode;
        }

        private static ParseNodeList ParseTypeMembers(SyntaxList<MemberDeclarationSyntax> members)
        {
            ParseNodeList parseNodes = new ParseNodeList();

            foreach (var member in members)
            {
                var attributes = ParseAttributes(member.AttributeLists);
                var modifiers = ParseModifiers(member.Modifiers);
                switch (member)
                {
                    case FieldDeclarationSyntax fieldDeclarationSyntax:
                        var field = new FieldDeclarationNode(null, attributes, modifiers, null, null, false);
                        parseNodes.Add(field);
                        break;
                    case BaseFieldDeclarationSyntax baseFieldDeclarationSyntax: //Is this actually a thing?
                        break;
                    case ConstructorDeclarationSyntax constructorDeclarationSyntax:
                        break;
                    case DestructorDeclarationSyntax destructorDeclarationSyntax:
                        break;
                    case IndexerDeclarationSyntax indexerDeclarationSyntax:
                        break;
                    case EventDeclarationSyntax eventDeclarationSyntax:
                        break;
                    case PropertyDeclarationSyntax propertyDeclarationSyntax:
                        break;
                    case MethodDeclarationSyntax methodDeclarationSyntax:
                        MethodDeclarationNode method = ParseMethod(methodDeclarationSyntax, attributes, modifiers);
                        parseNodes.Add(method);
                        break;
                    case OperatorDeclarationSyntax operatorDeclarationSyntax:
                        break;
                    case ClassDeclarationSyntax classDeclarationSyntax:
                        CustomTypeNode classNode = ParseCustomTypeDefinition(TokenType.Class, classDeclarationSyntax, true);
                        parseNodes.Add(classNode);
                        break;
                    case InterfaceDeclarationSyntax interfaceDeclarationSyntax:
                        CustomTypeNode interfaceNode = ParseCustomTypeDefinition(TokenType.Interface, interfaceDeclarationSyntax, true);
                        parseNodes.Add(interfaceNode);
                        break;
                    case StructDeclarationSyntax structDeclarationSyntax:
                        CustomTypeNode structNode = ParseCustomTypeDefinition(TokenType.Struct, structDeclarationSyntax, true);
                        parseNodes.Add(structNode);
                        break;
                    default:
                        throw new Exception("Invalid member!");
                }
            }

            return parseNodes;
        }

        private static MethodDeclarationNode ParseMethod(MethodDeclarationSyntax methodDeclarationSyntax, ParseNodeList attributes, Modifiers modifiers)
        {
            var returnType = ParseTypeName(methodDeclarationSyntax.ReturnType);
            var name = CreateAtomicName(methodDeclarationSyntax.Identifier);
            var typeParameters = ParseTypeParameters(methodDeclarationSyntax.TypeParameterList);
            var constraints = ParseConstraints(methodDeclarationSyntax.ConstraintClauses);
            var interfaceType = ParseExplicitInterfaceType(methodDeclarationSyntax);
            var parameters = ParseParameters(methodDeclarationSyntax.ParameterList);
            var body = ParseBlockStatement(methodDeclarationSyntax.Body);

            var method = new MethodDeclarationNode(
                null,
                attributes,
                modifiers,
                returnType: returnType,
                interfaceType: interfaceType,
                name: name,
                typeParameters: typeParameters,
                parameters: parameters,
                constraints: constraints,
                body: body);
            return method;
        }

        private static BlockStatementNode ParseBlockStatement(BlockSyntax body)
        {
            ParseNodeList statements = new ParseNodeList();

            foreach (var statement in body.Statements)
            {
                var parsedStatement = ParseStatement(statement);
                statements.Add(parsedStatement);
            }

            return new BlockStatementNode(null, statements);
        }

        private static StatementNode ParseStatement(StatementSyntax statement)
        {
            switch (statement)
            {
                case BreakStatementSyntax breakStatement:
                    return new BreakNode(CreateTokenFromSyntax(breakStatement.BreakKeyword, TokenType.Break));
                case BlockSyntax blockSyntax:
                    return ParseBlockStatement(blockSyntax);
                case LocalDeclarationStatementSyntax localDeclarationStatement:
                    return ParseDeclerationStatement(localDeclarationStatement);
                case ExpressionStatementSyntax expressionStatement:
                    return new ExpressionStatementNode(ParseExpression(expressionStatement.Expression));
                case ReturnStatementSyntax returnStatementSyntax:
                    return new ReturnNode(null, ParseExpression(returnStatementSyntax.Expression));
                default:
                    throw new NotImplementedException();
            }
        }

        private static StatementNode ParseDeclerationStatement(LocalDeclarationStatementSyntax localDeclarationStatement)
        {
            var modifiers = ParseModifiers(localDeclarationStatement.Modifiers);
            var type = ParseTypeName(localDeclarationStatement.Declaration.Type);

            ParseNodeList variableInitializers = new ParseNodeList();
            foreach (var variable in localDeclarationStatement.Declaration.Variables)
            {
                var name = CreateAtomicName(variable.Identifier);
                var initializer = ParseVariableInitializer(variable.Initializer);
                var initilizerNode = new VariableInitializerNode(name, initializer);
                variableInitializers.Add(initilizerNode);
            }

            if (localDeclarationStatement.Modifiers.Contains(SyntaxFactory.Token(SyntaxKind.ConstKeyword)))
            {
                return new ConstantDeclarationNode(null, new ParseNodeList(), Modifiers.None, type, variableInitializers);
            }

            return new VariableDeclarationNode(null, new ParseNodeList(), modifiers, type, variableInitializers, false);
        }

        private static ParseNode ParseVariableInitializer(EqualsValueClauseSyntax initializer)
        {
            switch (initializer.Value)
            {
                case StackAllocArrayCreationExpressionSyntax stackAlloc:
                case ImplicitStackAllocArrayCreationExpressionSyntax implicitStack:
                    throw new NotSupportedException();
                case ArrayCreationExpressionSyntax arrayCreation:
                    return new ArrayInitializerNode(null, null); //Implement
                default:
                    return ParseExpression(initializer.Value);
            }
        }

        private static NameNode ParseExplicitInterfaceType(MethodDeclarationSyntax methodDeclarationSyntax)
        {
            if (methodDeclarationSyntax.ExplicitInterfaceSpecifier != null)
            {
                return ParseNameNode(methodDeclarationSyntax.ExplicitInterfaceSpecifier.Name);
            }

            return null;
        }

        private static ParseNodeList ParseParameters(ParameterListSyntax parameterList)
        {
            ParseNodeList parseNodes = new ParseNodeList();

            foreach (var parameter in parameterList.Parameters)
            {
                ParameterNode parameterNode = ParseParameter(parameter);
                parseNodes.Add(parameterNode);
            }

            return parseNodes;
        }

        private static ParameterNode ParseParameter(ParameterSyntax parameter)
        {
            var attributes = ParseAttributes(parameter.AttributeLists);
            ParameterFlags parameterFlags = ParameterFlags.None; //Parse the modifiers or keywords
            var typeName = ParseTypeName(parameter.Type);
            var name = CreateAtomicName(parameter.Identifier);

            var isExtensionMethod = parameter.Modifiers.Contains(SyntaxFactory.Token(SyntaxKind.ThisKeyword));

            ParameterNode parameterNode = new ParameterNode(
                null,
                attributes,
                parameterFlags,
                typeName,
                name,
                isExtensionMethodTarget: isExtensionMethod);
            return parameterNode;
        }

        private static ParseNode ParseTypeName(TypeSyntax typeSyntax)
        {
            if (typeSyntax is IdentifierNameSyntax identifierNameSyntax)
            {
                return CreateAtomicName(identifierNameSyntax.Identifier);
            }
            else if (typeSyntax is PredefinedTypeSyntax predefinedType)
            {
                return ParsePredefinedType(predefinedType.Keyword);
            }
            else if (typeSyntax is GenericNameSyntax genericNameSyntax)
            {
                return new GenericNameNode(
                    CreateIdentifier(genericNameSyntax.Identifier),
                    ParseTypeArguments(genericNameSyntax.TypeArgumentList));
            }
            else if(typeSyntax == null) //Var or anonymous usages
            {
                var varToken = CreateTokenFromSyntax(SyntaxFactory.Token(SyntaxKind.VarKeyword));
                return new IntrinsicTypeNode(varToken);
            }

            throw new Exception();
        }

        private static ParseNodeList ParseTypeArguments(TypeArgumentListSyntax typeArgumentList)
        {
            ParseNodeList parseNodes = new ParseNodeList();

            foreach (var typeArgument in typeArgumentList.Arguments)
            {
                parseNodes.Add(ParseTypeName(typeArgument));
            }

            return parseNodes;
        }

        private static ParseNode ParsePredefinedType(SyntaxToken keyword)
        {
            var position = GetTokenPosition(keyword, out string path);

            switch (keyword.ValueText)
            {
                case "void":
                    return new IntrinsicTypeNode(new Token(TokenType.Void, path, position));
                case "string":
                    return new IntrinsicTypeNode(new Token(TokenType.String, path, position));
                case "bool":
                    return new IntrinsicTypeNode(new Token(TokenType.Bool, path, position));
                case "ushort":
                    return new IntrinsicTypeNode(new Token(TokenType.UShort, path, position));
                case "short":
                    return new IntrinsicTypeNode(new Token(TokenType.Short, path, position));
                case "int":
                    return new IntrinsicTypeNode(new Token(TokenType.Int, path, position));
                case "uint":
                    return new IntrinsicTypeNode(new Token(TokenType.UInt, path, position));
                case "long":
                    return new IntrinsicTypeNode(new Token(TokenType.Long, path, position));
                case "ulong":
                    return new IntrinsicTypeNode(new Token(TokenType.ULong, path, position));
                case "float":
                    return new IntrinsicTypeNode(new Token(TokenType.Float, path, position));
                case "double":
                    return new IntrinsicTypeNode(new Token(TokenType.Double, path, position));
                case "decimal":
                    return new IntrinsicTypeNode(new Token(TokenType.Decimal, path, position));
                case "object":
                    return new IntrinsicTypeNode(new Token(TokenType.Object, path, position));
                default:
                    throw new NotSupportedException();
            }
        }

        private static ParseNodeList ParseConstraints(SyntaxList<TypeParameterConstraintClauseSyntax> constraintClauses)
        {
            ParseNodeList parseNodes = new ParseNodeList();

            foreach (var constraintClause in constraintClauses)
            {
                //We don't really care about this.
                var name = ParseIdentifier(constraintClause.Name);
                var constraint = new TypeParameterConstraintNode((AtomicNameNode)name, new ParseNodeList(), false);
                parseNodes.Add(constraint);
            }

            return parseNodes;
        }

        private static ParseNodeList ParseBaseTypes(BaseListSyntax baseList)
        {
            ParseNodeList parseNodes = new ParseNodeList();
            if (baseList == null)
            {
                return parseNodes;
            }

            foreach (var type in baseList.Types)
            {
                if (type is SimpleBaseTypeSyntax simpleBaseTypeSyntax)
                {
                    var name = ParseTypeName(simpleBaseTypeSyntax.Type);
                    parseNodes.Add(name);
                }
                else
                {
                    throw new Exception();
                }
            }

            return parseNodes;
        }

        private static ParseNodeList ParseTypeParameters(TypeParameterListSyntax typeParameterList)
        {
            ParseNodeList parseNodes = new ParseNodeList();

            if (typeParameterList == null)
            {
                return parseNodes;
            }

            foreach (var typeParameterSyntax in typeParameterList.Parameters)
            {
                var attributes = ParseAttributes(typeParameterSyntax.AttributeLists);
                var name = CreateAtomicName(typeParameterSyntax.Identifier);
                var typeParameter = new TypeParameterNode(attributes, name);

                parseNodes.Add(typeParameter);
            }

            return parseNodes;
        }

        private static Modifiers ParseModifiers(SyntaxTokenList modifiers)
        {
            var parsedModifier = Modifiers.None;
            foreach (var modifier in modifiers)
            {
                switch (modifier.Kind())
                {
                    case SyntaxKind.PublicKeyword:
                        parsedModifier |= Modifiers.Public;
                        break;
                    case SyntaxKind.InternalKeyword:
                        parsedModifier |= Modifiers.Internal;
                        break;
                    case SyntaxKind.PrivateKeyword:
                        parsedModifier |= Modifiers.Private;
                        break;
                    case SyntaxKind.ProtectedKeyword:
                        parsedModifier |= Modifiers.Protected;
                        break;
                    case SyntaxKind.PartialKeyword:
                        parsedModifier |= Modifiers.Partial;
                        break;
                    case SyntaxKind.AbstractKeyword:
                        parsedModifier |= Modifiers.Abstract;
                        break;
                    case SyntaxKind.StaticKeyword:
                        parsedModifier |= Modifiers.Static;
                        break;
                    case SyntaxKind.SealedKeyword:
                        parsedModifier |= Modifiers.Sealed;
                        break;
                    case SyntaxKind.ConstKeyword:
                        break;
                    default:
                        throw new Exception();
                }
            }

            return parsedModifier;
        }

        private static NamespaceNode ParseNamespace(NamespaceDeclarationSyntax namespaceDeclaration)
        {
            var nameNode = ParseNameNode(namespaceDeclaration.Name);
            var usings = new ParseNodeList(ParseUsings(namespaceDeclaration.Usings));
            var members = ParseMembers(namespaceDeclaration);
            var namespaceNode = new NamespaceNode(null, nameNode, new ParseNodeList(), usings, new ParseNodeList(members));
            return namespaceNode;
        }

        private static List<ParseNode> ParseAttributes(IEnumerable<AttributeListSyntax> attributeLists)
        {
            List<ParseNode> attributes = new List<ParseNode>();
            foreach (AttributeListSyntax attributeList in attributeLists)
            {
                List<AttributeNode> attributeNodes = new List<AttributeNode>();
                var target = attributeList.Target;
                foreach (var attribute in attributeList.Attributes)
                {
                    var name = ParseNameNode(attribute.Name);

                    attributeNodes.Add(new AttributeNode(name, ParseAttributesArguments(attribute.ArgumentList)));
                }

                var attributeBlockNode = new AttributeBlockNode(null, new ParseNodeList(attributeNodes));
            }

            return attributes;
        }

        private static ExpressionListNode ParseAttributesArguments(AttributeArgumentListSyntax argumentList)
        {
            List<ParseNode> expressions = new List<ParseNode>();

            foreach (var argument in argumentList.Arguments)
            {
                var expression = ParseExpression(argument.Expression);
                expressions.Add(expression);
            }

            return new ExpressionListNode(null, new ParseNodeList(expressions));
        }

        private static ParseNode ParseExpression(ExpressionSyntax expressionSyntax)
        {
            switch (expressionSyntax)
            {
                case LiteralExpressionSyntax literalExpression:
                    return new LiteralNode(ParseLiteralExpressionAsToken(literalExpression));
                case MemberAccessExpressionSyntax memberAccessExpressionSyntax:
                    return new BinaryExpressionNode(ParseExpression(memberAccessExpressionSyntax.Expression), TokenType.Dot, ParseSimpleName(memberAccessExpressionSyntax.Name));
                case IdentifierNameSyntax identifierNameSyntax:
                    return ParseIdentifier(identifierNameSyntax);
                case ObjectCreationExpressionSyntax objectCreationExpressionSyntax:
                    var typeReference = ParseTypeName(objectCreationExpressionSyntax.Type);
                    var arguments = ParseArguments(objectCreationExpressionSyntax.ArgumentList);
                    return new NewNode(ParseTokenFromSyntaxTokenKind(SyntaxKind.NewKeyword), typeReference, arguments);
                case LambdaExpressionSyntax lambdaExpressionSyntax:
                    return ParseLambdaExpression(lambdaExpressionSyntax);
                case InvocationExpressionSyntax invocationExpressionSyntax:
                    return ParseInvocationExpression(invocationExpressionSyntax);
                case PrefixUnaryExpressionSyntax prefixUnaryExpressionSyntax:
                    return new UnaryExpressionNode(
                        CreateTokenFromSyntax(prefixUnaryExpressionSyntax.OperatorToken),
                        ParseExpression(prefixUnaryExpressionSyntax.Operand));
                case BinaryExpressionSyntax binaryExpressionSyntax:
                    return new BinaryExpressionNode(
                        ParseExpression(binaryExpressionSyntax.Left),
                        ResolveSyntaxTokenType(binaryExpressionSyntax.OperatorToken),
                        ParseExpression(binaryExpressionSyntax.Right));
                default:
                    throw new NotSupportedException(expressionSyntax.GetType().Name);
            }
        }

        private static ParseNode ParseInvocationExpression(InvocationExpressionSyntax invocationExpressionSyntax)
        {
            return new BinaryExpressionNode(
                ParseExpression(invocationExpressionSyntax.Expression),
                TokenType.OpenParen,
                ParseArguments(invocationExpressionSyntax.ArgumentList));
        }

        private static ParseNode ParseLambdaExpression(LambdaExpressionSyntax lambdaExpressionSyntax)
        {
            var parameters = new ParseNodeList();
            if (lambdaExpressionSyntax is ParenthesizedLambdaExpressionSyntax parenthesizedLambda)
            {
                parameters = ParseParameters(parenthesizedLambda.ParameterList);
            }
            else if (lambdaExpressionSyntax is SimpleLambdaExpressionSyntax simpleLambdaExpressionSyntax)
            {
                parameters = new ParseNodeList(ParseParameter(simpleLambdaExpressionSyntax.Parameter));
            }

            BlockStatementNode body = null;
            if(lambdaExpressionSyntax.Body is BlockSyntax block)
            {
                body = ParseBlockStatement(block);
            }
            else if (lambdaExpressionSyntax.Body is ExpressionSyntax expressionSyntax)
            {
                var parsedExpression = ParseExpression(expressionSyntax);
                var statements = new ParseNodeList(new ExpressionStatementNode(parsedExpression));
                body = new BlockStatementNode(null, statements);
            }

            return new AnonymousMethodNode(null, parameters, body);
        }

        private static ExpressionListNode ParseArguments(ArgumentListSyntax argumentList)
        {
            ParseNodeList parseNodes = new ParseNodeList();
            foreach (var argument in argumentList.Arguments)
            {
                var parsedExpression = ParseExpression(argument.Expression);

                if (argument.RefKindKeyword.Kind() == SyntaxKind.OutKeyword)
                {
                    parsedExpression = new UnaryExpressionNode(
                        CreateTokenFromSyntax(argument.RefKindKeyword, TokenType.Out),
                        parsedExpression);
                }

                parseNodes.Add(parsedExpression);
            }

            return new ExpressionListNode(null, parseNodes);
        }

        private static LiteralToken ParseLiteralExpressionAsToken(LiteralExpressionSyntax literalExpression)
        {
            var position = GetTokenPosition(literalExpression.Token, out string path);

            switch (literalExpression.Kind())
            {
                case SyntaxKind.StringLiteralExpression:
                    return new StringToken(literalExpression.Token.ValueText, path, position);
                case SyntaxKind.NumericLiteralExpression:
                    switch (literalExpression.Token.Value)
                    {
                        case ushort uint16:
                            return new UIntToken(uint16, path, position);
                        case short int16:
                            return new IntToken(int16, path, position);
                        case uint uint32:
                            return new UIntToken(uint32, path, position);
                        case int int32:
                            return new IntToken(int32, path, position);
                        case ulong uint64:
                            return new ULongToken(uint64, path, position);
                        case long int64:
                            return new LongToken(int64, path, position);
                        case float floatValue:
                            return new FloatToken(floatValue, path, position);
                        case double doubleValue:
                            return new DoubleToken(doubleValue, path, position);
                        case decimal decimalValue:
                            return new DecimalToken(decimalValue, path, position);
                        default:
                            throw new Exception();
                    }
                case SyntaxKind.FalseLiteralExpression:
                case SyntaxKind.TrueLiteralExpression:
                    return new BooleanToken((bool)literalExpression.Token.Value, path, position);
                case SyntaxKind.NullLiteralExpression:
                    return new NullToken(path, position);
                default:
                    throw new NotSupportedException(literalExpression.Kind().ToString());
            }
        }

        private static List<ParseNode> ParseUsings(IEnumerable<UsingDirectiveSyntax> usings)
        {
            List<ParseNode> parsedUsings = new List<ParseNode>();

            foreach (var usingNode in usings)
            {
                NameNode namespaceNameNode = ParseNameNode(usingNode.Name);
                if (usingNode.Alias != null)
                {
                    var name = CreateAtomicName(usingNode.Alias.Name.Identifier);
                    var usingAlias = new UsingAliasNode(null, name, namespaceNameNode);
                    parsedUsings.Add(usingAlias);
                }
                else
                {
                    var usingNamespace = new UsingNamespaceNode(null, namespaceNameNode);
                    parsedUsings.Add(usingNamespace);
                }
            }

            return parsedUsings;
        }

        private static NameNode ParseNameNode(NameSyntax nameSyntax)
        {
            if (nameSyntax is QualifiedNameSyntax)
            {
                List<ParseNode> parseNodes = new List<ParseNode>();
                NameSyntax current = nameSyntax;
                while (current != null)
                {
                    if (current is QualifiedNameSyntax qualifiedNameSyntax)
                    {
                        var nameNode = ParseSimpleName(qualifiedNameSyntax.Right);
                        parseNodes.Add(nameNode);
                        current = qualifiedNameSyntax.Left;
                        continue;
                    }

                    //Must be an identifer
                    var identifier = ParseIdentifier(current as IdentifierNameSyntax);
                    parseNodes.Add(identifier);
                    break;
                }

                return new MultiPartNameNode(null, new ParseNodeList(parseNodes.Reverse<ParseNode>()));
            }
            else if (nameSyntax is IdentifierNameSyntax identifierNameSyntax)
            {
                return ParseIdentifier(identifierNameSyntax);
            }

            throw new Exception("We can't get here?!");
        }

        private static NameNode ParseSimpleName(SimpleNameSyntax simpleNameSyntax)
        {
            return CreateAtomicName(simpleNameSyntax.Identifier);
        }

        private static NameNode ParseIdentifier(IdentifierNameSyntax identifierNameSyntax)
        {
            return CreateAtomicName(identifierNameSyntax.Identifier);
        }

        private static AtomicNameNode CreateAtomicName(SyntaxToken syntaxToken)
        {
            var position = GetTokenPosition(syntaxToken, out string path);
            var nameNode = new Parser.Name(syntaxToken.ValueText);
            return new AtomicNameNode(new Tokens.IdentifierToken(nameNode, nameNode.Text.StartsWith("@"), path, position));
        }

        private static IdentifierToken CreateIdentifier(SyntaxToken syntaxToken)
        {
            var position = GetTokenPosition(syntaxToken, out string path);
            var nameNode = new Parser.Name(syntaxToken.ValueText);
            return new IdentifierToken(nameNode, false, path, position);
        }

        private static Parser.BufferPosition GetTokenPosition(SyntaxToken syntaxToken, out string path)
        {
            var location = syntaxToken.GetLocation();
            if(location.Kind == LocationKind.None)
            {
                path = string.Empty;
                return new Parser.BufferPosition(0, 0, 0);
            }

            var lineSpan = location.GetLineSpan();
            path = location.SourceTree.FilePath;
            return new Parser.BufferPosition(lineSpan.StartLinePosition.Line, lineSpan.StartLinePosition.Character, 0);
        }

        //TODO: Make extension method
        //TODO: Make it handle it's type internally
        private static Token CreateTokenFromSyntax(SyntaxToken syntaxToken, TokenType? tokenType = null)
        {
            tokenType = tokenType ?? ResolveSyntaxTokenType(syntaxToken);
            var position = GetTokenPosition(syntaxToken, out string path);
            return new Token(tokenType.Value, path, position);
        }

        private static TokenType ResolveSyntaxTokenType(SyntaxToken syntaxToken)
        {
            switch (syntaxToken.Kind())
            {
                case SyntaxKind.MinusToken:
                    return TokenType.Minus;
                case SyntaxKind.MinusMinusToken:
                    return TokenType.MinusMinus;
                case SyntaxKind.PlusToken:
                    return TokenType.Plus;
                case SyntaxKind.PlusPlusToken:
                    return TokenType.PlusPlus;
                case SyntaxKind.VarKeyword:
                    return TokenType.Var;
                case SyntaxKind.NewKeyword:
                    return TokenType.New;
                default: throw new NotSupportedException();
            }
        }

        private static Token ParseTokenFromSyntaxTokenKind(SyntaxKind syntaxKind)
        {
            return CreateTokenFromSyntax(SyntaxFactory.Token(syntaxKind));
        }

        private string LoadText(IStreamSource source)
        {
            Stream stream = source.GetStream();
            string text = string.Empty;
            if (stream != null)
            {
                StreamReader reader = new StreamReader(stream);
                text = reader.ReadToEnd();

                source.CloseStream(stream);
            }

            return text;
        }
    }
}
