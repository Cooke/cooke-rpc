using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CookeRpc.AspNetCore.Core;
using CookeRpc.AspNetCore.Model.TypeDefinitions;

namespace CookeRpc.AspNetCore.Model
{
    public class RpcModelTypeBinder : ITypeBinder
    {
        private readonly RpcModel _rpcModel;
        private readonly Dictionary<string, List<INamedRpcType>> _typesByName;
        private readonly Dictionary<Type, INamedRpcType> _typesByClrType;

        public RpcModelTypeBinder(RpcModel rpcModel)
        {
            _rpcModel = rpcModel;
            _typesByName = rpcModel.Types.GroupBy(x => x.Name).ToDictionary(x => x.Key, x => x.ToList());
            _typesByClrType = rpcModel.Types.Where(IsObjectType).ToDictionary(x => x.ClrType, x => x);
            bool IsObjectType(INamedRpcType x) => x is ObjectRpcType;
        }

        public string GetName(Type type)
        {
            if (type.IsGenericType && !type.IsTypeDefinition) {
                var sb = new StringBuilder();
                sb.Append(GetName(type.GetGenericTypeDefinition()));
                // sb.Append('<');
                // sb.AppendJoin(',', type.GetGenericArguments().Select(GetName));
                // sb.Append('>');
                return sb.ToString();
            }

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
            return _rpcModel.Types.Count(x => x.ClrType.IsAssignableTo(targetType)) > 1 ||
                   _rpcModel.Types.Count(x => x.ClrType.IsAssignableFrom(targetType)) > 1;
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
            var rpcTypes = _typesByName.GetValueOrDefault(refDataType.Name) ?? throw CreateResolveException();


            var clrDataType = rpcTypes.Count == 1
                ? rpcTypes.First().ClrType
                : rpcTypes.Select(x => x.ClrType).FirstOrDefault(x => x.IsAssignableTo(targetType)) ??
                  throw CreateResolveException();

            // Generic
            if (clrDataType.IsGenericTypeDefinition || targetType.IsGenericTypeDefinition) {
                var genericTypeArguments = refDataType.Arguments.Zip(targetType.GenericTypeArguments)
                    .Select(pair => Resolve(pair.First, pair.Second)).ToArray();

                return clrDataType.MakeGenericType(genericTypeArguments);
            }

            return clrDataType;

            InvalidOperationException CreateResolveException()
            {
                return new InvalidOperationException(
                    $"Failed to resolve type {refDataType.Name} to target type {targetType.Name}");
            }
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
    }
}