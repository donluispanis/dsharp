// MethodDeclarationNode.cs
// Script#/Core/Compiler
// This source code is subject to terms and conditions of the Apache License, Version 2.0.
//

using System.Linq;
using DSharp.Compiler.CodeModel.Attributes;
using DSharp.Compiler.CodeModel.Names;
using DSharp.Compiler.CodeModel.Statements;
using DSharp.Compiler.CodeModel.Tokens;
using DSharp.Compiler.CodeModel.Types;

namespace DSharp.Compiler.CodeModel.Members
{
    internal class MethodDeclarationNode : MemberNode
    {
        private readonly NameNode interfaceType;
        private readonly AtomicNameNode name;
        private ParseNodeList constraints;
        private bool isOverloadedMethod;

        public MethodDeclarationNode(Token token,
                                     ParseNodeList attributes,
                                     Modifiers modifiers,
                                     ParseNode returnType,
                                     NameNode interfaceType,
                                     AtomicNameNode name,
                                     ParseNodeList typeParameters,
                                     ParseNodeList formals,
                                     ParseNodeList constraints,
                                     BlockStatementNode body)
            : this(ParseNodeType.MethodDeclaration, token, attributes, modifiers, returnType, name, formals, body)
        {
            this.interfaceType = (NameNode) GetParentedNode(interfaceType);
            this.TypeParameters = GetParentedNodeList(typeParameters);
            this.constraints = GetParentedNodeList(constraints);
        }

        protected MethodDeclarationNode(ParseNodeType nodeType, Token token,
                                        ParseNodeList attributes,
                                        Modifiers modifiers,
                                        ParseNode returnType,
                                        AtomicNameNode name,
                                        ParseNodeList formals,
                                        BlockStatementNode body)
            : base(nodeType, token)
        {
            Attributes = GetParentedNodeList(AttributeNode.GetAttributeList(attributes));
            Modifiers = modifiers;
            Type = GetParentedNode(returnType);
            this.name = (AtomicNameNode) GetParentedNode(name);
            Parameters = GetParentedNodeList(formals);
            Implementation = (BlockStatementNode) GetParentedNode(body);
        }

        public override ParseNodeList Attributes { get; }

        public BlockStatementNode Implementation { get; }

        public ParseNode InterfaceType => interfaceType;

        public override Modifiers Modifiers { get; }

        public override string Name
        {
            get
            {
                if(isOverloadedMethod)
                {
                    string parameters = string.Join("_", Parameters.OfType<ParameterNode>().Select(
                        n =>
                        {
                            if(n.Type.Token is IdentifierToken token)
                            {
                                return token.Identifier;
                            }

                            return Token.GetString(n.Type.Token.Type);
                        }));
                    return name.Name + "_" + parameters;
                }

                return name.Name;
            }
        }

        public ParseNodeList Parameters { get; }

        public override ParseNode Type { get; }

        public bool IsExensionMethod => Parameters.FirstOrDefault()?.As<ParameterNode>().IsExtensionMethodTarget ?? false;

        public ParseNodeList TypeParameters { get; }

        internal ParseNodeList Constraints => constraints;

        public bool IsGenericMethod => TypeParameters?.Any() ?? false;

        public bool IsOverloadedMethod => isOverloadedMethod;

        public void MarkAsOverloaded()
        {
            isOverloadedMethod = true;
        }
    }
}
