using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CookeRpc.AspNetCore.Core;
using CookeRpc.AspNetCore.Model.Types;

namespace CookeRpc.AspNetCore.Model
{
    public class RpcModelTypeBinder : ITypeBinder
    {
        private readonly RpcModel _rpcModel;
        private Dictionary<string, RpcType> _typesByName;

        public RpcModelTypeBinder(RpcModel rpcModel)
        {
            _rpcModel = rpcModel;
            _typesByName = rpcModel.MappedTypes.Values.Where(x => x.Name is not null).GroupBy(x => x.Name)
                .ToDictionary(x => x.Key!, x => x.First());
        }

        public string GetName(Type type)
        {
            var rpcType = _rpcModel.MappedTypes.GetValueOrDefault(type);
            if (rpcType == null)
            {
                throw new InvalidOperationException($"Cannot resolve RPC type for CLR type: {type}");
            }

            if (rpcType.Name == null)
            {
                throw new InvalidOperationException($"Cannot resolve type name for RPC type: {rpcType}");
            }

            return rpcType.Name;
        }

        public Type ResolveType(string typeName, Type targetType) => Resolve(Parse(typeName), targetType);

        public bool ShouldResolveType(Type targetType)
        {
            return _rpcModel.TypesDefinitions.Count(x => x.ClrType.IsAssignableTo(targetType)) > 1;
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
                NativeType nativeType => nativeType switch
                {
                    var x when x == NativeType.Map => typeof(Dictionary<,>),
                    var x when x == NativeType.Array => typeof(List<>),
                    _ when !targetType.IsAbstract => targetType,
                    _ => throw new InvalidOperationException("Failed to resolve type")
                },
                CustomType customType => customType.TypeDefinition.ClrType,
                GenericType genericType => genericType switch
                {
                    var x when x.InnerType == NativeType.Array => typeof(List<>),
                    var x when x.InnerType == NativeType.Map => typeof(Dictionary<,>),
                    _ => throw new InvalidOperationException("Failed to resolve type")
                },
                _ => throw new InvalidOperationException("Failed to resolve type")
            };

            // Generic
            if (clrDataType.IsGenericTypeDefinition || targetType.IsGenericTypeDefinition)
            {
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
            while (head < span.Length && span[head] != '<' && span[head] != '>' && span[head] != ',')
            {
                head++;
            }

            var name = new string(span.Slice(tail, head - tail));

            if (head >= span.Length || span[head] != '<')
            {
                return new JsonRpcTypeRef(name, Array.Empty<JsonRpcTypeRef>());
            }

            head++;
            tail = head;

            var args = new List<JsonRpcTypeRef>();
            while (head < span.Length && span[head] != '>')
            {
                args.Add(Parse(span, ref tail, ref head));
                head++;
                tail = head;
            }

            return new JsonRpcTypeRef(name, args);
        }

        private string SerializeType(RpcType rpcType)
        {
            switch (rpcType)
            {
                case CustomType:
                case NativeType:
                    return rpcType.Name ?? throw new InvalidOperationException();

                case GenericType standardType:
                    if (!standardType.TypeArguments.Any())
                    {
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