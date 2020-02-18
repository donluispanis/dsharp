using System.Linq;
using DSharp.Compiler.CodeModel;
using DSharp.Compiler.CodeModel.Members;
using DSharp.Compiler.CodeModel.Names;
using DSharp.Compiler.CodeModel.Tokens;
using DSharp.Compiler.CodeModel.Types;

namespace DSharp.Compiler.Helpers
{
    internal static class ParameterParserHelper
    {
        internal static string GetSeparatedParameters(string nodeName, ParseNodeList parameters, string paramSeparator, string genericParamSeparator)
        {
            string arguments = string.Join(paramSeparator, parameters.OfType<ParameterNode>().Select(
                parameter =>
                {
                    if (parameter.Type is GenericNameNode genericNode)
                    {
                        return GenerateGenericName(parameter, genericNode, genericParamSeparator);
                    }

                    return GetTypeName(parameter);
                }));

            return nodeName + paramSeparator + arguments;
        }

        private static string GenerateGenericName(ParameterNode parameter, GenericNameNode genericNode, string separator)
        {
            string genericArguments = string.Concat(genericNode.TypeArguments.Select(node =>
            {
                string arguments = "";
                GetArguments(node, separator, ref arguments);
                return arguments;
            }));
            return GetTypeName(parameter) + separator + genericArguments;
        }

        private static string GetTypeName(ParameterNode node)
        {
            if (node.Type.Token is IdentifierToken token)
            {
                return token.Identifier;
            }

            return Token.GetString(node.Type.Token.Type);
        }

        private static void GetArguments(ParseNode parameter, string separator, ref string arguments)
        {
            if (parameter is TypeNode typeNode)
            {
                arguments += Token.GetString(typeNode.Token.Type) + separator;
            }

            if (!(parameter is GenericNameNode) && parameter is NameNode nameNode)
            {
                arguments += nameNode.Name + separator;
            }

            if (parameter is GenericNameNode genericNode)
            {
                arguments += genericNode.Name + separator;

                foreach (ParseNode node in genericNode.TypeArguments)
                {
                    GetArguments(node, separator, ref arguments);
                }
            }
        }
    }
}
