using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CookeRpc.AspNetCore.Core;
using CookeRpc.AspNetCore.Model.TypeDefinitions;
using CookeRpc.AspNetCore.Model.Types;

namespace CookeRpc.AspNetCore.Model
{
    public class RpcModelTypeBinder : ITypeBinder
    {
        private readonly RpcModel _rpcModel;
        private readonly Dictionary<string, RpcType> _typesByName;
        private readonly Dictionary<Type, RpcTypeDefinition> _typesByClrType;

        public RpcModelTypeBinder(RpcModel rpcModel)
        {
            _rpcModel = rpcModel;
            _typesByName = rpcModel.TypesDefinitions.Where(IsConcrete).GroupBy(x => x.Name).ToDictionary(x => x.Key,
                x => (RpcType)new RpcRefType(x.First()));
            _typesByClrType = rpcModel.TypesDefinitions.Where(IsConcrete).GroupBy(x => x.ClrType)
                .ToDictionary(x => x.Key, x => x.First());

            bool IsConcrete(RpcTypeDefinition x)
            {
                return x is not (RpcInterfaceDefinition or RpcUnionDefinition);
            }
        }

        public string GetName(Type type)
        {
            // Theoretically this may be insufficient for a generic type hierarchy  
            var typeDefinition = _typesByClrType.GetValueOrDefault(type);
            if (typeDefinition == null) {
                throw new InvalidOperationException($"Cannot resolve RPC type definition for CLR type: {type}");
            }

            return typeDefinition.Name;
        }

        public Type ResolveType(string typeName, Type targetType) => Resolve(Parse(typeName), targetType);

        public bool ShouldResolveType(Type targetType)
        {
            return _rpcModel.TypesDefinitions.Count(x => x.ClrType.IsAssignableTo(targetType)) > 1 ||
                   _rpcModel.TypesDefinitions.Count(x => x.ClrType.IsAssignableFrom(targetType)) > 1;
        }

        private JsonRpcTypeRef Parse(string typeName)
        {
            int tail = 0;
            int head = 0;
            return Parse(typeName.AsSpan(), ref tail, ref head);
        }

        private record JsonRpcTypeRef(string Name, IReadOnlyCollection<JsonRpcTypeRef> Arguments);

        private Type Resolve(JsonRpcTypeRef refDataType, Type targetType)
        {
            var rpcDataType = _typesByName.GetValueOrDefault(refDataType.Name) ??
                              throw new InvalidOperationException("Failed to resolve type");
            var clrDataType = rpcDataType switch
            {
                RpcRefType customType => customType.TypeDefinition.ClrType,
                RpcGenericType genericType => genericType switch
                {
                    var x when x.InnerType == PrimitiveTypes.Array => typeof(List<>),
                    var x when x.InnerType == PrimitiveTypes.Map => typeof(Dictionary<,>),
                    _ => throw new InvalidOperationException("Failed to resolve type")
                },
                _ => throw new InvalidOperationException("Failed to resolve type")
            };

            // Generic
            if (clrDataType.IsGenericTypeDefinition || targetType.IsGenericTypeDefinition) {
                var genericTypeArguments = refDataType.Arguments.Zip(targetType.GenericTypeArguments)
                    .Select(pair => Resolve(pair.First, pair.Second)).ToArray();

                return clrDataType.MakeGenericType(genericTypeArguments);
            }

            return clrDataType;
        }

        // map<int,array<string>>

        private static JsonRpcTypeRef Parse(ReadOnlySpan<char> span, ref int tail, ref int head)
        {
            // Parse type name
            while (head < span.Length && span[head] != '<' && span[head] != '>' && span[head] != ',') {
                head++;
            }

            var name = new string(span.Slice(tail, head - tail));

            if (head >= span.Length || span[head] != '<') {
                return new JsonRpcTypeRef(name, Array.Empty<JsonRpcTypeRef>());
            }

            head++;
            tail = head;

            var args = new List<JsonRpcTypeRef>();
            while (head < span.Length && span[head] != '>') {
                args.Add(Parse(span, ref tail, ref head));
                head++;
                tail = head;
            }

            return new JsonRpcTypeRef(name, args);
        }

        private string SerializeType(RpcType rpcType)
        {
            switch (rpcType) {
                case RpcRefType:
                    return rpcType.Name ?? throw new InvalidOperationException();

                case RpcGenericType standardType:
                    if (!standardType.TypeArguments.Any()) {
                        return standardType.Name ?? throw new InvalidOperationException();
                    }

                    var sb = new StringBuilder();
                    sb.Append(standardType.Name);
                    sb.Append('<');
                    sb.AppendJoin(',', standardType.TypeArguments.Select(SerializeType));
                    sb.Append('>');
                    return sb.ToString();

                default:
                    throw new ArgumentOutOfRangeException(nameof(rpcType));
            }
        }
    }
}